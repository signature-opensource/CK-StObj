using CK.Core;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Poco.Exc.Json.Export
{
    public sealed class PocoJsonExporter : IPocoExporter
    {
        public string ProtocolName => "Json";

        public void Write( IActivityMonitor monitor, Stream output, IPoco? data )
        {
        }

        public Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel = default )
        {
            throw new System.NotImplementedException();
        }
    }

}
