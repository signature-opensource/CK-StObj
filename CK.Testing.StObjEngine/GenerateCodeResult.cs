using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Testing
{
    /// <summary>
    /// Defines the result of <see cref="StObjEngine.IStObjEngineTestHelperCore.GenerateCode(StObjCollector, CompileOption)"/>.
    /// </summary>
    public readonly struct GenerateCodeResult
    {
        /// <summary>
        /// Gets the <see cref="StObjCollector"/> result that is successful.
        /// </summary>
        public readonly StObjCollectorResult Collector;

        /// <summary>
        /// Gets the code genration result.
        /// </summary>
        public readonly StObjCollectorResult.CodeGenerateResult CodeGen;

        /// <summary>
        /// Gets the <see cref="IStObjMap"/> if it is available.
        /// </summary>
        public readonly IStObjMap? EmbeddedStObjMap;

        internal GenerateCodeResult( StObjCollectorResult r, StObjCollectorResult.CodeGenerateResult c, IStObjMap? m )
        {
            Collector = r;
            CodeGen = c;
            EmbeddedStObjMap = m;
        }
    }
}
