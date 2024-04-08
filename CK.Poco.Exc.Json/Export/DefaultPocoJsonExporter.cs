using CK.Core;
using System;
using System.Buffers;
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
    public sealed class DefaultPocoJsonExporter : IPocoExporter
    {
        readonly PocoDirectory _pocoDirectory;

        /// <summary>
        /// Initializes a new standard "Json" exporter.
        /// </summary>
        /// <param name="pocoDirectory">The Poco directory.</param>
        public DefaultPocoJsonExporter( PocoDirectory pocoDirectory )
        {
            _pocoDirectory = pocoDirectory;
        }

        /// <summary>
        /// Returns "Json".
        /// </summary>
        public string ProtocolName => "Json";

        /// <summary>
        /// Writes a poco in <see cref="PocoJsonExportOptions.Default"/> format.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="output">The target output.</param>
        /// <param name="data">The data to write.</param>
        public void Write( IActivityMonitor monitor, Stream output, IPoco? data )
        {
            if( !_pocoDirectory.WriteJson( output, data, true, PocoJsonExportOptions.Default ) )
            {
                Throw.InvalidOperationException( $"Poco type '{((IPocoGeneratedClass)data!).Factory.Name}' is not Exchangeable." );
            }
        }

        /// <summary>
        /// Writes a poco in <see cref="PocoJsonExportOptions.Default"/> format.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="output">The target output.</param>
        /// <param name="data">The data to write.</param>
        public void Write( IActivityMonitor monitor, IBufferWriter<byte> output, IPoco? data )
        {
            if( !_pocoDirectory.WriteJson( output, data, true, PocoJsonExportOptions.Default ) )
            {
                Throw.InvalidOperationException( $"Poco type '{((IPocoGeneratedClass)data!).Factory.Name}' is not Exchangeable." );
            }
        }

        /// <summary>
        /// Asynchronously writes a poco in <see cref="PocoJsonExportOptions.Default"/> format.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="output">The target output.</param>
        /// <param name="data">The data to write.</param>
        /// <param name="cancel">Optional cancellation token.</param>
        public Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel = default )
        {
            if( !_pocoDirectory.WriteJson( output, data, true, PocoJsonExportOptions.Default ) )
            {
                return Task.FromException( new InvalidOperationException( $"Poco type '{((IPocoGeneratedClass)data!).Factory.Name}' is not Exchangeable." ) );
            }
            return Task.CompletedTask;
        }
    }

}
