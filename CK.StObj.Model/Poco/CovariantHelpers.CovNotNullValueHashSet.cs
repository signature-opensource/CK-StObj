using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core
{
    public static partial class CovariantHelpers
    {
        /// <summary>
        /// HashSet of a non nullable value type that is also a IReadOnlySet of its nullable and a IReadOnlySet&lt;object&gt;.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        public sealed class CovNotNullValueHashSet<T> : HashSet<T>, IReadOnlySet<T?>, IReadOnlySet<object> where T : struct
        {
            /// <summary>
            /// Initializes a new empty set with a default comparer for the <typeparamref name="T"/>.
            /// </summary>
            public CovNotNullValueHashSet() { }

            /// <summary>
            /// Initializes a new set with an initial content.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            public CovNotNullValueHashSet( IEnumerable<T> collection ) : base( collection ) { }

            /// <summary>
            /// Initializes a new set with a specific comparer.
            /// </summary>
            /// <param name="comparer">The comparer to use.</param>
            public CovNotNullValueHashSet( IEqualityComparer<T>? comparer ) : base( comparer ) { }

            /// <summary>
            /// Initializes a new set with an initial capacity.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            public CovNotNullValueHashSet( int capacity ) : base( capacity ) { }

            /// <summary>
            /// Initializes a new set with an initial content and a specific comparer.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            /// <param name="comparer">The comparer to use.</param>
            public CovNotNullValueHashSet( IEnumerable<T> collection, IEqualityComparer<T>? comparer ) : base( collection, comparer ) { }

            /// <summary>
            /// Initializes a new set with an initial capacity and a specific comparer.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            /// <param name="comparer">The comparer to use.</param>
            public CovNotNullValueHashSet( int capacity, IEqualityComparer<T>? comparer ) : base( capacity, comparer ) { }

            bool IReadOnlySet<object>.Contains( object item ) => item is T v && Contains( v );

            bool IReadOnlySet<object>.IsProperSubsetOf( IEnumerable<object> other ) => CovariantHelpers.IsProperSubsetOf( this, other );

            bool IReadOnlySet<object>.IsSubsetOf( IEnumerable<object> other ) => CovariantHelpers.IsSubsetOf( this, other );

            bool IReadOnlySet<object>.IsProperSupersetOf( IEnumerable<object> other ) => CovariantHelpers.IsProperSupersetOf( this, other );

            bool IReadOnlySet<object>.IsSupersetOf( IEnumerable<object> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<object>.Overlaps( IEnumerable<object> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<object>.SetEquals( IEnumerable<object> other ) => CovariantHelpers.SetEquals( this, other );

            IEnumerator<object> IEnumerable<object>.GetEnumerator()
            {
                // Don't use this.Cast<object>().GetEnumerator(): stack overflow.
                var e = GetEnumerator();
                while( e.MoveNext() ) yield return e.Current;
            }

            #region Nullable item support.

            bool IReadOnlySet<T?>.Contains( T? item ) => item.HasValue && base.Contains( item.Value );

            bool IReadOnlySet<T?>.IsProperSubsetOf( IEnumerable<T?> other ) => CovariantHelpers.IsProperSubsetOf( this, other );

            bool IReadOnlySet<T?>.IsProperSupersetOf( IEnumerable<T?> other ) => CovariantHelpers.IsProperSupersetOf( this, other );

            bool IReadOnlySet<T?>.IsSubsetOf( IEnumerable<T?> other ) => CovariantHelpers.IsSubsetOf( this, other );

            bool IReadOnlySet<T?>.IsSupersetOf( IEnumerable<T?> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<T?>.Overlaps( IEnumerable<T?> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<T?>.SetEquals( IEnumerable<T?> other ) => CovariantHelpers.SetEquals( this, other );

            sealed class NullEnumerator : IEnumerator<T?>
            {
                readonly IEnumerator<T> _inner;

                public NullEnumerator( IEnumerator<T> inner )
                {
                    _inner = inner;
                }

                public T? Current => _inner.Current;

                object IEnumerator.Current => _inner.Current;

                public void Dispose() => _inner.Dispose();

                public bool MoveNext() => _inner.MoveNext();

                public void Reset() => _inner.Reset();
            }

            IEnumerator<T?> IEnumerable<T?>.GetEnumerator() => new NullEnumerator( GetEnumerator() );

            #endregion
        }

    }
}
