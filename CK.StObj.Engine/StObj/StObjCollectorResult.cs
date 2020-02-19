#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\StObjCollectorResult.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CK.CodeGen;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates the result of the <see cref="StObjCollector"/> work.
    /// </summary>
    public partial class StObjCollectorResult
    {
        readonly DynamicAssembly _tempAssembly;
        readonly StObjObjectEngineMap _liftedMap;
        readonly BuildValueCollector _valueCollector;

        internal StObjCollectorResult(
            CKTypeCollectorResult typeResult,
            DynamicAssembly tempAssembly,
            Dictionary<string, object> primaryRunCache,
            IReadOnlyList<MutableItem> orderedStObjs,
            BuildValueCollector valueCollector )
        {
            CKTypeResult = typeResult;
            _liftedMap = CKTypeResult?.RealObjects?.EngineMap;
            _tempAssembly = tempAssembly;
            if( primaryRunCache != null ) SecondaryRunAccessor = key => primaryRunCache[key];
            OrderedStObjs = orderedStObjs;
            _valueCollector = valueCollector;
        }

        /// <summary>
        /// Gets an accessor for the primary run cache only if this result comes
        /// from a primary run, null otherwise.
        /// </summary>
        public Func<string, object> SecondaryRunAccessor { get; }

        /// <summary>
        /// True if a fatal error occured. Result should be discarded.
        /// </summary>
        public bool HasFatalError => OrderedStObjs == null || (CKTypeResult?.HasFatalError ?? false);

        /// <summary>
        /// Gets the result of the types discovery and analysis.
        /// </summary>
        public CKTypeCollectorResult CKTypeResult { get; }

        /// <summary>
        /// Gets the <see cref="IStObjObjectEngineMap"/> that extends runtime <see cref="IStObjObjectMap"/>.
        /// </summary>
        public IStObjObjectEngineMap StObjs => _liftedMap;

        /// <summary>
        /// Gets the <see cref="IStObjServiceMap"/>.
        /// </summary>
        public IStObjServiceMap Services => _liftedMap;

        /// <summary>
        /// Gets the name of this StObj map.
        /// Never null, defaults to the empty string.
        /// </summary>
        public string MapName => _liftedMap?.MapName ?? String.Empty;

        /// <summary>
        /// Gets all the <see cref="IStObjResult"/> ordered by their dependencies.
        /// Null if <see cref="HasFatalError"/> is true.
        /// </summary>
        public IReadOnlyList<IStObjResult> OrderedStObjs { get; }

        /// <summary>
        /// Gets the features.
        /// </summary>
        public IReadOnlyCollection<VFeature> Features => _liftedMap.Features;

        /// <summary>
        /// Generates final assembly.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="finalFilePath">Full path of the final dynamic assembly. Must end with '.dll'.</param>
        /// <param name="saveSource">Whether generated source files must be saved alongside the final dll.</param>
        /// <param name="informationalVersion">Informational version.</param>
        /// <param name="skipCompilation">
        /// When true, compilation is skipped (but actual code generation step is always called).
        /// </param>
        /// <returns>False if any error occured (logged into <paramref name="monitor"/>).</returns>
        public CodeGenerateResult GenerateFinalAssembly(
            IActivityMonitor monitor,
            string finalFilePath,
            bool saveSource,
            string informationalVersion,
            bool skipCompilation )
        {
            bool hasError = false;
            using( monitor.OnError( () => hasError = true ) )
            using( monitor.OpenInfo( "Generating StObj dynamic assembly." ) )
            {
                using( monitor.OpenInfo( "Registering direct properties as PostBuildProperties." ) )
                {
                    foreach( MutableItem item in OrderedStObjs )
                    {
                        item.RegisterRemainingDirectPropertiesAsPostBuildProperties( _valueCollector );
                    }
                }
                if( !string.IsNullOrWhiteSpace( informationalVersion ) )
                {
                    _tempAssembly.DefaultGenerationNamespace.Workspace.Global
                            .Append( "[assembly:System.Reflection.AssemblyInformationalVersion(" )
                            .AppendSourceString( informationalVersion )
                            .Append( ")]" )
                            .NewLine();
                }
                var r = GenerateSourceCode( monitor, finalFilePath, saveSource, skipCompilation );
                Debug.Assert( r.Success || hasError, "!success ==> An error has been logged." );
                return r;
            }
        }
    }
}
