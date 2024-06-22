using System;
using System.Collections.Immutable;

namespace CK.Engine.TypeCollector
{
    public readonly struct RegType : IEquatable<RegType> 
    {
        /// <summary>
        /// Type to register.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// The Type's assembly.
        /// </summary>
        public CachedAssembly Assembly { get; }

        /// <summary>
        /// Type analysis heavily relies on supported interfaces. They are captured
        /// here because when registering types that are associated to a <see cref="Setup.ConfigurableAutoServiceKind"/>,
        /// we must starts with the leaves up to the base to avoid any backtracking in the algorithm. At this 
        /// level we only use the count of base interfaces as a good enough inheritance depth. 
        /// 
        /// </summary>
        public ImmutableArray<Type> Interfaces { get; }

        internal RegType( Type type, CachedAssembly assembly )
        {
            Type = type;
            Assembly = assembly;
        }

        public bool Equals( RegType other ) => Type == other.Type;

        public override int GetHashCode() => Type.GetHashCode();

        public static bool operator ==( RegType left, RegType right ) => left.Type == right.Type;

        public static bool operator !=( RegType left, RegType right ) => !(left == right);

        public override bool Equals( object? obj ) => obj is RegType r && Equals( r );
    }

}
