using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Basic set backed by a <see cref="BitArray"/> used as an alternative to the <see cref="HashSet{T}"/>.
    /// </summary>
    public sealed class PocoTypeRawSet : IReadOnlyCollection<IPocoType>, IMinimalPocoTypeSet
    {
        readonly BitArray _flags;
        readonly IPocoTypeSystem _typeSystem;
        int _count;

        /// <summary>
        /// Initializes a new empty set.
        /// </summary>
        /// <param name="typeSystem">The type system that defines the types manipulated by this set.</param>
        public PocoTypeRawSet( IPocoTypeSystem typeSystem )
        {
            Throw.CheckNotNullArgument( typeSystem );
            _flags = new BitArray( typeSystem.AllNonNullableTypes.Count );
            _typeSystem = typeSystem;
        }

        /// <summary>
        /// Initializes a new set whose content is based on a predicate.
        /// </summary>
        /// <param name="typeSystem">The type system that defines the types manipulated by this set.</param>
        /// <param name="filter">The filter to apply to fill the set.</param>
        public PocoTypeRawSet( IPocoTypeSystem typeSystem, Func<IPocoType,bool> filter )
        {
            Throw.CheckNotNullArgument( typeSystem );
            Throw.CheckNotNullArgument( filter );
            _typeSystem = typeSystem;
            _flags = new BitArray( typeSystem.AllNonNullableTypes.Count );
            int i = 0;
            foreach( var t in typeSystem.AllNonNullableTypes )
            {
                if( filter( t ) )
                {
                    _flags.Set( i++, true );
                    ++_count;
                }
            }
        }

        /// <summary>
        /// Initializes a new set whose content can be the <see cref="IPocoTypeSystem.AllNonNullableTypes"/>
        /// (when <paramref name="all"/> is true).
        /// </summary>
        /// <param name="typeSystem">The type system that defines the types manipulated by this set.</param>
        /// <param name="all">True to include all the type system's types.</param>
        public PocoTypeRawSet( IPocoTypeSystem typeSystem, bool all )
        {
            Throw.CheckNotNullArgument( typeSystem );
            _typeSystem = typeSystem;
            _flags = new BitArray( _count = typeSystem.AllNonNullableTypes.Count, all );
        }

        /// <summary>
        /// Gets the type system.
        /// </summary>
        public IPocoTypeSystem TypeSystem => _typeSystem;

        /// <summary>
        /// Gets whether the given type is contained in this bag.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the type is contained, false otherwise.</returns>
        public bool Contains( IPocoType t ) => _flags[t.Index >> 1];

        /// <summary>
        /// Adds a type to this bag.
        /// </summary>
        /// <param name="t">The type to add.</param>
        /// <returns>True if the type has been added, false if it already exists.</returns>
        public bool Add( IPocoType t )
        {
            if( _flags[t.Index >> 1] ) return false;
            _flags[t.Index >> 1] = true;
            ++_count;
            return true;
        }

        /// <summary>
        /// Removes a type from this bag.
        /// </summary>
        /// <param name="t">The type to add.</param>
        /// <returns>True if the type has been removed, false if it doesn't belong to this bag.</returns>
        public bool Remove( IPocoType t )
        {
            if( !_flags[t.Index >> 1] ) return false;
            _flags[t.Index >> 1] = false;
            --_count;
            return true;
        }

        /// <summary>
        /// Clears this bag.
        /// </summary>
        public void Clear()
        {
            _flags.SetAll( false );
            _count = 0;
        }

        /// <summary>
        /// Gets the number of contained types.
        /// </summary>
        public int Count => _count;

        PocoTypeRawSet( PocoTypeRawSet o )
        {
            _flags = new BitArray( o._flags );
            _count = o.Count;
            _typeSystem = o.TypeSystem;
        }

        /// <summary>
        /// Clones this set.
        /// </summary>
        /// <returns>A clone.</returns>
        public PocoTypeRawSet Clone() => new PocoTypeRawSet( this );

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        public IEnumerator<IPocoType> GetEnumerator() => _typeSystem.AllNonNullableTypes.Where( t => _flags[t.Index >> 1] ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
