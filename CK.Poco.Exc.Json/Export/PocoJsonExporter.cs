using CK.Core;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Poco.Exc.Json
{
    public sealed class PocoJsonExporter : IPocoExporter
    {
        public string ProtocolName => "Json";

        public void Write( IActivityMonitor monitor, Stream output, IPoco? data )
        {
            using Utf8JsonWriter w = new Utf8JsonWriter( output );
            using var wCtx = new PocoJsonWriteContext();
            data.WriteJson( w, wCtx, true );
        }

        public Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel = default )
        {
            Write( monitor, output, data );
            return Task.CompletedTask;
        }
    }

}
