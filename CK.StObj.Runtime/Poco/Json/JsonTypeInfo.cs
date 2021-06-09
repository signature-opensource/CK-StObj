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
    public partial class JsonTypeInfo : IAnnotationSet
    {
        /// <summary>
        /// Singleton for "object".
        /// Its handlers are <see cref="IJsonCodeGenHandler.IsMappedType"/>.
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

        /// <summary>
        /// Gets or sets whether this type is final: it is known to have no specialization.
        /// When let to null (the default), all registered types are automatically challenged before
        /// generating the code.
        /// <para>
        /// The rule is rather simple: as soon as another <see cref="JsonTypeInfo.Type"/> can
        /// satisfy this type (i.e. this type is assignable from the other), then this type is
        /// not final.
        /// </para>
        /// </summary>
        public bool? IsFinal { get; set; }

        // The factory method is JsonSerializationCodeGen.CreateTypeInfo.
        internal JsonTypeInfo( Type t, int number, string name, IReadOnlyList<string>? previousNames = null, JsonDirectType d = JsonDirectType.None, bool? isFinal = null )
        {
            Debug.Assert( number >= 0 && (!t.IsValueType || Nullable.GetUnderlyingType( t ) == null) );
            Type = t;
            Number = number;
            NumberName = number.ToString();
            Name = name;
            PreviousNames = previousNames ?? Array.Empty<string>();
            DirectType = d;
            if( isFinal == null && t.IsValueType ) isFinal = true;
            IsFinal = isFinal;
            NonNullHandler = new Handler( this, Type, false, isFinal == false );
            NullHandler = NonNullHandler.ToNullHandler();
        }

        // Untyped singleton object.
        JsonTypeInfo()
        {
            // _writer and _reader are unused and let to null.
            Type = typeof( object );
            Name = String.Empty;
            PreviousNames = Array.Empty<string>();
            Number = -1;
            NumberName = String.Empty;
            DirectType = JsonDirectType.Untyped;
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
        /// Calls <see cref="CodeReader"/> inside code that handles reading null.
        /// Note that <see cref="IsFinal"/> must be true otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="read">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="assignOnly">True is the variable must be only assigned: no in-place read is possible.</param>
        /// <param name="isNullable">True if the variable can be null, false if it cannot be null.</param>
        public void GenerateRead( ICodeWriter read, string variableName, bool assignOnly, bool isNullable )
        {
            if( !IsFinal.HasValue ) throw new InvalidOperationException( $"Json Type '{Name}' requires Json Type finalization before GenerateRead can be called." );
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
            switch( DirectType )
            {
                case JsonDirectType.Number: read.Append( variableName ).Append( " = r.GetInt32(); r.Read();" ); break;
                case JsonDirectType.String: read.Append( variableName ).Append( " = r.GetString(); r.Read();" ); break;
                case JsonDirectType.Boolean: read.Append( variableName ).Append( " = r.GetBoolean(); r.Read();" ); break;
                default:
                    {
                        Debug.Assert( CodeReader != null );
                        CodeReader( read, variableName, assignOnly, isNullable );
                        break;
                    }
            }
            if( isNullable ) read.CloseBlock();
        }

        /// <summary>
        /// Calls <see cref="CodeWriter"/> inside code that handles type discriminator and nullable.
        /// Note that <see cref="IsFinal"/> must be true otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="write">The code target.</param>
        /// <param name="variableName">The variable name.</param>
        /// <param name="isNullable">Whether null value must be handled.</param>
        /// <param name="writeTypeName">The non null type name if type discriminator must be written.</param>
        public void GenerateWrite( ICodeWriter write, string variableName, bool isNullable, string? writeTypeName )
        {
            if( !IsFinal.HasValue ) throw new InvalidOperationException( $"Json Type '{Name}' requires Json Type finalization before GenerateWrite can be called." );
            if( isNullable )
            {
                write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                        .Append( "else " )
                        .OpenBlock();
            }
            switch( DirectType )
            {
                case JsonDirectType.Number: write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" ); break;
                case JsonDirectType.String: write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" ); break;
                case JsonDirectType.Boolean: write.Append( "w.WriteBooleanValue( " ).Append( variableName ).Append( " );" ); break;
                default:
                    {
                        if( writeTypeName != null )
                        {
                            write.Append( "w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( writeTypeName ).Append( ");" ).NewLine();
                        }
                        Debug.Assert( CodeWriter != null );
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
                        if( writeTypeName != null )
                        {
                            write.Append( "w.WriteEndArray();" ).NewLine();
                        }
                        break;
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
