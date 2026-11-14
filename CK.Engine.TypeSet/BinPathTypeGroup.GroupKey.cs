using CK.Core;
using CK.Setup;
using System;
using System.Linq;

namespace CK.Engine.TypeCollector;


public sealed partial class BinPathTypeGroup
{
    internal readonly struct GroupKey : IEquatable<GroupKey>
    {
        readonly AssemblyCache.BinPathGroup _assemblyGroup;
        readonly BinPathConfiguration _configuration;

        public GroupKey( AssemblyCache.BinPathGroup assemblyGroup, BinPathConfiguration configuration )
        {
            _assemblyGroup = assemblyGroup;
            _configuration = configuration;
        }

        public AssemblyCache.BinPathGroup AssemblyGroup => _assemblyGroup;

        public BinPathConfiguration Configuration => _configuration;

        public bool Equals( GroupKey y )
        {
            Throw.DebugAssert( "Type key specializes Assembly key.",
                                _configuration.Path == y._configuration.Path
                                && _configuration.DiscoverAssembliesFromPath == y._configuration.DiscoverAssembliesFromPath
                                && _configuration.Assemblies.SetEquals( y._configuration.Assemblies ) );
            return _configuration.ExcludedTypes.SetEquals( y._configuration.ExcludedTypes )
                   && _configuration.Types.SetEquals( y._configuration.Types )
                   && _configuration.TypeSets.Count == y._configuration.TypeSets.Count
                   && _configuration.TypeSets.All( s => y._configuration.FindTypeSet( s.Name ) is { } o && s.SetEquals( o ) );
        }

        public override int GetHashCode()
        {
            int unorderedPoorHash1 = 0;
            foreach( var t in _configuration.Types ) unorderedPoorHash1 ^= t.GetHashCode();
            int unorderedPoorHash2 = 0;
            foreach( var t in _configuration.ExcludedTypes ) unorderedPoorHash2 ^= t.GetHashCode();
            int unorderedPoorHash3 = 0;
            foreach( var s in _configuration.TypeSets ) unorderedPoorHash3 ^= s.GetContentHashCode();
            return HashCode.Combine( unorderedPoorHash1, unorderedPoorHash2, unorderedPoorHash3 );
        }

        public override bool Equals( object? obj ) => obj is GroupKey k && Equals( k );
    }
}
