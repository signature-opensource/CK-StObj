

using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Testing.StObjEngine
{
    /// <summary>
    /// Captures the result of <see cref="StObjEngineTestHelperExtensions.RunSingleBinPathAndLoad(IMonitorTestHelper, EngineConfiguration, ISet{Type})"/>.
    /// </summary>
    /// <param name="EngineResult"> Gets the result of the engine. </param>
    /// <param name="Map"> Gets the map. </param>
    public sealed record RunAndLoadResult( StObjEngineResult EngineResult, IStObjMap Map );
}
