using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Demo;

[ReaDILoopRootParameter]
public class EngineConfiguration
{
    public bool DebugMode { get; set; }

    public override string ToString() => $"EngineConfiguration( DebugMode: {DebugMode} )";
}
