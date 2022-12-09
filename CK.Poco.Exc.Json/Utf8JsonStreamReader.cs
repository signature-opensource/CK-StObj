using Microsoft.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Provides a reading buffer on a Utf8 stream of bytes that may start with
    /// the Utf8 Byte Order Mask (BOM: 0xEF, 0xBB, 0xBF).
    /// <para>
    /// The buffer comes from the <see cref="ArrayPool{T}.Shared"/> of bytes: this
    /// reader MUST be disposed to return the current buffer to the pool.
    /// </para>
    /// <para>
    /// The pattern to use it is to replace all <c>Read()</c> calls with:
    /// <code>
    /// if( !reader.Read() ) streamReader.NeedMoreData( ref reader );
    /// </code>
    /// And all <c>Skip()</c> calls with:
    /// <code>
    /// if( !reader.TrySkip() ) streamReader.NeedMoreData( ref reader, true );
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
        public static int MaxBufferSize = int.MaxValue;
        public static int InitialBufferSize = 256;
#else
            const int MaxBufferSize = int.MaxValue;
            const int InitialBufferSize = 256;
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
        /// <returns>A new stream reader.</returns>
        public static Utf8JsonStreamReader Create( Stream stream, JsonReaderOptions options, out Utf8JsonReader r, bool leaveOpened = false )
        {
            Throw.CheckNotNullArgument( stream );
            Throw.CheckArgument( stream is not RecyclableMemoryStream, "Please use the ReadOnlySquence<byte> on the RecyclableMemoryStream instead." );
            if( ReadFirstBuffer( stream, out var buffer, out var count, out var initialOffset ) )
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

            static bool ReadFirstBuffer( Stream stream, out byte[] buffer, out int count, out int offset )
            {
                buffer = ArrayPool<byte>.Shared.Rent( InitialBufferSize );
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
        /// This uses the <see cref="Utf8JsonReader.BytesConsumed"/> and the
        /// current count of read bytes in the buffer. 
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
        public void NeedMoreData( ref Utf8JsonReader reader, bool skip = false )
        {
            int bytesConsumed = (int)reader.BytesConsumed + _initialOffset;
            // Not needed anymore (only for the BOM of the first buffer).
            _initialOffset = 0;
            // This is for skip handling (if skip parameter is true).
            int skipTargetDepth = reader.CurrentDepth;

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
                     || skipTargetDepth == reader.CurrentDepth
                     || SkipUntil( ref reader, skipTargetDepth )) )
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

