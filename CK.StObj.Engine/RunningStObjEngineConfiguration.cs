using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    public sealed partial class RunningStObjEngineConfiguration : StObjEngineConfiguration<RunningBinPathConfiguration>, IRunningStObjEngineConfiguration
    {
        readonly List<RunningBinPathGroup> _binPathGroups;

        internal RunningStObjEngineConfiguration( XElement e ) : base( e )
        {
            _binPathGroups = new List<RunningBinPathGroup>();
        }

        /// <summary>
        /// Creates and initialize a <see cref="RunningStObjEngineConfiguration"/> from a <see cref="StObjEngineConfiguration"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The engine configuration.</param>
        /// <returns>The running configuration or null.</returns>
        public static RunningStObjEngineConfiguration? CreateRunningConfiguration( IActivityMonitor monitor, StObjEngineConfiguration c )
        {
            var r = new RunningStObjEngineConfiguration( c.ToXml() );
            return r.Initialize( monitor ) ? r : null;
        }

        /// <inheritdoc />
        protected override RunningBinPathConfiguration CreateBinPath( XElement e ) => new RunningBinPathConfiguration( e );

        public IReadOnlyList<RunningBinPathGroup> BinPathGroups { get; private set; }

        internal bool Initialize( IActivityMonitor monitor )
        {
            if( BaseSHA1.IsZero || BaseSHA1 == SHA1Value.Empty )
            {
                BaseSHA1 = SHA1Value.ComputeHash( CoreApplicationIdentity.InstanceId );
                monitor.Info( $"Zero or Empty BaseSHA1, using a random one '{BaseSHA1}'. This disables any cache." );
            }
            if( BinPaths.Count == 1 )
            {
                var b = BinPaths[0];
                b.ExcludedTypes.AddRange( GlobalExcludedTypes );
                b.Group = new RunningBinPathGroup( b, new[] { b }, BaseSHA1 );
                _binPathGroups.Add( b.Group );
                monitor.Trace( $"No unification required (single BinPath)." );
            }
            else
            {
                if( !InitializeMultipleBinPaths( monitor ) ) return false;
            }
            return true;
        }

        bool InitializeMultipleBinPaths( IActivityMonitor monitor )
        {
            // Propagates root excluded types to all bin paths.
            foreach( var f in BinPaths )
            {
                f.ExcludedTypes.AddRange( GlobalExcludedTypes );
            }

            // Starts by grouping actual BinPaths by similarity.
            foreach( var g in BinPaths.GroupBy( Util.FuncIdentity, SimilarBinPathComparer.Default ) )
            {
                // Ordering the set here to ensure a deterministic head for the group. 
                var similar = g.OrderBy( b => b.Path ).ToArray();
                var group = new RunningBinPathGroup( similar[0], similar, SHA1Value.ComputeHash( BaseSHA1.ToString() + similar[0].Path ) );
                foreach( var b in g )
                {
                    b.Group = group;
                }
            }
            if( _binPathGroups.Count > 1 )
            {
                // Create the unified BinPath. If this fails, it's useless to continue.
                var unifiedBinPath = CreateUnifiedBinPathConfiguration( monitor, _binPathGroups, GlobalExcludedTypes );
                if( unifiedBinPath == null ) return false;

                // Is the UnifiedBinPath required?
                int idx = _binPathGroups.FindIndex( b => SimilarBinPathComparer.Default.Equals( b.Configuration, unifiedBinPath ) );
                RunningBinPathGroup found;
                if( idx >= 0 )
                {
                    found = _binPathGroups[idx];
                    monitor.Trace( $"No unification required, BinPath '{found.Configuration.Path}' covers all the required components." );
                    _binPathGroups.RemoveAt( idx );
                }
                else
                {
                    found = new RunningBinPathGroup( unifiedBinPath, new[] {unifiedBinPath}, SHA1Value.ComputeHash( DateTime.UtcNow.ToLongDateString() ) );
                    monitor.Info( $"Unification required." );
                }
                // Ensures that it is the first in the list of BinPaths to process:
                // the "covering" bin path must be the PrimaryPath, be it a pure unified or a regular one.
                _binPathGroups.Insert( 0, found );
            }
            else
            {
                monitor.Trace( $"No unification required, {BinPaths.Count} BinPaths share the same components." );
            }
            return true;
        }

        /// <summary>
        /// Considers two BinPaths to be equal if and only if they have the same Assemblies,
        /// same ExcludedTypes and Types configurations.
        /// </summary>
        sealed class SimilarBinPathComparer : IEqualityComparer<RunningBinPathConfiguration>
        {
            public static SimilarBinPathComparer Default = new SimilarBinPathComparer();

            public bool Equals( RunningBinPathConfiguration? x, RunningBinPathConfiguration? y )
            {
                Debug.Assert( x != null && y != null );
                bool s = x.Types.Count == y.Types.Count
                         && x.Assemblies.SetEquals( y.Assemblies )
                         && x.ExcludedTypes.SetEquals( y.ExcludedTypes );
                if( s )
                {
                    var one = x.Types.Select( xB => xB.ToString() ).OrderBy( Util.FuncIdentity );
                    var two = x.Types.Select( xB => xB.ToString() ).OrderBy( Util.FuncIdentity );
                    s = one.SequenceEqual( two );
                }
                return s;
            }

            public int GetHashCode( RunningBinPathConfiguration b ) => b.ExcludedTypes.Count
                                                                        + b.Types.Count * 59
                                                                        + b.Assemblies.Count * 117;
        }

        /// <summary>
        /// Creates a <see cref="BinPathConfiguration"/> that unifies multiple <see cref="BinPathConfiguration"/>.
        /// This configuration is the one used on the unified working directory.
        /// This unified configuration doesn't contain any <see cref="BinPathConfiguration.AspectConfigurations"/>.
        /// </summary>
        /// <param name="monitor">Monitor for error.</param>
        /// <param name="configurations">Multiple configurations.</param>
        /// <param name="globalExcludedTypes">Types to exclude: see <see cref="StObjEngineConfiguration{T}.GlobalExcludedTypes"/>.</param>
        /// <returns>The unified configuration or null on error.</returns>
        static RunningBinPathConfiguration? CreateUnifiedBinPathConfiguration( IActivityMonitor monitor,
                                                                               IEnumerable<RunningBinPathGroup> configurations,
                                                                               IEnumerable<string> globalExcludedTypes )
        {
            var unified = new RunningBinPathConfiguration()
            {
                IsUnifiedPure = true,
                Path = AppContext.BaseDirectory,
                OutputPath = AppContext.BaseDirectory,
                // The root (the Working directory) doesn't want any output by itself.
                GenerateSourceFiles = false
            };
            Debug.Assert( unified.CompileOption == CompileOption.None );
            // Assemblies and types are the union of the assemblies and types of the bin paths.
            unified.Assemblies.AddRange( configurations.SelectMany( b => b.Configuration.Assemblies ) );

            var fusion = new Dictionary<string, BinPathConfiguration.TypeConfiguration>();
            foreach( var c in configurations.SelectMany( b => b.Configuration.Types ) )
            {
                if( fusion.TryGetValue( c.Name, out var exists ) )
                {
                    if( !c.Optional ) exists.Optional = false;
                    if( exists.Kind != c.Kind )
                    {
                        monitor.Error( $"Invalid Type configuration across BinPaths for '{c.Name}': {exists.Kind} vs. {c.Kind}." );
                        return null;
                    }
                }
                else fusion.Add( c.Name, new BinPathConfiguration.TypeConfiguration( c.Name, c.Kind, c.Optional ) );
            }
            unified.Types.AddRange( fusion.Values );
            unified.ExcludedTypes.AddRange( globalExcludedTypes );
            return unified;
        }


        IReadOnlyList<IStObjEngineAspectConfiguration> IRunningStObjEngineConfiguration.Aspects => Aspects;

        IReadOnlyList<IRunningBinPathConfiguration> IRunningStObjEngineConfiguration.BinPaths => BinPaths;

        IReadOnlySet<string> IRunningStObjEngineConfiguration.GlobalExcludedTypes => GlobalExcludedTypes;
    }
}
