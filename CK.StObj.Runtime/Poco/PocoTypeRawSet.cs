using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    /// <summary>
    /// Basic set backed by a bit flag used as a faster <see cref="HashSet{T}"/> that handles
    /// both nullable and non nullable types at the same time (the <see cref="Count"/> is always even).
    /// </summary>
    public sealed class PocoTypeRawSet : IReadOnlyPocoTypeSet, IMinimalPocoTypeSet
    {
        // Only TypeSet IPocoTypeSet implementation access to this array to expose
        // it as an immutable array: a IPocoTypeSet is a immutable object that protects
        // its PocoTypeRawSet.
        internal readonly int[] _array;
        readonly IPocoTypeSystem _typeSystem;
        readonly NonNullableSet _nonNullables;
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
        /// Initializes a new set with a content based on a predicate.
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
                    ++_count;
                }
                ++i;
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
            _array = new int[(int)((uint)(typeSystem.AllNonNullableTypes.Count - 1 + (1 << 5)) >> 5)];
            if( all )
            {
                new Span<int>( _array ).Fill( -1 );
                // Clears high bit values in the last int to be able to use SequenceEquals
                // on the ints regardless of how the set is initialized.
                int count = typeSystem.AllNonNullableTypes.Count;
                int extraBits = count & 31;
                if( extraBits > 0 )
                {
                    _array[^1] = (1 << extraBits) - 1;
                }
                _count = count;
            }
            _nonNullables = new NonNullableSet( this );
        }

        /// <summary>
        /// Gets the type system.
        /// </summary>
        public IPocoTypeSystem TypeSystem => _typeSystem;

        /// <summary>
        /// Gets the number of contained types.
        /// This is necessarily even since nullable and non nullable types are coupled.
        /// </summary>
        public int Count => _count << 1;

        /// <inheritdoc />
        public bool Contains( IPocoType t ) => Get( t.Index >> 1 );

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        bool Get( int index ) => (_array[index >> 5] & (1 << index)) != 0;

        /// <summary>
        /// Adds a type to this set.
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
        /// Removes a type from this set.
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
        /// Gets the non nullable types contained in this set.
        /// </summary>
        public IReadOnlyPocoTypeSet NonNullableTypes => _nonNullables;

        /// <summary>
        /// Gets an immutable array of the internal flags.
        /// </summary>
        /// <returns>An immutable array with the internal flags.</returns>
        public ImmutableArray<int> CreateFlagArray()
        {
            return ImmutableArray.Create<int>( _array );
        }

        /// <summary>
        /// Gets the internal flag array.
        /// </summary>
        public IReadOnlyList<int> FlagArray => _array;

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

        /// <summary>
        /// Gets whether this set is a super set of another set.
        /// <para>
        /// The other <see cref="TypeSystem"/> must be the same as this one otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="other">The other type set.</param>
        /// <returns>True if this set is a super set of the other one.</returns>
        public bool IsSupersetOf( PocoTypeRawSet other )
        {
            if( other._typeSystem != _typeSystem || _count < other._count ) return false;
            if( other == this ) return true;

            Throw.DebugAssert( "This is driven by the initial TypeSystem.", _array.Length == other._array.Length );

            uint count = (uint)_array.Length;
            int[] tA = _array;
            int[] oA = other._array;
            uint i = 0;
#if NET8_0_OR_GREATER
            if( _array.Length >= 8 )
            {
                ref int left = ref MemoryMarshal.GetArrayDataReference<int>( tA );
                ref int right = ref MemoryMarshal.GetArrayDataReference<int>( oA );

                if( Vector512.IsHardwareAccelerated && count >= Vector512<int>.Count )
                {
                    for( ; i < count - (Vector512<int>.Count - 1u); i += (uint)Vector512<int>.Count )
                    {
                        var l = Vector512.LoadUnsafe( ref left, i );
                        var r = l | Vector512.LoadUnsafe( ref right, i );
                        if( l != r ) return false;
                    }
                }
                else if( Vector256.IsHardwareAccelerated && count >= Vector256<int>.Count )
                {
                    for( ; i < count - (Vector256<int>.Count - 1u); i += (uint)Vector256<int>.Count )
                    {
                        var l = Vector256.LoadUnsafe( ref left, i );
                        var r = l | Vector256.LoadUnsafe( ref right, i );
                        if( l != r ) return false;
                    }
                }
                else if( Vector128.IsHardwareAccelerated && count >= Vector128<int>.Count )
                {
                    for( ; i < count - (Vector128<int>.Count - 1u); i += (uint)Vector128<int>.Count )
                    {
                        var l = Vector128.LoadUnsafe( ref left, i );
                        var r = l | Vector128.LoadUnsafe( ref right, i );
                        if( l != r ) return false;
                    }
                }
            }
#endif
            for( ; i < count; i++ )
            {
                var l = tA[ i ];
                var r = l | oA[i];
                if( l != r ) return false;
            }
            return true;
        }

        PocoTypeRawSet( PocoTypeRawSet o )
        {
            _array = (int[])o._array.Clone();
            _count = o._count;
            _typeSystem = o.TypeSystem;
            _nonNullables = new NonNullableSet( this );
        }

        /// <summary>
        /// Clones this set.
        /// </summary>
        /// <returns>A clone.</returns>
        public PocoTypeRawSet Clone() => new PocoTypeRawSet( this );

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        public IEnumerator<IPocoType> GetEnumerator() => _typeSystem.AllTypes.Where( Contains ).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IEnumerator<IPocoType> GetNonNullableEnumerator() => _typeSystem.AllNonNullableTypes.Where( Contains ).GetEnumerator();

        sealed class NonNullableSet : IReadOnlyPocoTypeSet
        {
            readonly PocoTypeRawSet _s;

            public NonNullableSet( PocoTypeRawSet s ) => _s = s;

            public int Count => _s._count;

            public bool Contains( IPocoType t ) => !t.IsNullable && _s.Contains( t );

            public IEnumerator<IPocoType> GetEnumerator() => _s.GetNonNullableEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _s.GetNonNullableEnumerator();
        }

    }

}
