using System.Collections.Generic;
using System.Linq;

namespace CK.Core
{
    public static partial class CovariantHelpers
    {
        /// <summary>
        /// List of nullable value type that is a IReadOnlyList&lt;object?&gt;.
        /// </summary>
        /// <typeparam name="T">The list item type.</typeparam>
        public sealed class CovNullableValueList<T> : List<T?>, IReadOnlyList<object?> where T : struct
        {
            /// <summary>
            /// Initialize a new empty list.
            /// </summary>
            public CovNullableValueList() { }

            /// <summary>
            /// Initializes a new list with an initial content.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            public CovNullableValueList( IEnumerable<T?> collection ) : base( collection ) { }

            /// <summary>
            /// Initializes a new list with an initial capacity.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            public CovNullableValueList( int capacity ) : base( capacity ) { }

            object? IReadOnlyList<object?>.this[int index] => this[index];

            IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
            {
                // Don't use this.Cast<object>().GetEnumerator(): stack overflow.
                var e = GetEnumerator();
                while( e.MoveNext() ) yield return e.Current;
            }
        }

    }
}
