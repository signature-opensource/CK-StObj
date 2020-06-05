using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace CK.Setup
{
    public static class DictionaryExtension
    {
        class ReadOnlyDictionaryWrapper<TKey, TValue, TReadOnlyValue> : IReadOnlyDictionary<TKey, TReadOnlyValue> where TValue : TReadOnlyValue where TKey : notnull
        {
            private IDictionary<TKey, TValue> _dictionary;

            public ReadOnlyDictionaryWrapper( IDictionary<TKey, TValue> dictionary )
            {
                if( dictionary == null ) throw new ArgumentNullException( "dictionary" );
                _dictionary = dictionary;
            }
            public bool ContainsKey( TKey key ) { return _dictionary.ContainsKey( key ); }

            public IEnumerable<TKey> Keys { get { return _dictionary.Keys; } }

            public bool TryGetValue( TKey key, out TReadOnlyValue value )
            {
                var r = _dictionary.TryGetValue( key, out var v );
                value = v;
                return r;
            }

            public IEnumerable<TReadOnlyValue> Values => _dictionary.Values.Cast<TReadOnlyValue>();

            public TReadOnlyValue this[TKey key] => _dictionary[key];

            public int Count => _dictionary.Count;

            public IEnumerator<KeyValuePair<TKey, TReadOnlyValue>> GetEnumerator() => _dictionary.Select( x => new KeyValuePair<TKey, TReadOnlyValue>( x.Key, x.Value ) ).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public static IReadOnlyDictionary<TKey, TReadOnlyValue> AsCovariantReadOnly<TKey, TValue, TReadOnlyValue>( this IDictionary<TKey,TValue> @this )
            where TKey : notnull
            where TValue : TReadOnlyValue
        {
            return new ReadOnlyDictionaryWrapper<TKey, TValue, TReadOnlyValue>( @this );
        }
    }
}
