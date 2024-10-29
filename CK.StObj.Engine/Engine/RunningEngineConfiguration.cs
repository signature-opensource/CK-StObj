using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup;

/// <summary>
/// Implements <see cref="IRunningEngineConfiguration"/>.
/// </summary>
sealed partial class RunningEngineConfiguration : IRunningEngineConfiguration
{
    readonly List<RunningBinPathGroup> _binPathGroups;

    /// <inheritdoc />
    public EngineConfiguration Configuration { get; }

    /// <inheritdoc cref="IRunningEngineConfiguration.Groups" />
    public IReadOnlyList<RunningBinPathGroup> Groups => _binPathGroups;

    IReadOnlyList<IRunningBinPathGroup> IRunningEngineConfiguration.Groups => _binPathGroups;

    // New way
    public RunningEngineConfiguration( EngineConfiguration configuration, IReadOnlyList<BinPathTypeGroup> groups )
    {
        Configuration = configuration;
        _binPathGroups = groups.Select( g => new RunningBinPathGroup( configuration, g ) ).ToList();
    }


    internal bool Initialize( IActivityMonitor monitor, out bool canSkipRun )
    {
        // Lets be optimistic.
        canSkipRun = true;
        // Provides the canSkipRun to each group.
        foreach( var g in _binPathGroups )
        {
            if( !g.Initialize( monitor, Configuration.ForceRun, ref canSkipRun ) ) return false;
        }
        return true;
    }
}
