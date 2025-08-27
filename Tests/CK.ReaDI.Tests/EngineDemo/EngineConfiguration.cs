using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Demo;

public class EngineConfiguration
{
    public bool DebugMode { get; set; }

    public List<EngineAspectConfiguration> Aspects { get; } = new List<EngineAspectConfiguration>();

    public List<BinPathConfiguration> BinPaths { get; } = new List<BinPathConfiguration>();

    public bool NormalizeConfiguration( IActivityMonitor monitor )
    {
        if( BinPaths.Count == 0 )
        {
            monitor.Error( "Empty BinPaths." );
            return false;
        }
        return true;
    }

    public override string ToString() => $"EngineConfiguration( DebugMode: {DebugMode} )";
}
