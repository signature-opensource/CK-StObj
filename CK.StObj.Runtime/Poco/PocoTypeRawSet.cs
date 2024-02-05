using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    /// <summary>
    /// Basic set backed by a bit flag used as a faster <see cref="HashSet{T}"/>.
    /// </summary>
    public sealed class PocoTypeRawSet : IReadOnlyCollection<IPocoType>, IMinimalPocoTypeSet
    {
        readonly int[] _array;
        readonly IPocoTypeSystem _typeSystem;
        int _count;

        /// <summary>
        /// Initializes a new empty set.
        /// </summary>
        /// <param name="typeSystem">The type system that defines the types manipulated by this set.</param>
        public PocoTypeRawSet( IPocoTypeSystem typeSystem )
            : this( typeSystem, false )
        {
        }

        /// <summary>
        /// Initializes a new set whose content is based on a predicate.
        /// </summary>
        /// <param name="typeSystem">The type system that defines the types manipulated by this set.</param>
        /// <param name="filter">The filter to apply to fill the set.</param>
        public PocoTypeRawSet( IPocoTypeSystem typeSystem, Func<IPocoType,bool> filter )
            : this( typeSystem, false )
        {
            Throw.CheckNotNullArgument( filter );
            int i = 0;
            foreach( var t in typeSystem.AllNonNullableTypes )
            {
                if( filter( t ) )
                {
                    ref int segment = ref _array[i >> 5];
                    segment |= (1 << i);
                    ++i;
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
            var l = typeSystem.AllNonNullableTypes.Count;
            _array = new int[(int)((uint)(l - 1 + (1 << 5)) >> 5)];
            if( all )
            {
                new Span<int>( _array ).Fill( -1 );
                // Clears high bit values in the last int to be able to use SequenceEquals
                // on the ints regardless of how the set is initialized.
                int extraBits = l & 31;
                if( extraBits > 0 )
                {
                    _array[^1] = (1 << extraBits) - 1;
                }
                _count = l;
            }
        }

        /// <summary>
        /// Gets the type system.
        /// </summary>
        public IPocoTypeSystem TypeSystem => _typeSystem;

        /// <summary>
        /// Gets the number of contained types.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets whether the given type is contained in this bag.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the type is contained, false otherwise.</returns>
        public bool Contains( IPocoType t ) => Get( t.Index >> 1 );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        bool Get( int index ) => (_array[index >> 5] & (1 << index)) != 0;

        /// <summary>
        /// Adds a type to this bag.
        /// </summary>
        /// <param name="t">The type to add.</param>
        /// <returns>True if the type has been added, false if it already exists.</returns>
        public bool Add( IPocoType t )
        {
            int idx = t.Index >> 1;
            ref int segment = ref _array[idx >> 5];
            int bitMask = 1 << idx;
            if( (segment & bitMask) != 0 ) return false;
            segment |= bitMask;
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
            int idx = t.Index >> 1;
            ref int segment = ref _array[idx >> 5];
            int bitMask = 1 << idx;
            if( (segment & bitMask) == 0 ) return false;
            segment &= ~bitMask;
            --_count;
            return true;
        }

        /// <summary>
        /// Clears this set.
        /// </summary>
        public void Clear()
        {
            new Span<int>( _array ).Fill( 0 );
            _count = 0;
        }

        /// <summary>
        /// Implements equality on the content.
        /// We don't use Equals, we don't override GetHashCode.
        /// </summary>
        /// <param name="other">The other set.</param>
        /// <returns>True if sets have the same content.</returns>
        public bool SameContentAs( PocoTypeRawSet other )
        {
            return other._typeSystem == _typeSystem
                   && other._count == _count
                   && other._array.AsSpan().SequenceEqual( _array );
        }

        PocoTypeRawSet( PocoTypeRawSet o )
        {
            _array = (int[])o._array.Clone();
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
        public IEnumerator<IPocoType> GetEnumerator() => _typeSystem.AllNonNullableTypes.Where( Contains ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
