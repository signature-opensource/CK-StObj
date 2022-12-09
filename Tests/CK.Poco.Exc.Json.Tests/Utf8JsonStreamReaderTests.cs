using CK.Core;
using CK.Poco.Exc.Json.Tests.CrisLike;
using CommunityToolkit.HighPerformance.Buffers;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Newtonsoft.Json.Bson;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace CK.Poco.Exc.Json.Tests
{
    public partial class Utf8JsonStreamReaderTests
    {
        [Test]
        public void how_Utf8JsonReader_and_JsonReadState_work()
        {
            var jsonString = @"{""Date"":""2019-08-01T00:00:00-07:00"",""Temperature"":25}";

            byte[] bytes = Encoding.UTF8.GetBytes( jsonString );
            var stream = new MemoryStream( bytes );

            // Lets's start with a buffer that will only be able to handle the
            // "Date" property name.
            var buffer = new byte[10];

            // Fill the buffer with the 10 first bytes.
            stream.Read( buffer );

            Encoding.UTF8.GetString( buffer ).Should().Be( "{\"Date\":\"2" );
            // We set isFinalBlock to false since we expect more data in a subsequent read from the stream.
            var reader = new Utf8JsonReader( buffer, isFinalBlock: false, state: default );

            reader.TokenType.Should().Be( JsonTokenType.None, "We have not read anything yet." );
            reader.Read().Should().BeTrue();
            reader.TokenType.Should().Be( JsonTokenType.StartObject, "We have read the StartObject." );
            reader.Read().Should().BeTrue();
            reader.TokenType.Should().Be( JsonTokenType.PropertyName );
            reader.GetString().Should().Be( "Date", "The Property name Date has been successfully read." );

            reader.BytesConsumed.Should().Be( 8 );
            reader.Read().Should().BeFalse( "Now Read() cannot read the next token." );
            ReadMore( stream, ref buffer, ref reader );
            Encoding.UTF8.GetString( buffer ).Should().Be( "\"2019-08-0" );
            reader.Read().Should().BeFalse( "This buffer is too small: ReadMore will acquire a bigger buffer..." );
            reader.BytesConsumed.Should().Be( 0 );
            ReadMore( stream, ref buffer, ref reader );
            reader.Read().Should().BeFalse( "This buffer is still too small..." );
            ReadMore( stream, ref buffer, ref reader );
            reader.Read().Should().BeTrue( "Got it." );

            static void ReadMore( Stream stream, ref byte[] buffer, ref Utf8JsonReader reader )
            {
                int bytesRead;
                if( reader.BytesConsumed == 0 )
                {
                    int previousLength = buffer.Length;
                    Array.Resize( ref buffer, previousLength * 2 );
                    bytesRead = stream.Read( buffer.AsSpan( previousLength ) );
                }
                else
                {
                    int unread = buffer.Length - (int)reader.BytesConsumed;
                    if( unread > 0 )
                    {
                        ReadOnlySpan<byte> leftover = buffer.AsSpan( (int)reader.BytesConsumed );
                        leftover.CopyTo( buffer );
                        bytesRead = stream.Read( buffer.AsSpan( leftover.Length ) );
                    }
                    else
                    {
                        bytesRead = stream.Read( buffer );
                    }
                }
                reader = new Utf8JsonReader( buffer, isFinalBlock: bytesRead == 0, reader.CurrentState );
            }

        }

        const string sampleJson = @"
{
    ""Date"": ""2019-08-01T00:00:00-07:00"",
    ""Temperature"": 25,
    ""Measures"": [ ""One"", ""Two"", ""Three"" ],
    ""Objects"":
        {
            ""F1"": {},
            ""F2"":[],
            ""F3"":
                [
                    ""Hip!"",
                    {},
                    {
                        ""Hop"": true
                    }
                ]
        },
    ""Done"": true
}";

        bool ReadDoneProperty( Utf8JsonStreamReader context, ref Utf8JsonReader reader )
        {
            bool result;
            if( !reader.Read() ) context.NeedMoreData( ref reader );
            reader.TokenType.Should().Be( JsonTokenType.StartObject );
            if( !reader.Read() ) context.NeedMoreData( ref reader );
            for(; ; )
            {
                reader.TokenType.Should().Be( JsonTokenType.PropertyName );
                if( reader.ValueTextEquals( "Done" ) )
                {
                    if( !reader.Read() ) context.NeedMoreData( ref reader );
                    result = reader.GetBoolean();
                    break;
                }
                // Eats the property name.
                if( !reader.Read() ) context.NeedMoreData( ref reader );
                // Skip the property value.
                if( reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.StartObject )
                {
                    if( !reader.TrySkip() ) context.NeedMoreData( ref reader, true );
                    Debug.Assert( reader.TokenType == JsonTokenType.EndArray || reader.TokenType == JsonTokenType.EndObject,
                                    "This is why we need a subsequent Read." );
                }
                if( !reader.Read() ) context.NeedMoreData( ref reader );
            }
            if( !reader.Read() ) context.NeedMoreData( ref reader );
            reader.TokenType.Should().Be( JsonTokenType.EndObject );
            return result;
        }

        static SlicedStream CreateDataStream( SlicedStream.ReadMode mode, string data )
        {
            return new SlicedStream( new MemoryStream( Encoding.UTF8.GetBytes( data ) ), mode );
        }

        [TestCase( SlicedStream.ReadMode.OneByte )]
        [TestCase( SlicedStream.ReadMode.TwoBytes )]
        [TestCase( SlicedStream.ReadMode.Full )]
        [TestCase( SlicedStream.ReadMode.Random )]
        public void worst_case_read( SlicedStream.ReadMode mode )
        {
            // The ArrayPool<byte> gives us 16 bytes min.
            Utf8JsonStreamReader.InitialBufferSize = 16;

            // OneByte triggers a lot of calls to NeedMoreData. 
            using SlicedStream stream = CreateDataStream( mode, sampleJson );
            using var sr = Utf8JsonStreamReader.Create( stream, default, out var reader );

            ReadDoneProperty( sr, ref reader ).Should().BeTrue();

            // We cannot read more.
            reader.Read().Should().BeFalse();
            // But calling for more data can always be safely done.
            if( !reader.Read() ) sr.NeedMoreData( ref reader );
        }

        [TestCase( SlicedStream.ReadMode.OneByte )]
        [TestCase( SlicedStream.ReadMode.TwoBytes )]
        [TestCase( SlicedStream.ReadMode.Full )]
        [TestCase( SlicedStream.ReadMode.Random )]
        public void worst_case_read_with_Utf8_BOM( SlicedStream.ReadMode mode )
        {
            var Utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] data = Utf8Bom.Concat( Encoding.UTF8.GetBytes( sampleJson ) ).ToArray();

            // The ArrayPool<byte> gives us 16 bytes min.
            Utf8JsonStreamReader.InitialBufferSize = 16;

            using SlicedStream stream = new SlicedStream( new MemoryStream( data ), mode );
            using var sr = Utf8JsonStreamReader.Create( stream, default, out var reader );

            ReadDoneProperty( sr, ref reader ).Should().BeTrue();

            // We cannot read more.
            reader.Read().Should().BeFalse();
            // But calling for more data can always be safely done.
            if( !reader.Read() ) sr.NeedMoreData( ref reader );
        }

        [TestCase( SlicedStream.ReadMode.OneByte )]
        [TestCase( SlicedStream.ReadMode.TwoBytes )]
        [TestCase( SlicedStream.ReadMode.Full )]
        [TestCase( SlicedStream.ReadMode.Random )]
        public void read_buffer_GetUnreadBytes( SlicedStream.ReadMode mode )
        {
            var data = sampleJson + "More bytes after...";
            using SlicedStream stream = CreateDataStream( mode, data );
            using var sr = Utf8JsonStreamReader.Create( stream, default, out var reader );

            ReadDoneProperty( sr, ref reader ).Should().BeTrue();

            // We are on the closing brace.
            reader.TokenType.Should().Be( JsonTokenType.EndObject );
            // If we try to Read() more, this is an exception.
            try
            {
                if( !reader.Read() ) sr.NeedMoreData( ref reader );
                Debug.Fail( "This is not Json after!" );
            }
            catch( JsonException ) { }

            // The GetUnreadBytes can be called as long as the Utf8JsonStreamReader is not disposed.
            // Depending on the length read, the buffer can be small.
            "More bytes after...".Should().StartWith( Encoding.UTF8.GetString( sr.GetUnreadBytes( ref reader ) ) );
        }



    }
}
