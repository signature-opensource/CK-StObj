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
        /// Its handlers are <see cref="IJsonCodeGenHandler.IsTypeMapping"/>.
        /// </summary>
        public static readonly JsonTypeInfo Untyped = new JsonTypeInfo();

        AnnotationSetImpl _annotations;

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
        public IJsonCodeGenHandler NullHandler { get; }

        /// <summary>
        /// Not nullable type handler.
        /// </summary>
        public IJsonCodeGenHandler NonNullHandler { get; }

        /// <summary>
        /// Gets the type name. Uses the ExternalNameAttribute, the type full name
        /// or a generated name for generic List, Set and Dictionary. 
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the name used when "ECMAScript standard" is used: this is almost always <see cref="Name"/> (big integers use "Number" or "BigInt").
        /// A <see cref="ECMAScriptStandardReader"/> should be registered for this name.
        /// </summary>
        public string ECMAScriptStandardName { get; private set; }

        /// <summary>
        /// Gets whether this <see cref="Name"/> differs from this <see cref="ECMAScriptStandardName"/>.
        /// </summary>
        public bool HasECMAScriptStandardName => ECMAScriptStandardName != Name;

        /// <summary>
        /// Gets whether the writer uses a 'ref' parameter.
        /// This should be used for large struct.
        /// </summary>
        public bool ByRefWriter { get; private set; }

        /// <summary>
        /// Gets the previous names (if any).
        /// </summary>
        public IReadOnlyList<string> PreviousNames { get; }

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
        /// This is initially true (except if the type is sealed) but as soon as a type that can be assigned
        /// to this one is registered by <see cref="JsonSerializationCodeGen.AllowTypeInfo(JsonTypeInfo)"/>
        /// this becomes false.
        /// </summary>
        public bool IsFinal { get; internal set; }

        // The factory method is JsonSerializationCodeGen.CreateTypeInfo.
        internal JsonTypeInfo( Type t, int number, string name, StartTokenType startTokenType, IReadOnlyList<string>? previousNames = null )
        {
            Debug.Assert( number >= 0 && (!t.IsValueType || Nullable.GetUnderlyingType( t ) == null) );
            Type = t;
            Number = number;
            NumberName = number.ToString( System.Globalization.NumberFormatInfo.InvariantInfo );
            StartTokenType = startTokenType;
            ECMAScriptStandardName = Name = name;
            PreviousNames = previousNames ?? Array.Empty<string>();
            IsFinal = true;
            NonNullHandler = new Handler( this, Type, false, false );
            NullHandler = NonNullHandler.ToNullHandler();
        }

        // Untyped singleton object.
        JsonTypeInfo()
        {
            // CodeReader and CodeWriter are unused and let to null.
            Type = typeof( object );
            ECMAScriptStandardName = Name = String.Empty;
            PreviousNames = Array.Empty<string>();
            Number = -1;
            NumberName = String.Empty;
            StartTokenType = StartTokenType.Array;
            IsFinal = false;
            NonNullHandler = new Handler( this, Type, false, true );
            NullHandler = NonNullHandler.ToNullHandler();
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
        /// <returns>This type info.</returns>
        public JsonTypeInfo SetECMAScriptStandardName( string name )
        {
            ECMAScriptStandardName = name;
            return this;
        }


        /// <summary>
        /// Calls <see cref="CodeReader"/> inside code that handles reading null.
        /// </summary>
        /// <param name="read">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        /// <param name="isNullable">True if the variable can be null, false if it cannot be null.</param>
        public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool isNullable )
        {
            if( CodeReader == null ) throw new InvalidOperationException( "CodeReader has not been set." );
            // We currently ignore null input when the value is not nullable.
            if( isNullable )
            {
                read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" )
                    .OpenBlock()
                    .Append( variableName ).Append( " = null;" ).NewLine()
                    .Append( "r.Read();" )
                    .CloseBlock()
                    .Append( "else" )
                    .OpenBlock();
            }
            CodeReader( read, variableName, assignOnly, isNullable );
            if( isNullable ) read.CloseBlock();
        }

        /// <summary>
        /// Calls <see cref="CodeWriter"/> inside code that handles type discriminator and nullable.
        /// </summary>
        /// <param name="write">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="isNullable">Whether null value must be handled.</param>
        /// <param name="writeTypeName">True if type discriminator must be written.</param>
        public void GenerateWrite( ICodeWriter write, string variableName, bool isNullable, bool writeTypeName )
        {
            if( CodeWriter == null ) throw new InvalidOperationException( "CodeWriter has not been set." );
            if( isNullable )
            {
                write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                        .Append( "else " )
                        .OpenBlock();
            }
            if( IsIntrinsic )
            {
                CodeWriter( write, variableName );
            }
            else
            {
                if( writeTypeName )
                {
                    write.Append( "w.WriteStartArray(); w.WriteStringValue( " );
                    if( HasECMAScriptStandardName )
                    {
                        write.Append( "options?.Mode == PocoJsonSerializerMode.ECMAScriptStandard ? " ).AppendSourceString( ECMAScriptStandardName )
                             .Append( " : " );
                    }
                    write.AppendSourceString( Name ).Append( " );" ).NewLine();
                }
                bool hasBlock = false;
                if( isNullable && Type.IsValueType )
                {
                    if( ByRefWriter )
                    {
                        hasBlock = true;
                        write.OpenBlock()
                                .Append( "var notNull = " ).Append( variableName ).Append( ".Value;" ).NewLine();
                        variableName = "notNull";
                    }
                    else variableName += ".Value";
                }
                CodeWriter( write, variableName );
                if( hasBlock )
                {
                    write.CloseBlock();
                }
                if( writeTypeName )
                {
                    write.Append( "w.WriteEndArray();" ).NewLine();
                }
            }
            if( isNullable ) write.CloseBlock();
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