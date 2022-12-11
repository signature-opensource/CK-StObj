using Microsoft.IO;
using Microsoft.VisualBasic;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Provides a reading buffer on a Utf8 stream of bytes. 
    /// The Utf8 Byte Order Mask (BOM: 0xEF, 0xBB, 0xBF) that may exist at the start is
    /// kindly handled.
    /// <para>
    /// The buffer comes from the <see cref="ArrayPool{T}.Shared"/> of bytes: this
    /// reader MUST be disposed to return the current buffer to the pool.
    /// </para>
    /// <para>
    /// The pattern to use it is to replace all <c>Read()</c> calls with:
    /// <code>
    /// if( !reader.Read() ) streamReader.ReadMoreData( ref reader );
    /// </code>
    /// And all <c>Skip()</c> calls with:
    /// <code>
    /// if( !reader.TrySkip() ) streamReader.SkipMoreData( ref reader );
    /// </code>
    /// </para>
    /// </summary>
    public sealed class Utf8JsonStreamReader : IDisposable, IUtf8JsonReaderDataProvider
    {
        readonly Stream _stream;
        byte[] _buffer;
        int _initialOffset;
        readonly bool _leaveOpened;
        int _count;

#if DEBUG
        // We can test/change this in debug mode only.
        public static int MaxBufferSize = int.MaxValue;
#else
        const int MaxBufferSize = int.MaxValue;
#endif
        Utf8JsonStreamReader( Stream stream, byte[] buffer, int count, int initialOffset, bool leaveOpened )
        {
            _stream = stream;
            _buffer = buffer;
            _count = count;
            _initialOffset = initialOffset;
            _leaveOpened = leaveOpened;
        }

        /// <summary>
        /// Creates a new <see cref="Utf8JsonStreamReader"/> and an initial reader.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="options">The Json reader options.</param>
        /// <param name="r">The initial reader.</param>
        /// <param name="leaveOpened">
        /// True to let the stream opened when disposing the reader. to dispose it.
        /// By default, the stream is disposed.
        /// </param>
        /// <param name="initialBufferSize">
        /// Initial buffer size (will grow as needed). The buffer size is only driven by
        /// the longest token (plus some white space) to read.
        /// Current <c>>ArrayPool&lt;byte&gt.Shared</c> that is used returns at least 16 bytes: the
        /// initial buffer size will at least be 16.
        /// </param>
        /// <returns>A new stream reader.</returns>
        public static Utf8JsonStreamReader Create( Stream stream,
                                                   JsonReaderOptions options,
                                                   out Utf8JsonReader r,
                                                   bool leaveOpened = false,
                                                   int initialBufferSize = 512 )
        {
            Throw.CheckNotNullArgument( stream );
            Throw.CheckArgument( "Please use the ReadOnlySquence<byte> on the RecyclableMemoryStream instead.", stream is not RecyclableMemoryStream );
            if( ReadFirstBuffer( stream, out var buffer, out var count, out var initialOffset, initialBufferSize ) )
            {
                r = new Utf8JsonReader( buffer.AsSpan( initialOffset, count - initialOffset ), false, new JsonReaderState( options ) );
            }
            else
            {
                Debug.Assert( initialOffset == 0 );
                count = 0;
                r = new Utf8JsonReader( ReadOnlySpan<byte>.Empty, options );
            }
            return new Utf8JsonStreamReader( stream, buffer, count, initialOffset, leaveOpened );

            static bool ReadFirstBuffer( Stream stream, out byte[] buffer, out int count, out int offset, int initialBufferSize )
            {
                // ArrayPool gives us 16 bytes at least but 4 is the required min size to handle the BOM.
                buffer = ArrayPool<byte>.Shared.Rent( Math.Max( initialBufferSize, 4 ) );
                offset = 0;
                int lenRead = count = stream.Read( buffer );
                if( lenRead == 0 ) return false;
                if( buffer[0] == 0xEF ) // Start of Utf8 BOM that is: 0xEF, 0xBB, 0xBF
                {
                    // If not all the BOM is found, we let the data as-is: the reader will fail.
                    // We must ensure at least one "real" byte: we need at least 4 bytes.
                    while( count < 4 )
                    {
                        lenRead = stream.Read( buffer.AsSpan( count ) );
                        // If we cannot read 4 bytes with a leading BOM, consider it
                        // as an empty data since it is not Json.
                        if( lenRead == 0 ) return false;
                        count += lenRead;
                    }
                    if( buffer[1] == 0xBB && buffer[2] == 0xBF )
                    {
                        offset = 3;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Gets whether this reader is disposed.
        /// </summary>
        public bool IsDisposed => _buffer == null;

        /// <summary>
        /// Gets the whole current buffer.
        /// </summary>
        public ReadOnlySpan<byte> RawBuffer
        {
            get
            {
                Throw.CheckState( !IsDisposed );
                return _buffer;
            }
        }

        /// <summary>
        /// Gets the unread available bytes in the buffer.
        /// This uses the <see cref="Utf8JsonReader.BytesConsumed"/>, the
        /// current count of read bytes in the buffer and ignores the leading
        /// Utf8 Byte Order Mask if any. 
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <returns>The unread available bytes.</returns>
        public ReadOnlySpan<byte> GetUnreadBytes( ref Utf8JsonReader reader )
        {
            Throw.CheckState( !IsDisposed );
            int offset = _initialOffset + (int)reader.BytesConsumed;
            return _buffer.AsSpan( offset, _count - offset );
        }

        /// <summary>
        /// Disposes this reader.
        /// </summary>
        public void Dispose()
        {
            var b = _buffer;
            if( b != null )
            {
                _buffer = null!;
                ArrayPool<byte>.Shared.Return( b, clearArray: true );
                if( !_leaveOpened ) _stream.Dispose();
            }
        }

        /// <inheritdoc />
        public void ReadMoreData( ref Utf8JsonReader reader ) => ReadMoreData( ref reader, false );

        /// <inheritdoc />
        public void SkipMoreData( ref Utf8JsonReader reader )
        {
            // TrySkip returns always true when TokenType is not a PropertyName, a StartObject or StartArray.
            // If we want to be aggressive here we could require that the caller does this check:
            //      Throw.CheckState( reader.TokenType == JsonTokenType.PropertyName || reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray );
            // But this is not how this API has been designed: a Skip/TrySkip requires a subsequent Read() call that does skip the token if it's not one of these.
            if( reader.TokenType == JsonTokenType.PropertyName )
            {
                ReadMoreData( ref reader, false );
            }
            if( reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray )
            {
                if( !reader.TrySkip() ) ReadMoreData( ref reader, true );
            }
        }

        void ReadMoreData( ref Utf8JsonReader reader, bool skip )
        {
            int bytesConsumed = (int)reader.BytesConsumed + _initialOffset;
            // Not needed anymore (only for the BOM of the first buffer).
            _initialOffset = 0;
            // This is for skip handling (if skip parameter is true).
            int initialDepth = reader.CurrentDepth;

            retry:
            if( reader.IsFinalBlock ) return;
            int bytesRead;
            int unread = _count - bytesConsumed;
            if( unread > 0 )
            {
                if( bytesConsumed == 0 )
                {
                    if( _count == _buffer.Length )
                    {
                        if( _buffer.Length == MaxBufferSize )
                        {
                            reader.ThrowJsonException( $"A token requires more than MaxBufferSize = {MaxBufferSize} bytes." );
                        }
                        byte[] newBuffer = ArrayPool<byte>.Shared.Rent( (_buffer.Length < (MaxBufferSize / 2)) ? _buffer.Length * 2 : MaxBufferSize );
                        System.Buffer.BlockCopy( _buffer, bytesConsumed, newBuffer, 0, unread );
                        ArrayPool<byte>.Shared.Return( _buffer, clearArray: true );
                        _buffer = newBuffer;
                    }
                }
                else
                {
                    System.Buffer.BlockCopy( _buffer, bytesConsumed, _buffer, 0, unread );
                }
                bytesRead = _stream.Read( _buffer.AsSpan( unread ) );
                _count = unread + bytesRead;
            }
            else
            {
                _count = bytesRead = _stream.Read( _buffer );
            }
            reader = new Utf8JsonReader( _buffer.AsSpan( 0, _count ), isFinalBlock: bytesRead == 0, reader.CurrentState );
            if( reader.Read()
                && (!skip
                     || initialDepth == reader.CurrentDepth
                     || SkipUntil( ref reader, initialDepth )) )
            {
                // We have read a token and we are not skipping or the skip is over.
                return;
            }
            bytesConsumed = (int)reader.BytesConsumed;
            goto retry;

            static bool SkipUntil( ref Utf8JsonReader reader, int initialDepth )
            {
                do
                {
                    if( !reader.Read() ) return false;
                }
                while( initialDepth != reader.CurrentDepth );
                return true;
            }
        }
    }
}

