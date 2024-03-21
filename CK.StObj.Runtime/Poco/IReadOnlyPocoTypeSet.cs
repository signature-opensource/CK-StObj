using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Read-only type set. See <see cref="PocoTypeRawSet"/>
    /// </summary>
    public interface IReadOnlyPocoTypeSet : IReadOnlyCollection<IPocoType>
    {
        sealed class E : IReadOnlyPocoTypeSet
        {
            public int Count => 0;

            public bool Contains( IPocoType t ) => false;

            public IEnumerator<IPocoType> GetEnumerator() => Enumerable.Empty<IPocoType>().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// An immutable empty singleton type set.
        /// </summary>
        public static readonly IReadOnlyPocoTypeSet Empty = new E();

        /// <summary>
        /// Gets whether the given type is contained in this set.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the type is contained, false otherwise.</returns>
        bool Contains( IPocoType t );
    }

}
