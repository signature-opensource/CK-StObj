using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static readonly JsonTypeInfo Untyped = new JsonTypeInfo();

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

        internal string JsonName { get; }

        internal IReadOnlyList<string> PreviousJsonNames { get; }

        internal ECMAScriptStandardJsonName ECMAScriptStandardJsonName { get; private set; }

        /// <summary>
        /// Gets the token type that starts the representation.
        /// </summary>
        public StartTokenType StartTokenType { get; }

        /// <summary>
        /// Gets whether this is the untyped "object".
        /// </summary>
        public bool IsUntypedType => Type == typeof( object );

        /// <summary>
        /// Intrinsic types don't need any type marker: Boolean (JSON True and False tokens), string (JSON String token)
        /// and double (JSON Number token).
        /// </summary>
        public bool IsIntrinsic => StartTokenType == StartTokenType.Boolean || Type == typeof( string ) || Type == typeof( double );

        /// <summary>
        /// Gets or sets whether this type is final: it is known to have no specialization.
        /// This is initially true but as soon as a type that can be assigned
        /// to this one is registered by <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/>
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
                NonNullHandler = new HandlerForReferenceType( this );
            }
        }

        // Untyped singleton object.
        JsonTypeInfo()
        {
            // CodeReader and CodeWriter are unused and let to null.
            Type = typeof( object );
            JsonName = "Object";
            ECMAScriptStandardJsonName = new ECMAScriptStandardJsonName( "", true );
            PreviousJsonNames = Array.Empty<string>();
            Number = -1;
            NumberName = String.Empty;
            StartTokenType = StartTokenType.Array;
            NonNullHandler = new HandlerForObjectMapping( Type );
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
        /// Calls <see cref="CodeReader"/> inside code that handles reading null.
        /// </summary>
        /// <param name="read">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        /// <param name="isNullableVariable">True if the variable can be set to null, false if it cannot be set to null.</param>
        public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool isNullableVariable )
        {
            if( CodeReader == null ) throw new InvalidOperationException( "CodeReader has not been set." );
            // We currently ignore null input when the value is not nullable.
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
            if( _specializations == null ) _specializations = new List<JsonTypeInfo>() { sub };
            else
            {
                // Repeating the same sort by insertions here that has been done
                // on the global list.
                // This seems inefficient, but I failed to find a better way without
                // yet another type tree model and the fact is that :
                //  - Since the JsonTypes are purely opt-in, crawling the base types is not an option
                //    (we don't know if they need to be registered: this would imply to manage a kind of waiting list).
                //  - Only "external" non-poco classes are concerned, there should not be a lot of them.
                // May be another solution would be to, in the Finalization step, to 
                int i = 0;
                for( ; i < _specializations.Count; ++i )
                {
                    if( _specializations[i].Type.IsAssignableFrom( sub.Type ) )
                    {
                        break;
                    }
                }
                _specializations.Insert( i, sub );
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
