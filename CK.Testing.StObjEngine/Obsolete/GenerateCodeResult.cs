using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Testing
{
    /// <summary>
    /// The result of <see cref="StObjEngine.IStObjEngineTestHelperCore.GenerateCode(StObjCollector, Func{EngineConfiguration, EngineConfiguration}?, bool, CompileOption)"/>
    /// that captures the <see cref="StObjCollectorResult"/> and provides often needed property accessors to the single
    /// running group of the <see cref="EngineResult"/>.
    /// </summary>
    [Obsolete( "GenerateCode is obsolete." )]
    public readonly struct GenerateCodeResult
    {
        /// <summary>
        /// Gets whether the code generation succeeded (alias to <see cref="StObjEngineResult.Success"/>).
        /// </summary>
        public bool Success => EngineResult.Success;

        /// <summary>
        /// Gets the result of the successful type analysis.
        /// </summary>
        public StObjCollectorResult CollectorResult { get; }

        /// <summary>
        /// Gets the assembly file that may have been generated or not (alias to the single <see cref="IRunningBinPathGroup.GeneratedAssembly"/>).
        /// </summary>
        public IGeneratedArtifact AssemblyFile => EngineResult.Groups[0].GeneratedAssembly!;

        /// <summary>
        /// Gets the SHA1 of the run (alias to the single <see cref="IRunningBinPathGroup.RunSignature"/>).
        /// </summary>
        public SHA1Value RunSignature => EngineResult.Groups[0].RunSignature;

        /// <summary>
        /// Gets the engine result.
        /// There is necessarily one and only one <see cref="StObjEngineResult.Groups"/>.
        /// </summary>
        public StObjEngineResult EngineResult { get; }

        internal GenerateCodeResult( StObjCollectorResult r, StObjEngineResult e )
        {
            CollectorResult = r;
            EngineResult = e;
        }
    }
}
