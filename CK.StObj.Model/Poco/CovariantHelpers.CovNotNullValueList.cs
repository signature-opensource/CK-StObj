using System.Collections.Generic;
using System.Linq;

namespace CK.Core;

public static partial class CovariantHelpers
{
    /// <summary>
    /// List of value type that is also a IReadOnlyList of its nullable and a IReadOnlyList&lt;object&gt;.
    /// </summary>
    /// <typeparam name="T">The list item type.</typeparam>
    public sealed class CovNotNullValueList<T> : List<T>, IReadOnlyList<T?>, IReadOnlyList<object> where T : struct
    {
        /// <summary>
        /// Initialize a new empty list.
        /// </summary>
        public CovNotNullValueList() { }

        /// <summary>
        /// Initializes a new list with an initial content.
        /// </summary>
        /// <param name="collection">The initial content.</param>
        public CovNotNullValueList( IEnumerable<T> collection ) : base( collection ) { }

        /// <summary>
        /// Initializes a new list with an initial capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        public CovNotNullValueList( int capacity ) : base( capacity ) { }

        object IReadOnlyList<object>.this[int index] => this[index];

        T? IReadOnlyList<T?>.this[int index] => this[index];


        IEnumerator<object> IEnumerable<object>.GetEnumerator()
        {
            // Don't use this.Cast<object>().GetEnumerator(): stack overflow.
            var e = GetEnumerator();
            while( e.MoveNext() ) yield return e.Current;
        }

        IEnumerator<T?> IEnumerable<T?>.GetEnumerator()
        {
            // Don't use this.Cast<T?>().GetEnumerator(): stack overflow.
            var e = GetEnumerator();
            while( e.MoveNext() ) yield return e.Current;
        }
    }

}
