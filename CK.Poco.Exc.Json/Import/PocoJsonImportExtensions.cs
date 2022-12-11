using CK.Poco.Exc.Json;
using Microsoft.IO;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Core
{
    public static class PocoJsonImportExtensions
    {
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
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? JsonDeserialize<T>( this IPocoFactory<T> @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( utf8Json );
            // Dispose even if it is not currently required (no data provider).
            using var rCtx = new PocoJsonReadContext( options );
            return @this.ReadJson( ref r, rCtx );
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
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? JsonDeserialize<T>( this IPocoFactory<T> @this, string s, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            return JsonDeserialize( @this, Encoding.UTF8.GetBytes( s ).AsSpan(), options );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from Utf8 encoded bytes.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null )
        {
            var r = new Utf8JsonReader( utf8Json );
            // Dispose even if it is not currently required (no data provider).
            using var rCtx = new PocoJsonReadContext( options );
            return @this.ReadJson( ref r, rCtx );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from a JSON string.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="s">The string to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, string s, PocoJsonImportOptions? options = null )
        {
            return JsonDeserialize( @this, Encoding.UTF8.GetBytes( s ).AsSpan(), options );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from a utf8 encoded stream.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8JsonStream">The stream to deserialize from.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, Stream utf8JsonStream, PocoJsonImportOptions? options = null )
        {
            if( utf8JsonStream is RecyclableMemoryStream r )
            {
                var rSeq = new Utf8JsonReader( r.GetReadOnlySequence(), PocoJsonImportOptions.Default.ReaderOptions );
                // Dispose even if it is not currently required (no data provider).
                using var rSeqCtx = new PocoJsonReadContext( options );
                return @this.ReadJson( ref rSeq, rSeqCtx );
            }
            options ??= PocoJsonImportOptions.Default;
            using var rCtx = new PocoJsonReadContext( options, Utf8JsonStreamReader.Create( utf8JsonStream, options.ReaderOptions, out var reader ) );
            return @this.ReadJson( ref reader, rCtx );
        }

        /// <summary>
        /// Throws a <see cref="JsonException"/>.
        /// </summary>
        /// <param name="reader">This reader.</param>
        /// <param name="message">The exception message.</param>
        [DoesNotReturn]
        public static void ThrowJsonException( this ref Utf8JsonReader reader, string message )
        {
            throw new JsonException( $"{message} - {reader.BytesConsumed} consumed bytes, current depth is {reader.CurrentDepth}." );
        }

    }
}
