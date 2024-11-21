using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Captures the result of an engine run.
/// </summary>
public sealed partial class EngineResult
{
    readonly ImmutableArray<BinPath> _binPaths;
    readonly EngineConfiguration _configuration;
    readonly RunStatus _runStatus;

    public EngineResult( RunStatus runStatus, EngineConfiguration configuration, ImmutableArray<BinPathGroup> groups )
    {
        _runStatus = runStatus;
        _configuration = configuration;
        _binPaths = configuration.BinPaths.Select( c => new BinPath( this, c, groups.First( g => g.TypeGroup.Configurations.Contains( c ) ) ) )
                                          .ToImmutableArray();
    }

    /// <summary>
    /// Gets whether the run global run status.
    /// </summary>
    public RunStatus Status => _runStatus;

    /// <summary>
    /// Gets the result of the <see cref="EngineConfiguration.FirstBinPath"/>.
    /// </summary>
    public BinPath FirstBinPath => _binPaths[0];

    /// <summary>
    /// Gets a result for each <see cref="EngineConfiguration.BinPaths"/>.
    /// </summary>
    public ImmutableArray<BinPath> BinPaths => _binPaths;

    /// <summary>
    /// Finds the <see cref="BinPath"/> or throws a <see cref="ArgumentException"/> if not found.
    /// </summary>
    /// <param name="binPathName">The bin path name. Must be an existing BinPath or a <see cref="ArgumentException"/> is thrown.</param>
    /// <returns>The BinPath.</returns>
    public BinPath FindRequiredBinPath( string binPathName )
    {
        var b = FindBinPath( binPathName );
        if( b == null )
        {
            Throw.ArgumentException( nameof( binPathName ),
                                     $"""
                                      Unable to find BinPath named '{binPathName}'. Existing BinPaths are:
                                      '{_binPaths.Select( b => b.Name ).Concatenate( "', '" )}'.
                                      """ );
        }
        return b;
    }

    /// <summary>
    /// Tries to find the <see cref="BinPathConfiguration"/> or returns null.
    /// </summary>
    /// <param name="binPathName">The bin path name.</param>
    /// <returns>The BinPath or null.</returns>
    public BinPath? FindBinPath( string binPathName ) => _binPaths.FirstOrDefault( b => b.Name == binPathName );

}
