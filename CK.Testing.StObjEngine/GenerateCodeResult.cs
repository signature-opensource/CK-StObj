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
        /// Gets the embedded <see cref="IStObjMap"/> if it is available.
        /// </summary>
        public readonly IStObjMap? EmbeddedStObjMap;

        /// <summary>
        /// Gets the assembly name that may have been generated or not.
        /// <para>
        /// If the assembly exists and <see cref="EmbeddedStObjMap"/> is null, a new StObjMap can be obtained from it.
        /// </para>
        /// </summary>
        public readonly string AssemblyName;

        internal GenerateCodeResult( StObjCollectorResult r, bool s, IStObjMap? embedded, string assemblyName )
        {
            Collector = r;
            Success = s;
            EmbeddedStObjMap = embedded;
            AssemblyName = assemblyName;
        }
    }
}
