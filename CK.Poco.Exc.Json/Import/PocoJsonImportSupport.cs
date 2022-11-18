using CK.Core;
using CK.Poco.Exc.Json.Import;
using CK.Setup;
using System;
using System.Buffers;
using System.Text.Json;
using System.Xml.Schema;

namespace CK.Core
{
    /// <summary>
    /// Supports Json deserialization for <see cref="IPoco"/> types.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.PocoJson.PocoJsonImportImpl, CK.Poco.Exc.Json.Engine" )]
    public static class PocoJsonImportSupport
    {
        /// <summary>
        /// Base interface reader for typed Poco.
        /// </summary>
        public interface IFactoryReader
        {
            /// <summary>
            /// Non generic version of <see cref="IFactoryReader{T}.Read(ref Utf8JsonReader)"/> method,
            /// the type must be known and the data must not be <c>null</c>.
            /// <para>
            /// If the reader is <see cref="JsonTokenType.StartArray"/>, it must be a 2-cells array
            /// with this Poco's type that comes first (otherwise an exception is thrown).
            /// When the reader is <see cref="JsonTokenType.StartObject"/>, then it is the Poco's value.
            /// </para>
            /// </summary>
            /// <param name="reader">The Json reader.</param>
            /// <param name="options">Optional import options.</param>
            /// <returns>A new Poco.</returns>
            IPoco ReadTyped( ref Utf8JsonReader reader, PocoJsonImportOptions? options = null );
        }

        /// <summary>
        /// This interface is automatically supported by <see cref="IPocoFactory{T}"/>.
        /// The <see cref="PocoJsonExportSupport.ReadJson"/> extension method exposes it.
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
            /// <param name="options">Optional import options.</param>
            T? Read( ref Utf8JsonReader reader, PocoJsonImportOptions? options = null );
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
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? ReadJson<T>( this IPocoFactory<T> @this, ref Utf8JsonReader reader, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            Throw.CheckNotNullArgument( @this );
            if( CheckNullStart( ref reader, "expecting Json object, Json array or null value." ) ) return null;
            return ((IFactoryReader<T>)@this).Read( ref reader, options );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from the Json reader
        /// that must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="reader">The Json reader.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? ReadJson( this PocoDirectory @this, ref Utf8JsonReader reader, PocoJsonImportOptions? options = null )
        {
            Throw.CheckNotNullArgument( @this );
            if( CheckNullStart( ref reader, "expecting Json Poco array or null value." ) ) return null;

            if( reader.TokenType != JsonTokenType.StartArray ) throw new JsonException( "Expecting Json Poco array." );
            reader.Read();
            string? name = reader.GetString();
            IPocoFactory? f = name != null ? @this.Find( name ) : null;
            if( f == null ) throw new JsonException( $"Poco type '{name}' not found." );
            reader.Read();
            var p = ((IFactoryReader)f).ReadTyped( ref reader );
            if( reader.TokenType != JsonTokenType.EndArray ) throw new JsonException( "Expecting Json Poco end array." );
            reader.Read();
            return p;
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