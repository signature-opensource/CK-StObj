using CK.Core;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Poco.PE.Json
{
    public sealed class PocoJsonImporter : IPocoImporter
    {
        public string ProtocolName => "Json";

        public bool TryRead( IActivityMonitor monitor, Stream input, out IPoco? data )
        {
            throw new System.NotImplementedException();
        }

        public Task<(bool Success, IPoco? Data)> TryReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel )
        {
            throw new System.NotImplementedException();
        }
    }

}
