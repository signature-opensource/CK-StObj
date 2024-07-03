using CK.Setup;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Simple registry of available <see cref="IPocoImporter"/> and <see cref="IPocoExporter"/>
    /// and resolver of such importers/exporters thanks to available <see cref="IPocoImporterFactory"/>
    /// and <see cref="IPocoImporterFactory"/>.
    /// </summary>
    /// <remarks>
    /// Registration of this service triggers the availability of the engine Poco serialization service
    /// that exposes the standard names for the set of all serializable poco types.
    /// </remarks>
    [ContextBoundDelegation( "CK.Setup.PocoSerializableServiceEngineImpl, CK.Poco.Exchange.Engine" )]
    public class PocoExchangeService : ISingletonAutoService
    {
        readonly IPocoDirectoryExchangeGenerated _pocoDirectory;
        readonly IPocoImporter[] _importers;
        readonly IPocoExporter[] _exporters;
        readonly IPocoImporterFactory[] _importerFactories;
        readonly IPocoExporterFactory[] _exporterFactories;

        /// <summary>
        /// Initializes the registry with available importers and exporters.
        /// Unicity of <see cref="IPocoImporter.ProtocolName"/> (and <see cref="IPocoExporter.ProtocolName"/>)
        /// is checked and an <see cref="InvalidOperationException"/> may be thrown if a duplicated name is found.
        /// </summary>
        /// <param name="pocoDirectory">The poco directory.</param>
        /// <param name="importers">The set of available importers.</param>
        /// <param name="exporters">The set of available exporters.</param>
        /// <param name="importerFactories">The set of available importer factories.</param>
        /// <param name="exporterFactories">The set of available exporter factories.</param>
        public PocoExchangeService( PocoDirectory pocoDirectory,
                                    IEnumerable<IPocoImporter> importers,
                                    IEnumerable<IPocoExporter> exporters,
                                    IEnumerable<IPocoImporterFactory> importerFactories,
                                    IEnumerable<IPocoExporterFactory> exporterFactories )
        {
            _pocoDirectory = (IPocoDirectoryExchangeGenerated)pocoDirectory;
            _importers = importers.ToArray();
            var dupI = _importers.GroupBy( i => i.ProtocolName ).Where( g => g.Count() > 1 ).ToList();
            if( dupI.Count > 0 )
            {
                var dups = dupI.Select( g => $"'{g.Key}' by '{g.Select( i => i.GetType().ToCSharpName() ).Concatenate( "', '" )}'" );
                Throw.InvalidOperationException( $"More than one PocoImporter share the same ProtocolName: {dups.Concatenate( " and " )}" );
            }
            _exporters = exporters.ToArray();
            var dupE = _exporters.GroupBy( i => i.ProtocolName ).Where( g => g.Count() > 1 ).ToList();
            if( dupE.Count > 0 )
            {
                var dups = dupE.Select( g => $"'{g.Key}' by '{g.Select( i => i.GetType().ToCSharpName() ).Concatenate( "', '" )}'" );
                Throw.InvalidOperationException( $"More than one PocoExporter share the same ProtocolName: {dups.Concatenate( " and " )}" );
            }
            _importerFactories = importerFactories.ToArray();
            _exporterFactories = exporterFactories.ToArray();
        }

        /// <summary>
        /// Gets the available <see cref="ExchangeableRuntimeFilter"/>.
        /// </summary>
        public IReadOnlyCollection<ExchangeableRuntimeFilter> RuntimeFilters => _pocoDirectory.RuntimeFilters;

        /// <summary>
        /// Gets the available singleton importers.
        /// </summary>
        public IReadOnlyList<IPocoImporter> RegularImporters => _importers;

        /// <summary>
        /// Finds a singleton importer by its <see cref="IPocoImporter.ProtocolName"/>
        /// or throws an <see cref="InvalidOperationException"/> if it doesn't exist.
        /// </summary>
        /// <param name="protocolName">The protocol name.</param>
        /// <returns>The importer.</returns>
        public IPocoImporter FindAvailableImporter( string protocolName )
        {
            var o = _importers.FirstOrDefault( i => i.ProtocolName == protocolName );
            if( o == null ) Throw.InvalidOperationException( $"Unable to find IPocoImporter with ProtocolName '{protocolName}'." );
            return o;
        }

        /// <summary>
        /// Tries to resolve an importer based on its protocol name.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="protocol">The protocol to resolve.</param>
        /// <returns>The importer to use or null.</returns>
        public IPocoImporter? TryResolveImporter( IActivityMonitor monitor, string protocol )
        {
            if( PocoExtendedProtocolName.TryParse( protocol, out var n ) )
            {
                foreach( var f in _importerFactories )
                {
                    if( f.BaseProtocolName == n.BaseName )
                    {
                        var o = f.TryCreate( monitor, n );
                        if( o != null ) return o;
                    }
                }
                monitor.Error( $"Unable to resolve an importer for protocol '{protocol}'." );
            }
            else
            {
                var o = _importers.FirstOrDefault( i => i.ProtocolName == protocol );
                if( o != null ) return o;
                monitor.Error( $"Unable to find IPocoImporter with ProtocolName '{protocol}'." );
            }
            return null;
        }

        /// <summary>
        /// Gets the available singleton exporters.
        /// </summary>
        public IReadOnlyList<IPocoExporter> RegularExporters => _exporters;

        /// <summary>
        /// Finds a singleton exporter by its <see cref="IPocoExporter.ProtocolName"/>
        /// or throws an <see cref="InvalidOperationException"/> if it doesn't exist.
        /// </summary>
        /// <param name="protocolName">The protocol name.</param>
        /// <returns>The exporter.</returns>
        public IPocoExporter FindRegularExporter( string protocolName )
        {
            var o = _exporters.FirstOrDefault( i => i.ProtocolName == protocolName );
            if( o == null ) Throw.InvalidOperationException( $"Unable to find IPocoExporter with ProtocolName '{protocolName}'." );
            return o;
        }

        /// <summary>
        /// Tries to resolve an exporter based on its protocol name.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="protocol">The protocol to resolve.</param>
        /// <returns>The importer to use or null.</returns>
        public IPocoExporter? TryResolveExporter( IActivityMonitor monitor, string protocol )
        {
            if( PocoExtendedProtocolName.TryParse( protocol, out var n ) )
            {
                foreach( var f in _exporterFactories )
                {
                    if( f.BaseProtocolName == n.BaseName )
                    {
                        var o = f.TryCreate( monitor, n );
                        if( o != null ) return o;
                    }
                }
                monitor.Error( $"Unable to resolve an exporter for protocol '{protocol}'." );
            }
            else
            {
                var o = _exporters.FirstOrDefault( i => i.ProtocolName == protocol );
                if( o != null ) return o;
                monitor.Error( $"Unable to find IPocoExporter with ProtocolName '{protocol}'." );
            }
            return null;
        }
    }
}
