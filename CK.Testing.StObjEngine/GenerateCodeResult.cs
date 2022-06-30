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
        /// Gets whether the code generation succeeded.
        /// </summary>
        public readonly bool Success;

        /// <summary>
        /// Gets the <see cref="IStObjMap"/> if it is available.
        /// </summary>
        public readonly IStObjMap? EmbeddedStObjMap;

        internal GenerateCodeResult( StObjCollectorResult r, bool s, IStObjMap? m )
        {
            Collector = r;
            Success = s;
            EmbeddedStObjMap = m;
        }
    }
}
