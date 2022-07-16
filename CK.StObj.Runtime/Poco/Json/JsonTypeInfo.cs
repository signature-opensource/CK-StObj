using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace CK.Setup.Json
{
    /// <summary>
    /// Centralized type representation that holds the null and non-null handlers and
    /// carries the <see cref="CodeReader"/> and <see cref="CodeWriter"/> delegates.
    /// </summary>
    public partial class JsonTypeInfo : IAnnotationSet
    {
        // Empty specialization list for the following ObjectType (so that its IsFinal is false) must come first
        // because of static initialization!
        static readonly List<JsonTypeInfo> EmptySpecializations = new List<JsonTypeInfo>();
        /// <summary>
        /// Singleton for untyped "object".
        /// </summary>
        public static readonly JsonTypeInfo ObjectType = new JsonTypeInfo();

        AnnotationSetImpl _annotations;
        List<JsonTypeInfo>? _specializations;

        /// <summary>
        /// Gets the <see cref="NullableTypeTree"/> in its "normal null" form: nullable for reference types, non nullable for value types.
        /// </summary>
        public NullableTypeTree Type { get; }

        /// <summary>
        /// The unique incremented integer <see cref="Number"/> as a string.
        /// </summary>
        public string NumberName { get; }

        /// <summary>
        /// A unique incremented integer.
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Gets whether the writer uses a 'ref' parameter.
        /// This should be used for large struct (this is used for value tuples).
        /// </summary>
        public bool ByRefWriter { get; private set; }

        /// <summary>
        /// Gets the most abstract mapping nullable handler associated to this type (that is necessarily a reference type).
        /// It can be defined once (and only once) by <see cref="JsonSerializationCodeGen.AllowTypeAlias(NullableTypeTree, JsonTypeInfo, bool)"/>.
        /// </summary>
        public JsonCodeGenHandler? MostAbstractMapping { get; internal set; }

        /// <summary>
        /// Gets the most abstract Type: either the <see cref="MostAbstractMapping"/>'s one or this one.
        /// </summary>
        public Type MostAbstractType => MostAbstractMapping?.Type.Type ?? Type.Type;

        /// <summary>
        /// Gets a value that corresponds to the sort order of all the registered <see cref="JsonTypeInfo"/>.
        /// <para>
        /// The JsonTypeInfo list is ordered. UntypedObject, value types, sealed classes and Poco
        /// come first (and have a TypeSpecOrder = 0.0). Then come the "external" reference types ordered
        /// from "less IsAssignableFrom" (specialization) to "most IsAssignableFrom" (generalization) so that
        /// switch case entries on the <see cref="JsonTypeInfo.Type"/> are correctly ordered.
        /// This sort by insertion is done in the <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/> method
        /// and this is also where this TypeSpecOrder is computed and <see cref="JsonTypeInfo.Specializations"/> are added.
        /// </para>
        /// <para>
        /// This is an optimization: the IsAssignableFrom lookup is done once and any list of types (like the <see cref="JsonTypeInfo.Specializations"/>)
        /// can then be ordered by this value so that write switch can be correctly generated.
        /// </para>
        /// <para>
        /// This is also used as a flag to detect multiple calls to <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/>: it's
        /// initial value is invalid (-1.0) and AllowTypeInfo sets it to a zero or positive value. See <see cref="IsRegistered"/>.
        /// </para>
        /// </summary>
        public float TypeSpecOrder { get; internal set; }

        /// <summary>
        /// Gets whether <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/> has been called on this type information.
        /// </summary>
        public bool IsRegistered => TypeSpecOrder >= 0.0f;

        /// <summary>
        /// Nullable type handler.
        /// </summary>
        public JsonCodeGenHandler NullHandler => NonNullHandler.ToNullHandler();

        /// <summary>
        /// Not nullable type handler.
        /// </summary>
        public JsonCodeGenHandler NonNullHandler { get; }

        /// <summary>
        /// Gets the non null handler that is used in the generic write. It is this <see cref="NonNullHandler"/> except
        /// for generic type with subordinated non nullable reference types. In such case, the handler is the one of the type
        /// in the "oblivious nullable reference type context" (note that the <see cref="NullableTypeTree.ObliviousDefaultBuilder"/>
        /// is applied; so that dictionaries' key is non nullable).
        /// </summary>
        public JsonCodeGenHandler GenericWriteHandler { get; }

        internal string NonNullableJsonName { get; }

        internal IReadOnlyList<string> NonNullablePreviousJsonNames { get; }

        internal ECMAScriptStandardJsonName NonNullableECMAScriptStandardJsonName { get; private set; }

        /// <summary>
        /// Intrinsic types don't need any type marker: Boolean (JSON True and False tokens), string (JSON String token)
        /// and double (JSON Number token).
        /// </summary>
        public bool IsIntrinsic => Type.Type == typeof( bool ) || Type.Type == typeof( string ) || Type.Type == typeof( double );

        /// <summary>
        /// Gets whether this type is final: it is known to have no specialization.
        /// This is initially true (and always true for value types and Poco) but as soon
        /// as a reference type that can be assigned to this one is registered by <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/>
        /// this becomes false.
        /// <para>
        /// This is always false for the <see cref="JsonTypeInfo.ObjectType"/>.
        /// </para>
        /// </summary>
        public bool IsFinal => _specializations == null;

        /// <summary>
        /// Gets the non nullable (oblivious) CSharp name to use in generated code.
        /// Nullable handlers use the Nullable type for value types but since the generated code is
        /// not NRT aware, this is the CSharpName for both nullable and not nullable reference types.
        /// </summary>
        public string GenCSharpName { get; }

        /// <summary>
        /// Gets the ordered list of flattened specializations (all specializations recursively) from most
        /// general ones to most specialized (switch case on the type is correctly ordered).
        /// This is empty if <see cref="IsFinal"/> is true.
        /// </summary>
        public IReadOnlyList<JsonTypeInfo> AllSpecializations => _specializations ?? EmptySpecializations;

        // The factory method is JsonSerializationCodeGen.CreateTypeInfo.
        internal JsonTypeInfo( NullableTypeTree t, int number, string name, IReadOnlyList<string>? previousNames, JsonCodeGenHandler? writeHandler )
        {
            Debug.Assert( number >= 0 && t.IsNormalNull );
            Type = t;
            // We cannot use the oblivious type (that may have computed for the writeHandler) here because
            // value tuple must use their generic form (not the parentheses) in switch case.
            GenCSharpName = t.Type.ToCSharpName( useValueTupleParentheses: false );
            Number = number;
            NumberName = number.ToString( System.Globalization.NumberFormatInfo.InvariantInfo );
            TypeSpecOrder = -1.0f;
            // By default, the ECMAScriptStandardJsonName is the JsonName.
            NonNullableJsonName = name;
            NonNullableECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( name, false );
            NonNullablePreviousJsonNames = previousNames ?? Array.Empty<string>();
            if( t.Type.IsValueType )
            {
                NonNullHandler = new HandlerForValueType( this );
            }
            else
            {
                var n = new HandlerForReferenceType( this );
                NonNullHandler = n.ToNonNullHandler();
            }
            GenericWriteHandler = writeHandler ?? NonNullHandler;
        }

        // Untyped singleton object.
        JsonTypeInfo()
        {
            Type = typeof( object ).GetNullableTypeTree();
            GenCSharpName = "object";
            NonNullableJsonName = "Object";
            NonNullableECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( "Object", true );
            NonNullablePreviousJsonNames = Array.Empty<string>();
            Number = -1;
            TypeSpecOrder = -1.0f;
            NumberName = String.Empty;
            _specializations = EmptySpecializations;
            var n = new HandlerForReferenceType( this );
            NonNullHandler = n.ToNonNullHandler();
            GenericWriteHandler = NonNullHandler;
      }

        /// <summary>
        /// Gets or sets the generator that writes the code to read a value into a variable.
        /// </summary>
        public CodeReader? CodeReader { get; set; }

        /// <summary>
        /// Gets or sets the generator that writes the code to write the value of a variable.
        /// </summary>
        public CodeWriter? CodeWriter { get; set; }

        /// <summary>
        /// Sets the writer and reader delegates.
        /// </summary>
        /// <param name="w">The writer.</param>
        /// <param name="r">The reader.</param>
        /// <returns>This type info.</returns>
        public JsonTypeInfo Configure( CodeWriter w, CodeReader r )
        {
            CodeWriter = w;
            CodeReader = r;
            return this;
        }

        /// <summary>
        /// Sets whether the <see cref="CodeWriter"/> uses a "ref" for the variable.
        /// </summary>
        /// <returns>This type info.</returns>
        public JsonTypeInfo SetByRefWriter()
        {
            ByRefWriter = true;
            return this;
        }

        /// <summary>
        /// Sets the name to use when using "ECMAScript Standard" serialization mode.
        /// A <see cref="ECMAScriptStandardReader"/> should be registered for this name.
        /// </summary>
        /// <param name="name">The name to use.</param>
        /// <param name="isCanonical">Whether this standard name is the canonical one.</param>
        /// <returns>This type info.</returns>
        public JsonTypeInfo SetECMAScriptStandardName( string name, bool isCanonical )
        {
            // This is a security check. Only these types must be the "canonical" one.
            // If other categories than Number and BigInt are defined, a similar unicity check should
            // be done somewhere.
            if( isCanonical )
            {
                if( name == "Number" && Type.Type != typeof( double ) )
                {
                    throw new ArgumentException( "The canonical 'Number' is the double.", nameof( isCanonical ) );
                }
                if( name == "BigInt" && Type.Type != typeof( long ) )
                {
                    throw new ArgumentException( "The canonical 'BigInt' is the long (Int64).", nameof( isCanonical ) );
                }
            }
            NonNullableECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( name, isCanonical );
            return this;
        }

        internal void AddSpecialization( JsonTypeInfo sub )
        {
            Debug.Assert( !Type.Type.IsValueType && !Type.Type.IsSealed && !Type.Type.IsInterface
                          && !sub.Type.Type.IsInterface && !sub.Type.Type.IsValueType && !typeof( IPoco ).IsAssignableFrom( sub.Type.Type ) );
            Debug.Assert( sub.TypeSpecOrder > 0.0f, "The magic is that when this is called, the TypeSpecOrder of the specialization has necessarily been computed." );
            if( _specializations == null ) _specializations = new List<JsonTypeInfo>() { sub };
            else
            {
                Debug.Assert( _specializations.TrueForAll( s => s.TypeSpecOrder != sub.TypeSpecOrder ), "No existing specialization with the same TypeSpecOrder." );
                JsonSerializationCodeGen.InsertAtTypeSpecOrderUnsafe( _specializations, sub, 0 );
            }
        }

        /// <inheritdoc />
        public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

        /// <inheritdoc />
        public object? Annotation( Type type ) => _annotations.Annotation( type );

        /// <inheritdoc />
        public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

        /// <inheritdoc />
        public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

        /// <inheritdoc />
        public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

        /// <inheritdoc />
        public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

        /// <inheritdoc />
        public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        /// <summary>
        /// Overridden to return the <see cref="GenCSharpName"/>.
        /// </summary>
        /// <returns>The <see cref="GenCSharpName"/>.</returns>
        public override string ToString() => GenCSharpName;
    }
}
