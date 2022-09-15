using CK.Core;
using CK.Setup;
using System;
using System.Buffers;
using System.Text.Json;
using System.Xml.Schema;

namespace CK.Core
{
    /// <summary>
    /// Provides serialization and deserialization as Json for (at least) <see cref="IPoco"/>
    /// objects.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.Json.PocoJsonSerializerImpl, CK.StObj.Engine" )]
    public static class PocoJsonSerializer
    {
        /// <summary>
        /// This interface is automatically supported by IPoco.
        /// The <see cref="PocoJsonSerializer.Write"/> extension methods exposes it.
        /// </summary>
        public interface IWriter
        {
            /// <summary>
            /// Writes this IPoco as Json.
            /// </summary>
            /// <param name="writer">The Json writer.</param>
            /// <param name="options">The options.</param>
            /// <param name="withType">
            /// When true, a 2-cells array contains the Poco's name first and then the Poco's value.
            /// When false, the Poco's value object is directly written.
            /// </param>
            void Write( Utf8JsonWriter writer, bool withType, PocoJsonSerializerOptions? options = null );
        }

        /// <summary>
        /// Base interface reader for typed Poco.
        /// </summary>
        public interface IFactoryReader
        {
            /// <summary>
            /// Non generic version of <see cref="IFactoryReader{T}.Read(ref Utf8JsonReader, PocoJsonSerializerOptions?)"/> method,
            /// the type must be known and the data must not be <c>null</c>.
            /// <para>
            /// If the reader is <see cref="JsonTokenType.StartArray"/>, it must be a 2-cells array
            /// with this Poco's type that comes first (otherwise an exception is thrown).
            /// When the reader is <see cref="JsonTokenType.StartObject"/>, then it is the Poco's value.
            /// </para>
            /// </summary>
            /// <param name="reader">The Json reader.</param>
            /// <param name="options">The options.</param>
            /// <returns>A new Poco.</returns>
            IPoco ReadTyped( ref Utf8JsonReader reader, PocoJsonSerializerOptions? options = null );
        }

        /// <summary>
        /// This interface is automatically supported by <see cref="IPocoFactory{T}"/>.
        /// The <see cref="PocoJsonSerializer.Read"/> extension method exposes it.
        /// </summary>
        /// <typeparam name="T">The Poco type.</typeparam>
        public interface IFactoryReader<T> : IFactoryReader where T : class, IPoco
        {
            /// <summary>
            /// Reads the known typed Poco from a Json.
            /// If the reader is <see cref="JsonTokenType.StartArray"/>, it must be a 2-cells array
            /// with this Poco's type that comes first (otherwise an exception is thrown).
            /// When the reader is <see cref="JsonTokenType.StartObject"/>, then it is the Poco's value.
            /// </summary>
            /// <param name="reader">The Json reader.</param>
            /// <param name="options">The options.</param>
            T? Read( ref Utf8JsonReader reader, PocoJsonSerializerOptions? options = null );
        }        

        /// <summary>
        /// Writes this IPoco (that can be null) as Json.
        /// When this is null, the Json null value is written.
        /// </summary>
        /// <param name="o">This Poco (that can be null).</param>
        /// <param name="writer">The Json writer.</param>
        /// <param name="withType">
        /// When true (the default), a 2-cells array contains the Poco's <see cref="IPocoFactory.Name"/> first
        /// and then the Poco's value.
        /// When false, the Poco's value object is directly written.
        /// </param>
        /// <param name="options">The options.</param>
        public static void Write( this IPoco? o, Utf8JsonWriter writer, bool withType = true, PocoJsonSerializerOptions? options = null )
        {
            Throw.CheckNotNullArgument( writer );
            if( o == null ) writer.WriteNullValue();
            else ((IWriter)o).Write( writer, withType, options );
        }

        /// <summary>
        /// Serializes this Poco (that can be null) into UTF-8 Json bytes.
        /// </summary>
        /// <param name="this">The poco (can be null).</param>
        /// <param name="withType">True to emit this Poco type.</param>
        /// <param name="options">Options to use.</param>
        /// <returns>The Utf8 bytes.</returns>
        public static ReadOnlyMemory<byte> JsonSerialize( this IPoco? @this, bool withType = true, PocoJsonSerializerOptions? options = null )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                @this.Write( w, withType, options: options );
                w.Flush();
            }
            return m.WrittenMemory;
        }

        /// <summary>
        /// Reads a typed Poco from a Json reader (that can be null).
        /// <para>
        /// If the reader starts with a '[', it must be a 2-cells array with this Poco's type that
        /// comes first (otherwise an exception is thrown).
        /// If the reader starts with a '{', then it must be the Poco's value.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="reader">The reader.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? Read<T>( this IPocoFactory<T> @this, ref Utf8JsonReader reader, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            Throw.CheckNotNullArgument( @this );
            if( CheckNullStart( ref reader, "expecting Json object, Json array or null value." ) ) return null;
            return ((IFactoryReader<T>)@this).Read( ref reader, options );
        }

        /// <summary>
        /// Reads a typed Poco (that can be null) from Utf8 encoded bytes.
        /// <para>
        /// If the buffer starts with a '[', it must be a 2-cells array with this Poco's type that
        /// comes first (otherwise an exception is thrown).
        /// If the buffer starts with a '{', then it must be the Poco's value.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? JsonDeserialize<T>( this IPocoFactory<T> @this, ReadOnlySpan<byte> utf8Json, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( utf8Json );
            return Read( @this, ref r, options );
        }

        /// <summary>
        /// Reads a typed Poco (that can be null) from a string.
        /// <para>
        /// If the buffer starts with a '[', it must be a 2-cells array with this Poco's type that
        /// comes first (otherwise an exception is thrown).
        /// If the buffer starts with a '{', then it must be the Poco's value.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="s">The string to deserialize.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? JsonDeserialize<T>( this IPocoFactory<T> @this, string s, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            return JsonDeserialize( @this, System.Text.Encoding.UTF8.GetBytes( s ).AsSpan(), options );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from the Json reader
        /// that must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="reader">The Json reader.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? Read( this PocoDirectory @this, ref Utf8JsonReader reader, PocoJsonSerializerOptions? options = null )
        {
            Throw.CheckNotNullArgument( @this );
            if( CheckNullStart( ref reader, "expecting Json Poco array or null value." ) ) return null;

            if( reader.TokenType != JsonTokenType.StartArray ) throw new JsonException( "Expecting Json Poco array." );
            reader.Read();
            string? name = reader.GetString();
            IPocoFactory? f = name != null ? @this.Find( name ) : null;
            if( f == null ) throw new JsonException( $"Poco type '{name}' not found." );
            reader.Read();
            var p = ((IFactoryReader)f).ReadTyped( ref reader, options );
            if( reader.TokenType != JsonTokenType.EndArray ) throw new JsonException( "Expecting Json Poco end array." );
            reader.Read();
            return p;
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from Utf8 encoded bytes.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, ReadOnlySpan<byte> utf8Json, PocoJsonSerializerOptions? options = null )
        {
            var r = new Utf8JsonReader( utf8Json );
            return Read( @this, ref r, options );
        }


        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from a JSON string.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="s">The string to deserialize.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, string s, PocoJsonSerializerOptions? options = null )
        {
            return JsonDeserialize( @this, System.Text.Encoding.UTF8.GetBytes( s ).AsSpan(), options );
        }

        static bool CheckNullStart( ref Utf8JsonReader reader, string error )
        {
            if( reader.TokenStartIndex == 0 && (!reader.Read() || reader.TokenType == JsonTokenType.None) )
            {
                throw new JsonException( "Empty reader: " + error );
            }
            if( reader.TokenType == JsonTokenType.Null )
            {
                reader.Read();
                return true;
            }
            return false;
        }
    }
}
