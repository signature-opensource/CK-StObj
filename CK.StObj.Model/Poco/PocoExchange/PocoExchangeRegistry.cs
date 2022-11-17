using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core
{
    /// <summary>
    /// Simple registry of available <see cref="IPocoImporter"/> and <see cref="IPocoExporter"/>.
    /// </summary>
    public class PocoExchangeRegistry : ISingletonAutoService
    {
        readonly IPocoImporter[] _importers;
        readonly IPocoExporter[] _exporters;

        /// <summary>
        /// Initializes the registry with available importers and exporters.
        /// Unicity of <see cref="IPocoImporter.ProtocolName"/> (and <see cref="IPocoExporter.ProtocolName"/>)
        /// is checked and an <see cref="InvalidOperationException"/> may be thrown if a duplicated name is found.
        /// </summary>
        /// <param name="importers">The set of importers.</param>
        /// <param name="exporters">The set of exporters.</param>
        public PocoExchangeRegistry( IEnumerable<IPocoImporter> importers, IEnumerable<IPocoExporter> exporters )
        {
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
        }

        /// <summary>
        /// Gets the available importers.
        /// </summary>
        public IReadOnlyList<IPocoImporter> Importers => _importers;

        /// <summary>
        /// Finds an importer by its <see cref="IPocoImporter.ProtocolName"/>
        /// or throws an <see cref="InvalidOperationException"/> if it doesn't exist.
        /// </summary>
        /// <param name="protocolName">The protocol name.</param>
        /// <returns>The importer.</returns>
        public IPocoImporter FindImporter( string protocolName )
        {
            var o = _importers.FirstOrDefault( i => i.ProtocolName == protocolName );
            if( o == null ) Throw.InvalidOperationException( $"Unable to find IPocoImporter with ProtocolName '{protocolName}'." );
            return o;
        }

        /// <summary>
        /// Gets the available exporters.
        /// </summary>
        public IReadOnlyList<IPocoExporter> Exporters => _exporters;

        /// <summary>
        /// Finds an exporter by its <see cref="IPocoExporter.ProtocolName"/>
        /// or throws an <see cref="InvalidOperationException"/> if it doesn't exist.
        /// </summary>
        /// <param name="protocolName">The protocol name.</param>
        /// <returns>The exporter.</returns>
        public IPocoExporter FindExporter( string protocolName )
        {
            var o = _exporters.FirstOrDefault( i => i.ProtocolName == protocolName );
            if( o == null ) Throw.InvalidOperationException( $"Unable to find IPocoExporter with ProtocolName '{protocolName}'." );
            return o;
        }
    }
}
