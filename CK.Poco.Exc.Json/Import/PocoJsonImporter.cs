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

        /// <inheritdoc />
        public bool TryRead( IActivityMonitor monitor, Stream input, out IPoco? data )
        {
            try
            {
                data = _pocoDirectory.JsonDeserialize( input, PocoJsonImportOptions.Default );
                return true;
            }
            catch( Exception ex )
            {
                monitor.Error( $"While trying to read a Poco in Json.", ex );
                data = null;
                return false;
            }
        }

        /// <inheritdoc />
        public Task<(bool Success, IPoco? Data)> TryReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel )
        {
            if( TryRead(monitor,input,out var data) )
            {
                return Task.FromResult( (true, data) );
            }
            return Task.FromResult( (false, (IPoco?)null) );
        }

        /// <inheritdoc />
        public IPoco? Read( IActivityMonitor monitor, Stream input )
        {
            return _pocoDirectory.JsonDeserialize( input, PocoJsonImportOptions.Default );
        }

        /// <inheritdoc />
        public Task<IPoco?> ReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel )
        {
            return Task.FromResult( _pocoDirectory.JsonDeserialize( input, PocoJsonImportOptions.Default ) );
        }
    }

}
