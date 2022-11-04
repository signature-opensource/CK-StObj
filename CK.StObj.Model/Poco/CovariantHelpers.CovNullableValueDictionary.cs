using System.Collections.Generic;
using System.Linq;

namespace CK.Core
{
    public static partial class CovariantHelpers
    {
        /// <summary>
        /// Dictionary for nullable value type of <typeparamref name="TValue"/> that is also
        /// a IReadOnlyDictionary&lt;TKey, object?&gt;.
        /// </summary>
        /// <typeparam name="TKey">The type of the key. This is invariant.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        public sealed class CovNullableValueDictionary<TKey, TValue> : Dictionary<TKey, TValue?>,
                                                                       IReadOnlyDictionary<TKey, object?>
            where TKey : notnull
            where TValue : struct
        {
            /// <summary>
            /// Initializes a new empty Dictionary that has the default initial capacity, and uses the default equality
            /// comparer for the key type.
            /// </summary>
            public CovNullableValueDictionary() { }

            /// <summary>
            /// Initializes a new dictionary that contains elements copied from the specified IDictionary
            /// and uses the default equality comparer for the key type.
            /// </summary>
            /// <param name="dictionary">The initial content.</param>
            public CovNullableValueDictionary( IDictionary<TKey, TValue?> dictionary )
                : base( dictionary )
            {
            }

            /// <summary>
            /// Initializes a new dictionary that contains elements copied from the specified collection
            /// and uses the default equality comparer for the key type.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            public CovNullableValueDictionary( IEnumerable<KeyValuePair<TKey, TValue?>> collection )
                : base( collection )
            {
            }

            /// <summary>
            /// Initializes a new empty Dictionary class that has the default initial capacity, and uses the
            /// comparer.
            /// </summary>
            /// <param name="comparer">
            /// The comparer implementation to use when comparing keys, or null to use the default
            /// comparer for the type of the key.
            /// </param>
            public CovNullableValueDictionary( IEqualityComparer<TKey>? comparer )
                : base( comparer )
            {
            }

            /// <summary>
            /// Initializes a new empty Dictionary that has the specified initial capacity, and uses the default equality
            /// comparer for the key type.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            public CovNullableValueDictionary( int capacity )
                : base( capacity )
            {
            }

            /// <summary>
            /// Initializes a new Dictionary that contains elements copied from the specified IDictionary
            /// and uses the specified comparer.
            /// </summary>
            /// <param name="dictionary">The initial content.</param>
            /// <param name="comparer">The comparer to use. Null to use the default comparer for the key type.</param>
            public CovNullableValueDictionary( IDictionary<TKey, TValue?> dictionary, IEqualityComparer<TKey>? comparer )
                : base( dictionary, comparer )
            {
            }

            /// <summary>
            /// Initializes a new Dictionary that contains elements copied from the specified collection
            /// and uses specified the comparer.
            /// </summary>
            /// <param name="collection">The initial content.</param>
            /// <param name="comparer">The comparer to use. Null to use the default comparer for the key type.</param>
            public CovNullableValueDictionary( IEnumerable<KeyValuePair<TKey, TValue?>> collection, IEqualityComparer<TKey>? comparer )
                : base( collection, comparer )
            {
            }

            /// <summary>
            /// Initializes a new empty of Dictionary that has the specified initial capacity, and uses the specified comparer.
            /// </summary>
            /// <param name="capacity">The initial capacity.</param>
            /// <param name="comparer">The comparer to use. Null to use the default comparer for the key type.</param>
            public CovNullableValueDictionary( int capacity, IEqualityComparer<TKey>? comparer )
                : base( capacity, comparer )
            {
            }

            object? IReadOnlyDictionary<TKey, object?>.this[TKey key] => this[key];

            IEnumerable<object?> IReadOnlyDictionary<TKey, object?>.Values => Values.Cast<object>();

            IEnumerable<TKey> IReadOnlyDictionary<TKey, object?>.Keys => Keys;

            bool IReadOnlyDictionary<TKey, object?>.TryGetValue( TKey key, out object? value )
            {
                if( TryGetValue( key, out var v ) )
                {
                    value = v;
                    return true;
                }
                value = null;
                return false;
            }

            IEnumerator<KeyValuePair<TKey, object?>> IEnumerable<KeyValuePair<TKey, object?>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TValue?>>)this).Select( kv => KeyValuePair.Create( kv.Key, (object?)kv.Value ) ).GetEnumerator();
            }
        }

    }
}
