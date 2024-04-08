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
        /// <returns>The deserialized Poco. Null if <see cref="PocoJsonExportOptions.TypeFilterName"/> rejected the Poco.</returns>
        [return: NotNullIfNotNull( nameof( o ) )]
        public static T? Roundtrip<T>( PocoDirectory directory,
                                       T? o,
                                       PocoJsonExportOptions? exportOptions = null,
                                       PocoJsonImportOptions? importOptions = null,
                                       Action<string>? text = null )
            where T : class, IPoco
        {
            using( var recyclableStream = Util.RecyclableStreamManager.GetStream() )
            {
                DoRoundTrip( directory, o, exportOptions, importOptions, text, recyclableStream );
            }
            using( var memoryStream = new MemoryStream() )
            {
                return DoRoundTrip( directory, o, exportOptions, importOptions, text, memoryStream );
            }

            static T? DoRoundTrip( PocoDirectory directory,
                                   T? o,
                                   PocoJsonExportOptions? exportOptions,
                                   PocoJsonImportOptions? importOptions,
                                   Action<string>? text,
                                   MemoryStream m )
            {
                byte[] bin1;
                string bin1Text;
                try
                {
                    directory.WriteJson( m, o, true, exportOptions );
                    m.Flush();
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                    text?.Invoke( bin1Text );
                    if( bin1.Length == 0 )
                    {
                        // Filtered out by the exportOptions.TypeFilterName.
                        return null;
                    }
                }
                catch( Exception )
                {
                    m.Flush();
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                    // On error, bin1 and bin1Text can be inspected here.
                    throw;
                }

                var o2 = directory.ReadJson( bin1, importOptions );
                m.Position = 0;
                directory.WriteJson( m, o, true, exportOptions );
                var bin2 = m.ToArray();
                bin2.Should().BeEquivalentTo( bin1 );

                // Check the extension method on IPocoFactory.ReadJson and IPoco.WriteJson.
                var o3 = directory.Find<T>()!.ReadJson( bin2, importOptions );
                Throw.DebugAssert( (o3 == null) == (o == null) );
                if( o3 != null )
                {
                    m.Position = 0;
                    o3.WriteJson( m, true, exportOptions ).Should().BeTrue();
                    var bin3 = m.ToArray();
                    bin3.Should().BeEquivalentTo( bin1 );
                }
                return (T?)o3;
            }
        }
    }
}
