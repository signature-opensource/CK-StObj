using CK.Poco.Exc.Json;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using static CK.Core.PocoJsonExportSupport;

namespace CK.Core
{
    /// <summary>
    /// Provides read from Json extension methods.
    /// </summary>
    public static class PocoJsonExportExtensions
    {
        /// <inheritdoc cref="IPocoDirectoryJsonExportGenerated.WriteAnyJson(Utf8JsonWriter, object?, PocoJsonExportOptions?)"/>
        public static void WriteAnyJson( this PocoDirectory @this, Utf8JsonWriter w, object? o, PocoJsonExportOptions? options = null )
        {
            ((IPocoDirectoryJsonExportGenerated)@this).WriteAnyJson( w, o, options );
        }

        /// <summary>
        /// Serializes this Poco (that can be null) into UTF-8 Json bytes.
        /// </summary>
        /// <param name="this">The poco (can be null).</param>
        /// <param name="withType">True to emit this Poco type.</param>
        /// <param name="options">Optional export options.</param>
        /// <returns>The Utf8 bytes.</returns>
        public static ReadOnlyMemory<byte> WriteJson( this IPoco? @this, bool withType = true, PocoJsonExportOptions? options = null )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                WriteJson( @this, w, withType, options );
            }
            return m.WrittenMemory;
        }

        /// <summary>
        /// Serializes this Poco (that can be null) into a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="this">The poco (can be null).</param>
        /// <param name="w">The target writer.</param>
        /// <param name="withType">True to emit this Poco type.</param>
        /// <param name="options">Optional export options.</param>
        public static void WriteJson( this IPoco? @this, Utf8JsonWriter w, bool withType = true, PocoJsonExportOptions? options = null )
        {
            if( @this == null ) w.WriteNullValue();
            else
            {
                using var wCtx = new PocoJsonWriteContext( ((IPocoGeneratedClass)@this).Factory.PocoDirectory, options );
                ((IWriter)@this).WriteJson( w, wCtx, withType );
            }
        }

        /// <summary>
        /// Serializes this Poco (that can be null) into a <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="this">The poco (can be null).</param>
        /// <param name="utf8JsonStream">The target stream.</param>
        /// <param name="withType">True to emit this Poco type.</param>
        /// <param name="options">Optional export options.</param>
        /// <returns>The Utf8 bytes.</returns>
        public static void WriteJson( this IPoco? @this, Stream utf8JsonStream, bool withType = true, PocoJsonExportOptions? options = null )
        {
            using( var w = new Utf8JsonWriter( utf8JsonStream ) )
            {
                WriteJson( @this, w, withType, options );
            }
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
