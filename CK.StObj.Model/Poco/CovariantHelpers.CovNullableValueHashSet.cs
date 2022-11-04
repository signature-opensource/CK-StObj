using System.Collections.Generic;
using System.Linq;

namespace CK.Core
{
    public static partial class CovariantHelpers
    {
        /// <summary>
        /// HashSet of a nullable value type that is a IReadOnlySet&lt;object?&gt;.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        public sealed class CovNullableValueHashSet<T> : HashSet<T?>, IReadOnlySet<object?> where T : struct
        {
            /// <summary>
            /// Initializes a new empty set with a default comparer for the nullable <typeparamref name="T"/>.
            /// </summary>
            public CovNullableValueHashSet() { }

            /// <summary>
            /// Initializes a new set with an initial content.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            public CovNullableValueHashSet( IEnumerable<T?> collection ) : base( collection ) { }

            /// <summary>
            /// Initializes a new set with a specific comparer.
            /// </summary>
            /// <param name="comparer">The comparer to use.</param>
            public CovNullableValueHashSet( IEqualityComparer<T?>? comparer ) : base( comparer ) { }

            /// <summary>
            /// Initializes a new set with an initial capacity.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            public CovNullableValueHashSet( int capacity ) : base( capacity ) { }

            /// <summary>
            /// Initializes a new set with an initial content and a specific comparer.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            /// <param name="comparer">The comparer to use.</param>
            public CovNullableValueHashSet( IEnumerable<T?> collection, IEqualityComparer<T?>? comparer ) : base( collection, comparer ) { }

            /// <summary>
            /// Initializes a new set with an initial capacity and a specific comparer.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            /// <param name="comparer">The comparer to use.</param>
            public CovNullableValueHashSet( int capacity, IEqualityComparer<T?>? comparer ) : base( capacity, comparer ) { }

            bool IReadOnlySet<object?>.Contains( object? item ) => (item is T e && Contains( e )) || (item == null && Contains( default ));

            bool IReadOnlySet<object?>.IsProperSubsetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsProperSubsetOf( this, other );

            bool IReadOnlySet<object?>.IsProperSupersetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsProperSupersetOf( this, other );

            bool IReadOnlySet<object?>.IsSubsetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsSubsetOf( this, other );

            bool IReadOnlySet<object?>.IsSupersetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsSupersetOf( this, other );

            bool IReadOnlySet<object?>.Overlaps( IEnumerable<object?> other ) => CovariantHelpers.NullableOverlaps( this, other );

            bool IReadOnlySet<object?>.SetEquals( IEnumerable<object?> other ) => CovariantHelpers.NullableSetEquals( this, other );

            IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => this.Cast<object?>().GetEnumerator();
        }

    }
}
