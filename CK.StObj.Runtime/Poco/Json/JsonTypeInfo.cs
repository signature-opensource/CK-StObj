using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Centralized type representation that holds the null and non-null handlers and
    /// carries the <see cref="CodeReader"/> and <see cref="CodeWriter"/> delegates.
    /// </summary>
    public class JsonTypeInfo : IAnnotationSet
    {
        /// <summary>
        /// Singleton for "object".
        /// Its handlers are <see cref="IJsonCodeGenHandler.IsAbstractType"/>.
        /// </summary>
        public static readonly JsonTypeInfo Untyped = new JsonTypeInfo();

        AnnotationSetImpl _annotations;

        class Handler : IJsonCodeGenHandler
        {
            public JsonTypeInfo Info { get; }
            public bool IsNullable { get; }
            public Type Type { get; }
            public string Name { get; }
            public bool IsAbstractType { get; }

            readonly Handler _otherHandler;

            public Handler( JsonTypeInfo info, Type t, bool isNullable, bool isAbstractType )
            {
                IsNullable = isNullable;
                IsAbstractType = isAbstractType;
                Info = info;
                Type = t;
                Name = info.Name;
                if( isNullable )
                {
                    Type = GetNullableType( t );
                    Name += '?';
                }
                _otherHandler = new Handler( this );
            }

            Handler( Handler other )
            {
                _otherHandler = other;
                IsNullable = !other.IsNullable;
                IsAbstractType = other.IsAbstractType;
                Info = other.Info;
                Name = other.Info.Name;
                if( IsNullable )
                {
                    Type = GetNullableType( other.Type );
                    Name += '?';
                }
                else Type = other.Type;
            }

            static Type GetNullableType( Type t )
            {
                if( t.IsValueType )
                {
                    t = typeof( Nullable<> ).MakeGenericType( t );
                }
                return t;
            }

            public void GenerateWrite( ICodeWriter write, string variableName, bool? withType = null, bool skipNullable = false )
            {
                if( Info.DirectType == JsonDirectType.Untyped )
                {
                    write.Append( "PocoDirectory_CK.WriteObject( w, " ).Append( variableName ).Append( ");" ).NewLine();
                    return;
                }
                bool isNullable = IsNullable && !skipNullable;
                if( isNullable )
                {
                    write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                            .Append( "else " )
                            .OpenBlock();
                }
                switch( Info.DirectType )
                {
                    case JsonDirectType.Number: write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" ); break;
                    case JsonDirectType.String: write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" ); break;
                    case JsonDirectType.Boolean: write.Append( "w.WriteBooleanValue( " ).Append( variableName ).Append( " );" ); break;
                    default:
                        {
                            bool writeType = (withType.HasValue ? withType.Value : IsAbstractType);
                            if( writeType )
                            {
                                write.Append( "w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( Name ).Append( ");" ).NewLine();
                            }
                            Debug.Assert( Info.CodeWriter != null );
                            bool hasBlock = false;
                            if( isNullable && Type.IsValueType )
                            {
                                if( Info.ByRefWriter )
                                {
                                    hasBlock = true;
                                    write.OpenBlock()
                                         .Append( "var notNull = " ).Append( variableName ).Append( ".Value;" ).NewLine();
                                    variableName = "notNull";
                                }
                                else variableName += ".Value";
                            }
                            Info.CodeWriter( write, variableName );
                            if( hasBlock )
                            {
                                write.CloseBlock();
                            }
                            if( writeType )
                            {
                                write.Append( "w.WriteEndArray();" ).NewLine();
                            }
                            break;
                        }
                }
                if( isNullable ) write.CloseBlock();
            }

            public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool skipIfNullBlock = false )
            {
                if( Info.DirectType == JsonDirectType.Untyped )
                {
                    read.Append( variableName ).Append( " = (" ).AppendCSharpName( Type ).Append( ")PocoDirectory_CK.ReadObject( ref r );" ).NewLine();
                    return;
                }
                bool isNullable = IsNullable && !skipIfNullBlock;
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
                switch( Info.DirectType )
                {
                    case JsonDirectType.Number: read.Append( variableName ).Append( " = r.GetInt32(); r.Read();" ); break;
                    case JsonDirectType.String: read.Append( variableName ).Append( " = r.GetString(); r.Read();" ); break;
                    case JsonDirectType.Boolean: read.Append( variableName ).Append( " = r.GetBoolean(); r.Read();" ); break;
                    default:
                        {
                            Debug.Assert( Info.CodeReader != null );
                            Info.CodeReader( read, variableName, assignOnly, isNullable );
                            break;
                        }
                }
                if( isNullable ) read.CloseBlock();
            }

            public IJsonCodeGenHandler CreateAbstract( Type t )
            {
                return new Handler( Info, t, IsNullable, true );
            }

            public IJsonCodeGenHandler ToNullHandler() => IsNullable ? this : _otherHandler;

            public IJsonCodeGenHandler ToNonNullHandler() => IsNullable ? _otherHandler : this;
        }

        /// <summary>
        /// Gets the primary type.
        /// For value type, this is the not nullable type.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// A unique incremented integer as a string.
        /// </summary>
        public string NumberName { get; }

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
        /// Gets whether the writer uses a 'ref' parameter.
        /// This should be used for large struct.
        /// </summary>
        public bool ByRefWriter { get; private set; }

        /// <summary>
        /// Gets the previous names (if any).
        /// </summary>
        public IReadOnlyList<string> PreviousNames { get; }

        /// <summary>
        /// Gets the <see cref="JsonDirectType"/>.
        /// </summary>
        public JsonDirectType DirectType { get; }

        // The factory method is JsonSerializationCodeGen.CreateTypeInfo.
        internal JsonTypeInfo( Type t, int number, string name, IReadOnlyList<string>? previousNames = null, JsonDirectType d = JsonDirectType.None, bool isAbstractType = false )
        {
            Debug.Assert( number >= 0 && (!t.IsValueType || Nullable.GetUnderlyingType( t ) == null) );
            Type = t;
            NumberName = number.ToString();
            Name = name;
            PreviousNames = previousNames ?? Array.Empty<string>();
            DirectType = d;
            NonNullHandler = new Handler( this, Type, false, isAbstractType );
            NullHandler = NonNullHandler.ToNullHandler();
        }

        // Untyped singleton object.
        JsonTypeInfo()
        {
            // _writer and _reader are unused and let to null.
            Type = typeof( object );
            Name = String.Empty;
            PreviousNames = Array.Empty<string>();
            NumberName = String.Empty;
            DirectType = JsonDirectType.Untyped;
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
    }
}
