using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    public class ExchangeableTypeNameMap
    {
        readonly FullExchangeableTypeName[] _names;
        readonly IPocoType[] _exchangeables;

        /// <summary>
        /// Initializes a map from the result of a <see cref="ExchangeableTypeNameBuilder.Generate(IActivityMonitor, IPocoTypeSystem)"/>
        /// </summary>
        /// <param name="names">The full type names.</param>
        /// <param name="typeSystem">The type system.</param>
        /// <param name="exchangeableCount">The number of exchangeable types.</param>
        public ExchangeableTypeNameMap( FullExchangeableTypeName[] names,
                                        IPocoTypeSystem typeSystem,
                                        int exchangeableCount )
        {
            _names = names;
            TypeSystem = typeSystem;
            _exchangeables = new IPocoType[exchangeableCount];
            int idx = 0;
            foreach( var t in typeSystem.AllNonNullableTypes )
            {
                var n = _names[t.Index >> 1];
                if( n.IsExchangeable )
                {
                    _exchangeables[idx] = t;
                    if( ++idx > exchangeableCount ) break;
                }
            }
            Throw.CheckArgument( idx == exchangeableCount );
        }

        /// <summary>
        /// Gets the type system.
        /// </summary>
        public IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets the non nullable types that are exchangeable.
        /// </summary>
        public IReadOnlyList<IPocoType> ExchangeableNonNullableTypes => _exchangeables;

        /// <summary>
        /// Gets all the types that are exchangeable.
        /// </summary>
        public IEnumerable<IPocoType> AllExchangeableTypes
        {
            get
            {
                foreach( var t in _exchangeables )
                {
                    yield return t;
                    yield return t.Nullable;
                }
            }
        }

        /// <summary>
        /// Gets all the oblivious types that are exchangeable (they can be nullable and non nullable).
        /// </summary>
        public IEnumerable<IPocoType> AllExchangeableObliviousTypes => AllExchangeableTypes.Where( t => t.IsOblivious );

        /// <summary>
        /// Gets all the non nullable oblivious types that are exchangeable.
        /// <para>
        /// This is often the set of types that must be handled by a serializer as long as anonymous records
        /// field names must not be specified.
        /// </para>
        /// </summary>
        public IEnumerable<IPocoType> ExchangeableNonNullableObliviousTypes => _exchangeables.Where( t => t.IsOblivious );

        /// <summary>
        /// Gets the exchangeable type name (that may be not <see cref="ExchangeableTypeName.IsExchangeable"/>)
        /// for a type. This returns the nullable or non nullable names.
        /// </summary>
        /// <param name="t">The type to lookup.</param>
        /// <returns>The exchangeable name.</returns>
        public ExchangeableTypeName GetName( IPocoType t ) => t.IsNullable
                                                                ? _names[t.Index >> 1].Nullable
                                                                : _names[t.Index >> 1].NonNullable;

        /// <summary>
        /// Gets whether a type is exchangeable.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>True if the type must be exchanged, false otherwise.</returns>
        public bool IsExchangeable( IPocoType t ) => _names[t.Index >> 1].IsExchangeable;

        /// <summary>
        /// Gets all the exchanged names.
        /// </summary>
        public IReadOnlyList<FullExchangeableTypeName> AllFullNames => _names;
    }
}
