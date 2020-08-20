#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\StObjCollectorResult.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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

        internal StObjCollectorResult(
            CKTypeCollectorResult typeResult,
            DynamicAssembly tempAssembly,
            BuildValueCollector? valueCollector )
        {
            Debug.Assert( !typeResult.HasFatalError || valueCollector == null, "typeResult.HasFatalError ==> valueCollector == null (ie. valueCollector != null ==> !typeResult.HasFatalError)" );
            CKTypeResult = typeResult;
            _tempAssembly = tempAssembly;
            _valueCollector = valueCollector;
            if( valueCollector != null )
            {
                EngineMap = typeResult.RealObjects.EngineMap;
            }
        }

        /// <summary>
        /// True if a fatal error occured. Result should be discarded.
        /// </summary>
        public bool HasFatalError => _valueCollector == null;

        /// <summary>
        /// Gets the result of the types discovery and analysis.
        /// </summary>
        public CKTypeCollectorResult CKTypeResult { get; }

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
