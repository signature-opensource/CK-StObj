using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.StObj.Engine.Tests
{
    public static class JsonTestHelper
    {
        /// <summary>
        /// Serializes the Poco in UTF-8 Json.
        /// </summary>
        /// <param name="o">The poco.</param>
        /// <param name="withType">True to emit types.</param>
        /// <returns>The bytes.</returns>
        public static ReadOnlyMemory<byte> Serialize( IPoco o, bool withType, PocoJsonSerializerOptions? options = null )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                o.Write( w, withType, options: options );
                w.Flush();
            }
            return m.WrittenMemory;
        }

        public static T? Deserialize<T>( IServiceProvider services, ReadOnlySpan<byte> b, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( b );
            var f = services.GetRequiredService<IPocoFactory<T>>();
            return f.Read( ref r, options );
        }

        public static T? Deserialize<T>( IServiceProvider services, string s, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            return Deserialize<T>( services, Encoding.UTF8.GetBytes( s ), options );
        }

        /// <summary>
        /// Tests a round trip to and from Json with type: the serialized form contains the serialized types.
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="directory">The Poco directory.</param>
        /// <param name="o">The Poco to serialize/deserialize.</param>
        /// <param name="text">Optional Json text hook called on success.</param>
        /// <returns>The deserialized Poco.</returns>
        [return: NotNullIfNotNull( "o" )]
        public static T? Roundtrip<T>( PocoDirectory directory, T? o, PocoJsonSerializerOptions? options = null, Action<string>? text = null ) where T : class, IPoco
        {
            byte[] bin1;
            string bin1Text;
            using( var m = new MemoryStream() )
            {
                try
                {
                    using( var w = new Utf8JsonWriter( m ) )
                    {
                        o.Write( w, true, options );
                        w.Flush();
                    }
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                    text?.Invoke( bin1Text );
                }
                catch( Exception )
                {
                    // On error, bin1 and bin1Text can be inspected here.
                    throw;
                }

                var r1 = new Utf8JsonReader( bin1 );

                var o2 = directory.ReadPocoValue( ref r1, options );

                m.Position = 0;
                using( var w2 = new Utf8JsonWriter( m ) )
                {
                    o2.Write( w2, true, options );
                    w2.Flush();
                }
                var bin2 = m.ToArray();

                bin1.Should().BeEquivalentTo( bin2 );

                return (T?)o2;
            }
        }
    }
}
