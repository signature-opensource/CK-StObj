using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using CK.CodeGen;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates the result of the <see cref="StObjCollector"/> work.
    /// </summary>
    public partial class StObjCollectorResult
    {
        readonly DynamicAssembly _tempAssembly;
        readonly BuildValueCollector? _valueCollector;
        readonly EndpointResult? _endpointResult;

        internal StObjCollectorResult( CKTypeCollectorResult typeResult,
                                       DynamicAssembly tempAssembly,
                                       EndpointResult? e,
                                       BuildValueCollector? valueCollector )
        {
            Debug.Assert( !typeResult.HasFatalError || valueCollector == null, "typeResult.HasFatalError ==> valueCollector == null (i.e. valueCollector != null ==> !typeResult.HasFatalError)" );
            CKTypeResult = typeResult;
            _tempAssembly = tempAssembly;
            _valueCollector = valueCollector;
            _endpointResult = e;
            if( valueCollector != null )
            {
                EngineMap = typeResult.RealObjects.EngineMap;
            }
        }

        /// <summary>
        /// True if a fatal error occurred. Result should be discarded.
        /// </summary>
        public bool HasFatalError => _valueCollector == null || _endpointResult == null;

        /// <summary>
        /// Gets the result of the types discovery and analysis.
        /// This exposes the <see cref="CKTypeCollectorResult.PocoDirectory"/> and <see cref="CKTypeCollectorResult.PocoTypeSystem"/>.
        /// </summary>
        public CKTypeCollectorResult CKTypeResult { get; }

        /// <summary>
        /// Gets the endpoint results if no error occurred during endpoint discovery and analysis,
        /// null otherwise.
        /// </summary>
        public EndpointResult? EndpointResult => _endpointResult;

        /// <summary>
        /// Gets the final <see cref="IStObjEngineMap"/> if <see cref="HasFatalError"/> is false.
        /// </summary>
        public IStObjEngineMap? EngineMap { get; }

        /// <summary>
        /// Gets the dynamic assembly for this context.
        /// </summary>
        public IDynamicAssembly DynamicAssembly => _tempAssembly;

    }
}
