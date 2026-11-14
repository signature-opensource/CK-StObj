using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.BinarySerialization.Poco.Tests;

/// <summary>
/// Test for partial read handling in POCO deserialization.
/// <para>
/// In .NET 6+, GZipStream.Read() may return fewer bytes than requested.
/// The POCO deserializer must handle this by looping until all bytes are read.
/// </para>
/// <para>
/// See: https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/partial-byte-reads-in-streams
/// </para>
/// </summary>
[TestFixture]
public class PartialPocoDeserializationTests
{
    public interface ITestPoco : IPoco
    {
        [DefaultValue( "" )]
        string Data { get; set; }
    }

    /// <summary>
    /// Tests that POCO deserialization correctly handles streams that return partial data
    /// (as GZipStream does in .NET 6+).
    /// </summary>
    [Test]
    public async Task Poco_deserialization_must_handle_partial_stream_reads_Async()
    {
        var engineConfiguration = TestHelper.CreateDefaultEngineConfiguration();
        engineConfiguration.FirstBinPath.Types.Add( typeof( ITestPoco ),
                                                    typeof( CommonPocoJsonSupport ),
                                                    typeof( PocoDirectory ) );
        await using var auto = (await engineConfiguration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();
        var poco = directory.Create<ITestPoco>( p => p.Data = new string( 'A', 200 ) );

        var sC = new BinarySerializerContext( BinarySerializer.DefaultSharedContext, auto.Services );

        // Serialize the POCO.
        using var memory = new MemoryStream();
        using( var serializer = BinarySerializer.Create( memory, sC ) )
        {
            serializer.WriteObject( poco );
        }
        memory.Position = 0;

        // Pollute ArrayPool to prevent buffer reuse from masking partial read bugs.
        // Rent buffers of various sizes, fill with garbage, and return them.
        for( int size = 16; size <= 4096; size *= 2 )
        {
            var buf = ArrayPool<byte>.Shared.Rent( size );
            Array.Fill( buf, (byte)'X' );
            ArrayPool<byte>.Shared.Return( buf );
        }

        var dC = new BinaryDeserializerContext( BinaryDeserializer.DefaultSharedContext, auto.Services );

        // Wrap in SlicedStream that returns max 10 bytes per read (simulating GZipStream partial reads).
        using var sliced = new SlicedStream( memory, maxBytesPerRead: 10 );

        // Deserialize - this must succeed even with partial reads.
        var result = BinaryDeserializer.Deserialize( sliced, dC, d => d.ReadObject<ITestPoco>() );

        result.IsValid.ShouldBeTrue( "Deserialization must succeed with partial reads" );
        var deserialized = result.GetResult();
        deserialized.ShouldNotBeNull();
        deserialized.Data.ShouldBe( poco.Data );
    }

    /// <summary>
    /// Stream wrapper that limits bytes returned per Read call, simulating GZipStream behavior in .NET 6+.
    /// The stream is seekable (unlike GZipStream) to simplify testing.
    /// </summary>
    sealed class SlicedStream : Stream
    {
        readonly Stream _inner;
        readonly int _maxBytesPerRead;

        public SlicedStream( Stream inner, int maxBytesPerRead )
        {
            _inner = inner;
            _maxBytesPerRead = maxBytesPerRead;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read( byte[] buffer, int offset, int count )
        {
            // Limit the number of bytes read per call to simulate GZipStream behavior.
            int toRead = Math.Min( count, _maxBytesPerRead );
            return _inner.Read( buffer, offset, toRead );
        }

        public override long Seek( long offset, SeekOrigin origin ) => _inner.Seek( offset, origin );
        public override void SetLength( long value ) => _inner.SetLength( value );
        public override void Write( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
        public override void Flush() => _inner.Flush();

        protected override void Dispose( bool disposing )
        {
            if( disposing ) _inner.Dispose();
            base.Dispose( disposing );
        }
    }
}
