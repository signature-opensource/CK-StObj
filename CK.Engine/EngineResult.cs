using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures the result of an engine run.
    /// </summary>
    public sealed class EngineResult
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


        /// <summary>
        /// Captures the result of a BinPath.
        /// </summary>
        public sealed class BinPath
        {
            readonly EngineResult _owner;
            readonly BinPathConfiguration _configuration;
            readonly BinPathGroup _group;
            readonly RunStatus _status;

            internal BinPath( EngineResult owner, BinPathConfiguration c, BinPathGroup group )
            {
                _owner = owner;
                _configuration = c;
                _group = group;
            }

            /// <summary>
            /// Gets whether this BinPath succeed, failed or have been skipped.
            /// </summary>
            public RunStatus Status => _group.Status;

            public EngineResult Owner => _owner;

            public string Name => _configuration.Name;

            public BinPathConfiguration Configuration => _configuration;

            public BinPathTypeGroup TypeGroup => _group.TypeGroup;

            public IPocoTypeSystemBuilder PocoTypeSystemBuilder
            {
                get
                {
                    Throw.CheckState( Owner.Status is not RunStatus.Failed );
                    return _group.PocoTypeSystemBuilder;
                }
            }

            public IStObjEngineMap EngineMap
            {
                get
                {
                    Throw.CheckState( Owner.Status is not RunStatus.Failed );
                    return _group.EngineMap;
                }
            }

            public IStObjMap LoadMap( IActivityMonitor monitor )
            {
                Throw.CheckState( Owner.Status is not RunStatus.Failed );
                return _group.LoadStObjMap( monitor );
            }

            public IStObjMap? TryLoadMap( IActivityMonitor monitor )
            {
                Throw.CheckState( Owner.Status is not RunStatus.Failed );
                return _group.TryLoadStObjMap( monitor );
            }

            public BinPathGroup Group => _group;
        }

    }

}
