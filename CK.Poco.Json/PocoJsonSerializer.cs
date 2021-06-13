using CK.Core;
using CK.Setup;
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
            /// Non generic version of <see cref="IFactoryReader{T}.Read(ref Utf8JsonReader)"/> method,
            /// the type must be known an the data must not be <c>null</c>.
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
            if( o == null ) writer.WriteNullValue();
            else ((IWriter)o).Write( writer, withType, options );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from the Json reader
        /// that must have been written with its type.
        /// </summary>
        /// <param name="directory">This directory.</param>
        /// <param name="reader">The Json reader.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco.</returns>
        public static IPoco? ReadPocoValue( this PocoDirectory directory, ref Utf8JsonReader reader, PocoJsonSerializerOptions? options = null )
        {
            if( CheckNullStart( ref reader, "expecting Json Poco array or null value." ) ) return null;

            if( reader.TokenType != JsonTokenType.StartArray ) throw new JsonException( "Expecting Json Poco array." );
            reader.Read();
            string name = reader.GetString();
            IPocoFactory? f = directory.Find( name );
            if( f == null ) throw new JsonException( $"Poco type '{name}' not found." );
            reader.Read();
            var p = ((IFactoryReader)f).ReadTyped( ref reader, options );
            if( reader.TokenType != JsonTokenType.EndArray ) throw new JsonException( "Expecting Json Poco end array." );
            reader.Read();
            return p;
        }

        /// <summary>
        /// Reads a typed Poco from a Json reader.
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="f">This poco factory.</param>
        /// <param name="reader">The reader.</param>
        /// <param name="options">The options.</param>
        /// <returns>The Poco.</returns>
        public static T? Read<T>( this IPocoFactory<T> f, ref Utf8JsonReader reader, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            if( CheckNullStart( ref reader, "expecting Json object, Json array or null value." ) ) return null;
            return ((IFactoryReader<T>)f).Read( ref reader, options );
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
