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
                ref var n = ref _names[t.Index >> 1];
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

        /// <summary>
        /// Process a set of types across one or more maps (types must obviously belong to the maps) and
        /// with help of the <paramref name="ambuiguityResolver"/> provides a unique mapping
        /// from a name to a type or any object that may indicate a specific handling for the name.
        /// <para>
        /// This must be used if names from the map(s) can lead to ambiguities. This should be avoided
        /// if possible since resolving the ambiguity to a supported type may not be that easy.
        /// </para>
        /// </summary>
        /// <param name="source">
        /// The source set of type. Typically <see cref="AllExchangeableObliviousTypes"/> or <see cref="ExchangeableNonNullableTypes"/>:
        /// this depends on the reader implementation.
        /// </param>
        /// <param name="maps">One or more maps to unify.</param>
        /// <param name="ambuiguityResolver">
        /// A function that must choose one type or an object among the different possible types associated to the same name.
        /// </param>
        /// <returns>The mapping from name to type or object.</returns>
        public static IEnumerable<(string,object)> GetUnifiedIncomingMap( IEnumerable<IPocoType> source,
                                                                          IEnumerable<ExchangeableTypeNameMap> maps,
                                                                          Func<string, IReadOnlySet<IPocoType>, object> ambuiguityResolver )
        {
            var names = new Dictionary<string, object>();
            foreach( var t in source )
            {
                foreach( var map in maps )
                {
                    AddNamedTypes( names, t, map );
                }
            }
            foreach( var (name,o) in names )
            {
                if( o is IPocoType t ) yield return (name, t);
                else
                {
                    yield return (name, ambuiguityResolver( name, (IReadOnlySet<IPocoType>)o ));
                }
            }

            static void AddNamedTypes( Dictionary<string, object> names, IPocoType t, ExchangeableTypeNameMap map )
            {
                var n = map.GetName( t );
                if( n.IsExchangeable && n.Type == t )
                {
                    if( names.TryGetValue( n.Name, out var o ) )
                    {
                        if( o is IPocoType p )
                        {
                            if( p != t ) names[n.Name] = new HashSet<IPocoType>() { p, t };
                        }
                        else
                        {
                            ((HashSet<IPocoType>)o).Add( t );
                        }
                    }
                    else
                    {
                        names[n.Name] = t;
                    }
                }
            }
        }
    }
}
