using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Utility class that orders a set of oblivious reference types using
    /// <see cref="Type.IsAssignableFrom(Type?)"/> rule: this can be used to
    /// generate a valid switch-case on the <see cref="IPocoType.ImplTypeName"/> for
    /// an untyped variable.
    /// </summary>
    public readonly struct ObliviousReferenceTypeSorter
    {
        readonly List<IPocoType> _sorted;

        /// <summary>
        /// Initializes a new empty sorter.
        /// </summary>
        public ObliviousReferenceTypeSorter()
        {
            _sorted = new List<IPocoType>();
        }

        /// <summary>
        /// Adds a new type to this sorter.
        /// As a convenience, this can returns false if the type is already registered but this
        /// should be avoided.
        /// <para>
        /// The type must not be 'object', nor a value type and be the oblivious one.
        /// </para>
        /// </summary>
        /// <param name="t">The type to add.</param>
        /// <param name="throwIfExists">False to return false instead of throwing an ArgumentException if the type already exists.</param>
        /// <returns>True if the type has been added, false if <paramref name="throwIfExists"/> is false and the type has been already added.</returns>
        public bool Add( IPocoType t, bool throwIfExists = true )
        {
            Throw.CheckNotNullArgument( t );
            if( t.Kind == PocoTypeKind.Any
                || t.Type.IsValueType
                || !t.IsOblivious )
            {
                Throw.ArgumentException( $"The type must not be 'object', nor a value type and be the oblivious one: {t}." );
            }
            // Finds the first type that can be assigned to the new one:
            // the new one must appear before it.
            for( int i = 0; i < _sorted.Count; i++ )
            {
                IPocoType existing = _sorted[i];
                if( existing.Type.IsAssignableFrom( t.Type ) )
                {
                    if( existing == t )
                    {
                        if( throwIfExists ) Throw.ArgumentException( $"The type '{t}' is already registered." );
                        return false;
                    }
                    _sorted.Insert( i, t );
                    return true;
                }
            }
            _sorted.Add( t );
            return true;
        }

        /// <summary>
        /// Gets the sorted types added so far.
        /// </summary>
        public IReadOnlyList<IPocoType> SortedTypes => _sorted;
    }

}
