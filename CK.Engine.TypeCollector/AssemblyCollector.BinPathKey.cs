using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Engine.TypeCollector
{
    public sealed partial class AssemblyCollector
    {
        internal readonly struct BinPathKey : IEquatable<BinPathKey>
        {
            readonly BinPathConfiguration _configuration;

            public BinPathKey( BinPathConfiguration configuration )
            {
                _configuration = configuration;
            }

            public bool Equals( BinPathKey y )
            {
                return _configuration.Path == y._configuration.Path
                       && _configuration.DiscoverAssembliesFromPath == y._configuration.DiscoverAssembliesFromPath
                       && _configuration.Assemblies.SetEquals( y._configuration.Assemblies );
            }

            public override int GetHashCode()
            {
                int unorderedPoorHash = 0;
                foreach( var s in _configuration.Assemblies ) unorderedPoorHash ^= s.GetHashCode();
                return HashCode.Combine( _configuration.Path, _configuration.DiscoverAssembliesFromPath, unorderedPoorHash );
            }

            public override bool Equals( object? obj ) => obj is BinPathKey k && Equals( k );
        }
    }
}
