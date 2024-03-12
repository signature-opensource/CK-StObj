using CK.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Poco.Exc.Json
{
    /// <summary>
    /// Singleton auto service IPoco exporter with <see cref="PocoJsonExportOptions.Default"/>.
    /// Its <see cref="ProtocolName"/> is "Json".
    /// </summary>
    public sealed class PocoJsonExporter : IPocoExporter
    {
        /// <summary>
        /// Returns "Json".
        /// </summary>
        public string ProtocolName => "Json";

        public void Write( IActivityMonitor monitor, Stream output, IPoco? data )
        {
            using Utf8JsonWriter w = new Utf8JsonWriter( output );
            if( !data.WriteJson( w, true, PocoJsonExportOptions.Default ) )
            {
                Throw.InvalidOperationException( $"Poco type '{((IPocoGeneratedClass)data!).Factory.Name}' is not Exchangeable." );
            }
        }

        public Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel = default )
        {
            using Utf8JsonWriter w = new Utf8JsonWriter( output );
            if( !data.WriteJson( w, true, PocoJsonExportOptions.Default ) )
            {
                return Task.FromException( new InvalidOperationException( $"Poco type '{((IPocoGeneratedClass)data!).Factory.Name}' is not Exchangeable." ) );
            }
            return Task.CompletedTask;
        }
    }

}
