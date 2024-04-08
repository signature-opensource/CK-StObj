using CK.Poco.Exc.Json;
using Microsoft.IO;
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Extends <see cref="PocoDirectory"/> and <see cref="IPocoFactory{T}"/> with functions.
    /// </summary>
    public static class PocoJsonImportExtensions
    {
        /// <inheritdoc cref="IPocoDirectoryJsonImportGenerated.ReadAnyJson(ref Utf8JsonReader, PocoJsonReadContext)"/>
        public static object? ReadAnyJson( this PocoDirectory @this, ref Utf8JsonReader r, PocoJsonReadContext context )
        {
            if( r.TokenType == JsonTokenType.None ) r.ReadWithMoreData( context );
            return ((IPocoDirectoryJsonImportGenerated)@this).ReadAnyJson( ref r, context );
        }

        #region object? PocoDirectory.ReadAnyJson from ROSpan, ROSequence, string and Stream.
        /// <summary>
        /// Reads any Poco compliants types (null, number, string, etc.).
        /// IPoco and other complex types like collections or records must (obviously) be typed.
        /// </summary>
        /// <param name="this">This poco directory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The read object (null for <see cref="JsonTokenType.Null"/>).</returns>
        public static object? ReadAnyJson( this PocoDirectory @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null )
        {
            // Dispose even if it is not currently required (no data provider).
            using var rCtx = new PocoJsonReadContext( @this, options );
            var r = new Utf8JsonReader( utf8Json, rCtx.Options.ReaderOptions );
            return @this.ReadAnyJson( ref r, rCtx );
        }

        /// <inheritdoc cref="ReadJson(PocoDirectory, ReadOnlySpan{byte}, PocoJsonImportOptions?)"/>
        public static object? ReadAnyJson( this PocoDirectory @this, ReadOnlySequence<byte> utf8Json, PocoJsonImportOptions? options = null )
        {
            // Dispose even if it is not currently required (no data provider).
            using var rCtx = new PocoJsonReadContext( @this, options );
            var r = new Utf8JsonReader( utf8Json, rCtx.Options.ReaderOptions );
            return @this.ReadAnyJson( ref r, rCtx );
        }

        /// <summary>
        /// Reads any Poco compliants types (null, number, string, etc.).
        /// IPoco and other complex types like collections or records must (obviously) be typed.
        /// </summary>
        /// <param name="this">This poco directory.</param>
        /// <param name="s">The Json string.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The read object (null for <see cref="JsonTokenType.Null"/>).</returns>
        public static object? ReadAnyJson( this PocoDirectory @this, string s, PocoJsonImportOptions? options = null )
        {
            var bytes = ArrayPool<byte>.Shared.Rent( Encoding.UTF8.GetMaxByteCount( s.Length ) );
            try
            {
                int len = Encoding.UTF8.GetBytes( s, bytes );
                return @this.ReadAnyJson( bytes.AsSpan( 0, len ), options );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return( bytes );
            }
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from a utf8 encoded stream.
        /// The Poco must have been written with its type.
        /// <para>
        /// If the stream is a <see cref="RecyclableMemoryStream"/>, this uses
        /// its <see cref="RecyclableMemoryStream.GetReadOnlySequence()"/>.
        /// </para>
        /// <para>
        /// For any other kind of stream, a <see cref="Utf8JsonStreamReader"/> is used.
        /// </para>
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8JsonStream">The stream to deserialize from.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static object? ReadAnyJson( this PocoDirectory @this, Stream utf8JsonStream, PocoJsonImportOptions? options = null )
        {
            if( utf8JsonStream is RecyclableMemoryStream r )
            {
                return ReadAnyJson( @this, r.GetReadOnlySequence(), options );
            }
            options ??= PocoJsonImportOptions.Default;
            Utf8JsonStreamReader sr = Utf8JsonStreamReader.Create( utf8JsonStream,
                                                                   options.ReaderOptions,
                                                                   out var reader,
                                                                   leaveOpened: false );
            using var rCtx = new PocoJsonReadContext( @this, options, sr );
            return @this.ReadAnyJson( ref reader, rCtx );
        }
        #endregion

        #region IPoco? PocoDirectory.ReadJson from ROSpan, ROSequence, string and Stream.
        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from Utf8 encoded bytes.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? ReadJson( this PocoDirectory @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null )
        {
            // Dispose even if it is not currently required (no read context).
            using var rCtx = new PocoJsonReadContext( @this, options );
            var r = new Utf8JsonReader( utf8Json, rCtx.Options.ReaderOptions );
            return @this.ReadJson( ref r, rCtx );
        }

        /// <inheritdoc cref="ReadJson(PocoDirectory, ReadOnlySpan{byte}, PocoJsonImportOptions?)"/>
        public static IPoco? ReadJson( this PocoDirectory @this, ReadOnlySequence<byte> utf8Json, PocoJsonImportOptions? options = null )
        {
            // Dispose even if it is not currently required (no read context).
            using var rCtx = new PocoJsonReadContext( @this, options );
            var r = new Utf8JsonReader( utf8Json, rCtx.Options.ReaderOptions );
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
        public static IPoco? ReadJson( this PocoDirectory @this, string s, PocoJsonImportOptions? options = null )
        {
            var bytes = ArrayPool<byte>.Shared.Rent( Encoding.UTF8.GetMaxByteCount( s.Length ) );
            try
            {
                int len = Encoding.UTF8.GetBytes( s, bytes );
                return ReadJson( @this, bytes.AsSpan( 0, len ), options );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return( bytes );
            }
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from a utf8 encoded stream.
        /// The Poco must have been written with its type.
        /// <para>
        /// If the stream is a <see cref="RecyclableMemoryStream"/>, this uses
        /// its <see cref="RecyclableMemoryStream.GetReadOnlySequence()"/>.
        /// </para>
        /// <para>
        /// For any other kind of stream, a <see cref="Utf8JsonStreamReader"/> is used.
        /// </para>
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8JsonStream">The stream to deserialize from.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? ReadJson( this PocoDirectory @this, Stream utf8JsonStream, PocoJsonImportOptions? options = null )
        {
            if( utf8JsonStream is RecyclableMemoryStream r )
            {
                return ReadJson( @this, r.GetReadOnlySequence(), options ); 
            }
            options ??= PocoJsonImportOptions.Default;
            Utf8JsonStreamReader sr = Utf8JsonStreamReader.Create( utf8JsonStream,
                                                                   options.ReaderOptions,
                                                                   out var reader,
                                                                   leaveOpened: false );
            using var rCtx = new PocoJsonReadContext( @this, options, sr );
            return @this.ReadJson( ref reader, rCtx );
        }
        #endregion

        #region T? IPocoFactory<T>.ReadJson from ROSpan, ROSequence, string and Stream.

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
        public static T? ReadJson<T>( this IPocoFactory<T> @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            // Dispose even if it is not currently required (no data provider).
            using var rCtx = new PocoJsonReadContext( @this.PocoDirectory, options );
            var r = new Utf8JsonReader( utf8Json, rCtx.Options.ReaderOptions );
            return @this.ReadJson( ref r, rCtx );
        }

        /// <inheritdoc cref="ReadJson{T}(IPocoFactory{T}, ReadOnlySpan{byte}, PocoJsonImportOptions?)"/>
        public static T? ReadJson<T>( this IPocoFactory<T> @this, ReadOnlySequence<byte> utf8Json, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            // Dispose even if it is not currently required (no data provider).
            using var rCtx = new PocoJsonReadContext( @this.PocoDirectory, options );
            var r = new Utf8JsonReader( utf8Json, rCtx.Options.ReaderOptions );
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
        public static T? ReadJson<T>( this IPocoFactory<T> @this, string s, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            var bytes = ArrayPool<byte>.Shared.Rent( Encoding.UTF8.GetMaxByteCount( s.Length ) );
            try
            {
                int len = Encoding.UTF8.GetBytes( s, bytes );
                return ReadJson( @this, bytes.AsSpan( 0, len ), options );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return( bytes );
            }
        }

        /// <summary>
        /// Reads a typed Poco (that can be null) from a string.
        /// <para>
        /// If the buffer starts with a '[', it must be a 2-cells array with this Poco's type that
        /// comes first (otherwise an exception is thrown).
        /// If the buffer starts with a '{', then it must be the Poco's value.
        /// </para>
        /// <para>
        /// If the stream is a <see cref="RecyclableMemoryStream"/>, this uses
        /// its <see cref="RecyclableMemoryStream.GetReadOnlySequence()"/>.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="utf8JsonStream">The stream to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? ReadJson<T>( this IPocoFactory<T> @this, Stream utf8JsonStream, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            if( utf8JsonStream is RecyclableMemoryStream r )
            {
                return ReadJson( @this, r.GetReadOnlySequence(), options );
            }
            options ??= PocoJsonImportOptions.Default;
            Utf8JsonStreamReader sr = Utf8JsonStreamReader.Create( utf8JsonStream,
                                                                   options.ReaderOptions,
                                                                   out var reader,
                                                                   leaveOpened: false );
            using var rCtx = new PocoJsonReadContext( @this.PocoDirectory, options, sr );
            return @this.ReadJson( ref reader, rCtx );
        }

        #endregion

    }
}
