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
        /// <summary>
        /// Singleton for untyped "object".
        /// </summary>
        public static readonly JsonTypeInfo ObjectType = new JsonTypeInfo();

        AnnotationSetImpl _annotations;
        List<JsonTypeInfo>? _specializations;

        /// <summary>
        /// Gets the primary type.
        /// For value type, this is the not nullable type.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// The unique incremented integer <see cref="Number"/> as a string.
        /// </summary>
        public string NumberName { get; }

        /// <summary>
        /// A unique incremented integer.
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Nullable type handler.
        /// </summary>
        public IJsonCodeGenHandler NullHandler => NonNullHandler.ToNullHandler();

        /// <summary>
        /// Not nullable type handler.
        /// </summary>
        public IJsonCodeGenHandler NonNullHandler { get; }

        /// <summary>
        /// Gets whether the writer uses a 'ref' parameter.
        /// This should be used for large struct (this is used for value tuples).
        /// </summary>
        public bool ByRefWriter { get; private set; }

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
        /// </summary>
        public float TypeSpecOrder { get; internal set; }

        internal string JsonName { get; }

        internal IReadOnlyList<string> PreviousJsonNames { get; }

        internal ECMAScriptStandardJsonName ECMAScriptStandardJsonName { get; private set; }

        /// <summary>
        /// Gets the token type that starts the representation.
        /// </summary>
        public StartTokenType StartTokenType { get; }

        /// <summary>
        /// Intrinsic types don't need any type marker: Boolean (JSON True and False tokens), string (JSON String token)
        /// and double (JSON Number token).
        /// </summary>
        public bool IsIntrinsic => StartTokenType == StartTokenType.Boolean || Type == typeof( string ) || Type == typeof( double );

        /// <summary>
        /// Gets or sets whether this type is final: it is known to have no specialization.
        /// This is initially true (and always true for value types and Poco) but as soon as a reference type that
        /// can be assigned to this one is registered by <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/>
        /// this becomes false.
        /// </summary>
        public bool IsFinal => _specializations == null;

        /// <summary>
        /// Gets the ordered list of flattened specializations (all specializations recursively) from most
        /// general ones to most specialized (switch case on the type is correctly ordered).
        /// This is empty if <see cref="IsFinal"/> is true.
        /// </summary>
        public IReadOnlyList<JsonTypeInfo> AllSpecializations => _specializations ?? (IReadOnlyList<JsonTypeInfo>)Array.Empty<JsonTypeInfo>();

        // The factory method is JsonSerializationCodeGen.CreateTypeInfo.
        internal JsonTypeInfo( Type t, int number, string name, StartTokenType startTokenType, IReadOnlyList<string>? previousNames = null )
        {
            Debug.Assert( number >= 0 && (!t.IsValueType || Nullable.GetUnderlyingType( t ) == null), "Type is a reference type or a non nullable value type." );
            Type = t;
            Number = number;
            NumberName = number.ToString( System.Globalization.NumberFormatInfo.InvariantInfo );
            StartTokenType = startTokenType;
            // By default, the ECMAScriptStandardJsonName is the JsonName.
            JsonName = name;
            ECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( name, false );
            PreviousJsonNames = previousNames ?? Array.Empty<string>();
            if( t.IsValueType )
            {
                NonNullHandler = new HandlerForValueType( this );
            }
            else
            {
                var n = new HandlerForReferenceType( this );
                NonNullHandler = n.ToNonNullHandler();
            }
        }

        // Untyped singleton object.
        JsonTypeInfo()
        {
            // CodeReader and CodeWriter are unused and let to null.
            Type = typeof( object );
            JsonName = "Object";
            ECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( "Object", true );
            PreviousJsonNames = Array.Empty<string>();
            Number = -1;
            NumberName = String.Empty;
            StartTokenType = StartTokenType.Array;
            var n = new HandlerForReferenceType( this );
            NonNullHandler = n.ToNonNullHandler();
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
                if( name == "Number" && Type != typeof( double ) )
                {
                    throw new ArgumentException( "The canonical 'Number' is the double.", nameof( isCanonical ) );
                }
                if( name == "BigInt" && Type != typeof( long ) )
                {
                    throw new ArgumentException( "The canonical 'BigInt' is the long (Int64).", nameof( isCanonical ) );
                }
            }
            ECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( name, isCanonical );
            return this;
        }


        /// <summary>
        /// Calls <see cref="CodeReader"/> inside code that handles reading null if <paramref name="isNullableVariable"/> is true
        /// (otherwise simply calls CodeReader).
        /// </summary>
        /// <param name="read">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        /// <param name="isNullableVariable">True if the variable can be set to null, false if it cannot be set to null.</param>
        public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool isNullableVariable )
        {
            if( CodeReader == null ) throw new InvalidOperationException( "CodeReader has not been set." );
            if( isNullableVariable )
            {
                read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" )
                    .OpenBlock()
                    .Append( variableName ).Append( " = null;" ).NewLine()
                    .Append( "r.Read();" )
                    .CloseBlock()
                    .Append( "else" )
                    .OpenBlock();
            }
            CodeReader( read, variableName, assignOnly, isNullableVariable );
            if( isNullableVariable ) read.CloseBlock();
        }

        internal void AddSpecialization( JsonTypeInfo sub )
        {
            Debug.Assert( !Type.IsValueType && !Type.IsSealed && !Type.IsInterface
                          && !sub.Type.IsInterface && !sub.Type.IsValueType && !typeof( IPoco ).IsAssignableFrom( sub.Type ) );
            Debug.Assert( sub.TypeSpecOrder > 0.0f, "The magic is that when this is called, the TypeSpecOrder of the specialization has necessarily been called." );
            if( _specializations == null ) _specializations = new List<JsonTypeInfo>() { sub };
            else
            {
                JsonSerializationCodeGen.InsertAtTypeSpecOrder( _specializations, sub );
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

        public override string ToString() => Type.ToString();
    }
}
