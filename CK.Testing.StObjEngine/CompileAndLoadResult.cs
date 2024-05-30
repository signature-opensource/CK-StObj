

using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Testing.StObjEngine
{
    /// <summary>
    /// Captures the result of <see cref="IStObjEngineTestHelperCore.CompileAndLoadStObjMap(StObjCollector, Func{StObjEngineConfiguration, StObjEngineConfiguration}?)"/>.
    /// </summary>
    public sealed class CompileAndLoadResult
    {
        /// <summary>
        /// Gets the result of the code generation.
        /// </summary>
        public GenerateCodeResult GenerateCodeResult { get; }

        /// <summary>
        /// Gets the result of the type analysis (the <see cref="GenerateCodeResult.CollectorResult"/>.
        /// </summary>
        public StObjCollectorResult CollectorResult => GenerateCodeResult.CollectorResult;

        /// <summary>
        /// Gets the StObjMap.
        /// </summary>
        public IStObjMap Map { get; }

        internal CompileAndLoadResult( GenerateCodeResult c, IStObjMap map )
        {
            GenerateCodeResult = c;
            Map = map;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void Deconstruct( out StObjCollectorResult result, out IStObjMap map )
        {
            result = CollectorResult;
            map = Map;
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
