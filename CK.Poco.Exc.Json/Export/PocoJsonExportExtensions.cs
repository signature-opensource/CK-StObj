using CK.Poco.Exc.Json.Export;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Core
{
    public static class PocoJsonExportExtensions
    {
        /// <summary>
        /// Serializes this Poco (that can be null) into UTF-8 Json bytes.
        /// </summary>
        /// <param name="this">The poco (can be null).</param>
        /// <param name="withType">True to emit this Poco type.</param>
        /// <param name="options">Optional export options.</param>
        /// <returns>The Utf8 bytes.</returns>
        public static ReadOnlyMemory<byte> JsonSerialize( this IPoco? @this, bool withType = true, PocoJsonExportOptions? options = null )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                @this.WriteJson( w, withType, options );
                w.Flush();
            }
            return m.WrittenMemory;
        }

        /// <summary>
        /// Throws a <see cref="JsonException"/>.
        /// </summary>
        /// <param name="writer">This writer.</param>
        /// <param name="message">The exception message.</param>
        [DoesNotReturn]
        public static void ThrowJsonException( this Utf8JsonWriter writer, string message )
        {
            throw new JsonException( $"{message} - {writer.BytesCommitted} committed bytes, current depth is {writer.CurrentDepth}." );
        }

        /// <summary>
        /// Throws a <see cref="JsonException"/> with .
        /// </summary>
        /// <param name="writer">This writer.</param>
        /// <param name="message">The exception message.</param>
        [DoesNotReturn]
        public static void ThrowJsonNullWriteException( this Utf8JsonWriter writer )
        {
            ThrowJsonException( writer, "Unexpected null value for a non nullable." );
        }

    }
}
