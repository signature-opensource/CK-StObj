using CK.Core;
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
            data.WriteJson( w, true, PocoJsonExportOptions.Default );
        }

        public Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel = default )
        {
            Write( monitor, output, data );
            return Task.CompletedTask;
        }
    }

}
