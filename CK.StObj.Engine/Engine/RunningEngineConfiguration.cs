using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup;

/// <summary>
/// Implements <see cref="IRunningEngineConfiguration"/>.
/// </summary>
sealed partial class RunningEngineConfiguration : IRunningEngineConfiguration
{
    readonly List<RunningBinPathGroup> _binPathGroups;
    readonly GlobalTypeCache _typeCache;

    /// <inheritdoc />
    public EngineConfiguration Configuration { get; }

    /// <inheritdoc cref="IRunningEngineConfiguration.Groups" />
    public IReadOnlyList<RunningBinPathGroup> Groups => _binPathGroups;

    IReadOnlyList<IRunningBinPathGroup> IRunningEngineConfiguration.Groups => _binPathGroups;

    // New way
    public RunningEngineConfiguration( EngineConfiguration configuration, GlobalTypeCache typeCache, IReadOnlyList<BinPathTypeGroup> groups )
    {
        Configuration = configuration;
        _typeCache = typeCache;
        _binPathGroups = groups.Select( g => new RunningBinPathGroup( configuration, typeCache, g ) ).ToList();
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

    #region Legacy
    public RunningEngineConfiguration( EngineConfiguration configuration, GlobalTypeCache typeCache )
    {
        _binPathGroups = new List<RunningBinPathGroup>();
        Configuration = configuration;
    }

    /// <summary>
    /// This throws if the CKSetup configuration cannot be used for any reason (missing
    /// expected information).
    /// </summary>
    internal void ApplyCKSetupConfiguration( IActivityMonitor monitor, XElement ckSetupConfig )
    {
        static NormalizedPath MakeAbsolutePath( EngineConfiguration c, NormalizedPath p )
        {
            if( !p.IsRooted ) p = c.BasePath.Combine( p );
            p = p.ResolveDots();
            return p;
        }
        Throw.DebugAssert( ckSetupConfig != null );
        using( monitor.OpenInfo( "Applying CKSetup configuration." ) )
        {
            var ckSetupSHA1 = (string?)ckSetupConfig.Element( "RunSignature" );
            if( Configuration.BaseSHA1.IsZero )
            {
                if( ckSetupSHA1 != null )
                {
                    Configuration.BaseSHA1 = SHA1Value.Parse( ckSetupSHA1 );
                    monitor.Trace( $"Using CKSetup RunSignature '{Configuration.BaseSHA1}' for BaseSHA1." );
                }
            }
            else
            {
                if( ckSetupSHA1 == null )
                {
                    monitor.Trace( $"Using BaseSHA1 '{Configuration.BaseSHA1}' from engine configuration (no RunSignature from CKSetup)." );
                }
                else
                {
                    monitor.Warn( $"Using BaseSHA1 '{Configuration.BaseSHA1}' from engine configuration, ignoring CKSetup RunSignature '{ckSetupSHA1}'." );
                }
            }

            var binPaths = ckSetupConfig.Elements( EngineConfiguration.xBinPaths ).SingleOrDefault();
            if( binPaths == null ) Throw.ArgumentException( nameof( ckSetupConfig ), $"Missing &lt;BinPaths&gt; single element in '{ckSetupConfig}'." );

            foreach( XElement xB in binPaths.Elements( EngineConfiguration.xBinPath ) )
            {
                var assemblies = xB.Descendants()
                                   .Where( e => e.Name == "Model" || e.Name == "ModelDependent" )
                                   .Select( e => e.Value )
                                   .Where( s => s != null );

                var path = (string?)xB.Attribute( EngineConfiguration.xPath );
                if( path == null ) Throw.ArgumentException( nameof( ckSetupConfig ), $"Missing Path attribute in '{xB}'." );

                var rootedPath = MakeAbsolutePath( Configuration, path );
                var c = Configuration.BinPaths.SingleOrDefault( b => b.Path == rootedPath );
                if( c == null ) Throw.ArgumentException( nameof( ckSetupConfig ), $"Unable to find one BinPath element with Path '{rootedPath}' in: {Configuration.ToXml()}." );

                c.Assemblies.AddRange( assemblies );
                monitor.Info( $"Added assemblies from CKSetup to BinPath '{rootedPath}':{Environment.NewLine}{assemblies.Concatenate( Environment.NewLine )}." );
            }
        }
    }

    /// <summary>
    /// Considers two BinPaths to be equal if and only if they have the same Assemblies,
    /// same ExcludedTypes and Types configurations.
    /// </summary>
    sealed class SimilarBinPathComparer : IEqualityComparer<BinPathConfiguration>
    {
        public static SimilarBinPathComparer Default = new SimilarBinPathComparer();

        public bool Equals( BinPathConfiguration? x, BinPathConfiguration? y )
        {
            Throw.DebugAssert( x != null && y != null );
            return x.Assemblies.SetEquals( y.Assemblies )
                   && x.ExcludedTypes.SetEquals( y.ExcludedTypes )
                   && x.Types.SetEquals( y.Types );
        }

        // Useless but fulfills the equality contract.
        public int GetHashCode( BinPathConfiguration b ) => b.ExcludedTypes.Count
                                                            + b.Types.Count * 79
                                                            + b.Assemblies.Count * 117;
    }
    #endregion // Legacy
}
