using CK.Core;
using CK.Poco.Exc.Json;
using FluentAssertions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Poco.Exc.Json.Tests
{
    public static class JsonTestHelper
    {
        /// <summary>
        /// Tests a round trip to and from Json with type: the serialized form contains the serialized types.
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="directory">The Poco directory.</param>
        /// <param name="o">The Poco to serialize/deserialize.</param>
        /// <param name="exportOptions">The export options to use.</param>
        /// <param name="importOptions">The import options to use.</param>
        /// <param name="text">Optional Json text hook called on success.</param>
        /// <returns>The deserialized Poco.</returns>
        [return: NotNullIfNotNull( nameof( o ) )]
        public static T? Roundtrip<T>( PocoDirectory directory,
                                       T? o,
                                       PocoJsonExportOptions? exportOptions = null,
                                       PocoJsonImportOptions? importOptions = null,
                                       Action<string>? text = null )
            where T : class, IPoco
        {
            byte[] bin1;
            string bin1Text;
            using( var m = Util.RecyclableStreamManager.GetStream() )
            {
                using Utf8JsonWriter w = new Utf8JsonWriter( m );
                try
                {
                    o.WriteJson( w, true, exportOptions );
                    w.Flush();
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                    text?.Invoke( bin1Text );
                }
                catch( Exception )
                {
                    w.Flush();
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                    // On error, bin1 and bin1Text can be inspected here.
                    throw;
                }

                var o2 = directory.ReadJson( bin1, importOptions );

                m.Position = 0;
                using( var w2 = new Utf8JsonWriter( m ) )
                {
                    o2.WriteJson( w2, true, exportOptions );
                    w2.Flush();
                }
                var bin2 = m.ToArray();

                bin1.Should().BeEquivalentTo( bin2 );
                return (T?)o2;
            }
        }
    }
}
