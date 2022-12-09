using CK.Core;
using Microsoft.IO;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Poco.Exc.Json
{
    public sealed class PocoJsonImporter : IPocoImporter
    {
        readonly PocoDirectory _pocoDirectory;

        public string ProtocolName => "Json";

        public PocoJsonImporter( PocoDirectory pocoDirectory )
        {
            _pocoDirectory = pocoDirectory;
        }

        public bool TryRead( IActivityMonitor monitor, Stream input, out IPoco? data )
        {
            throw new NotImplementedException();
        }

        public Task<(bool Success, IPoco? Data)> TryReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel )
        {
            throw new System.NotImplementedException();
        }
    }

}
