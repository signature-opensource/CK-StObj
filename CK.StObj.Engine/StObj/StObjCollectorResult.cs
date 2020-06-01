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
            if( valueCollector != null ) EngineMap = typeResult.RealObjects.EngineMap;
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

        /// <summary>
        /// Generates final assembly.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="c">The code generation context.</param>
        /// <param name="finalFilePath">Full path of the final dynamic assembly. Must end with '.dll'.</param>
        /// <param name="informationalVersion">Informational version.</param>
        /// <returns>False if any error occured (logged into <paramref name="monitor"/>).</returns>
        public CodeGenerateResult GenerateFinalAssembly( IActivityMonitor monitor, ICodeGenerationContext c, string finalFilePath, string? informationalVersion )
        {
            if( EngineMap == null ) throw new InvalidOperationException( nameof( HasFatalError ) );
            if( c.Assembly != _tempAssembly ) throw new ArgumentException( "CodeGenerationContext mismatch.", nameof( c ) );
            bool hasError = false;
            using( monitor.OnError( () => hasError = true ) )
            using( monitor.OpenInfo( $"Generating dynamic assembly (Saving source: {c.SaveSource}, Compilation: {c.CompileSource})." ) )
            {
                using( monitor.OpenInfo( "Registering direct properties as PostBuildProperties." ) )
                {
                    foreach( MutableItem item in EngineMap.StObjs.OrderedStObjs )
                    {
                        item.RegisterRemainingDirectPropertiesAsPostBuildProperties( _valueCollector );
                    }
                }
                if( !string.IsNullOrWhiteSpace( informationalVersion ) )
                {
                    DynamicAssembly.DefaultGenerationNamespace.Workspace.Global
                            .Append( "[assembly:System.Reflection.AssemblyInformationalVersion(" )
                            .AppendSourceString( informationalVersion )
                            .Append( ")]" )
                            .NewLine();
                }
                var r = GenerateSourceCode( monitor, finalFilePath, c );
                Debug.Assert( r.Success || hasError, "!success ==> An error has been logged." );
                return r;
            }
        }
    }
}
