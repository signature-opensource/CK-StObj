using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Setup
{
    partial class StObjObjectEngineMap
    {
        IStObjServiceMap IStObjMap.Services => this;

        public IStObjServiceEngineMap Services => this;

        #region Service to Object mappings.

        readonly Dictionary<Type,MutableItem> _serviceToObjectMap;
        readonly List<MutableItem> _serviceRealObjects;
        readonly IReadOnlyDictionary<Type, IStObjFinalImplementation> _serviceToObjectMapExposed;

        internal void RegisterServiceFinalObjectMapping( Type t, CKTypeInfo typeInfo )
        {
            Debug.Assert( typeInfo is RealObjectClassInfo );
            var c = (RealObjectClassInfo)typeInfo;
            MutableItem mapping = c.AutoServiceImpl;
            if( mapping == null )
            {
                c.AutoServiceImpl = mapping = _map[typeInfo.Type];
                _serviceRealObjects.Add( mapping );
            }
            _serviceToObjectMap.Add( t, mapping );
        }

        public IReadOnlyDictionary<Type, IStObjFinalImplementation> ObjectMappings => _serviceToObjectMapExposed;

        public IReadOnlyList<IStObjFinalImplementation> ObjectMappingList => _serviceRealObjects;

        #endregion

        #region Service to Type mappings (Simple).

        readonly Dictionary<Type, IStObjServiceFinalSimpleMapping> _serviceSimpleMap;
        readonly List<IStObjServiceFinalSimpleMapping> _serviceSimpleList;
        readonly IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> _exposedServiceMap;

        class ServiceMapTypeAdapter : IReadOnlyDictionary<Type, IStObjServiceClassDescriptor>
        {
            readonly Dictionary<Type, IStObjServiceFinalSimpleMapping> _map;

            public ServiceMapTypeAdapter( Dictionary<Type, IStObjServiceFinalSimpleMapping> map )
            {
                _map = map;
            }

            public IStObjServiceClassDescriptor this[Type key] => _map[key];

            public IEnumerable<Type> Keys => _map.Keys;

            public IEnumerable<IStObjServiceClassDescriptor> Values => _map.Values;

            public int Count => _map.Count;

            public bool ContainsKey( Type key ) => _map.ContainsKey( key );

            public IEnumerator<KeyValuePair<Type, IStObjServiceClassDescriptor>> GetEnumerator()
            {
                return _map.Select( kv => new KeyValuePair<Type, IStObjServiceClassDescriptor>( kv.Key, kv.Value ) ).GetEnumerator();
            }

            public bool TryGetValue( Type key, [MaybeNullWhen(false)]out IStObjServiceClassDescriptor value )
            {
                value = null;
                if( !_map.TryGetValue( key, out var c ) ) return false;
                value = c;
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<Type, IStObjServiceFinalSimpleMapping> SimpleMappings => _serviceSimpleMap;

        /// <inheritdoc />
        public IReadOnlyList<IStObjServiceFinalSimpleMapping> SimpleMappingList => _serviceSimpleList;

        internal void RegisterFinalSimpleMapping( Type t, AutoServiceClassInfo c )
        {
            if( c.SimpleMappingListIndex == -1 )
            {
                c.SimpleMappingListIndex = _serviceSimpleList.Count;
                _serviceSimpleList.Add( c );
            }
            _serviceSimpleMap.Add( t, c );
        }

        IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappings => _exposedServiceMap;

        IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappingList => _serviceSimpleList;

        #endregion

        #region Service Manual mappings.

        class StObjServiceFinalManualMapping : IStObjServiceFinalManualMapping
        {
            readonly IStObjServiceClassFactoryInfo _c;

            public StObjServiceFinalManualMapping( int number, IStObjServiceClassFactoryInfo c )
            {
                ManualMappingIndex = number;
                _c = c;
            }

            public int ManualMappingIndex { get; }

            public Type ClassType => _c.ClassType;

            public Type FinalType => _c.FinalType;

            public bool IsScoped => _c.IsScoped;

            public AutoServiceKind AutoServiceKind => _c.AutoServiceKind;

            public IReadOnlyList<IStObjServiceParameterInfo> Assignments => _c.Assignments;

            public IReadOnlyCollection<Type> MarshallableTypes => _c.MarshallableTypes;

            public IReadOnlyCollection<Type> MultipleMappings => _c.MultipleMappings;

            public IReadOnlyCollection<Type> UniqueMappings => _c.UniqueMappings;

            public object CreateInstance( IServiceProvider provider )
            {
                return Create( provider, this, new Dictionary<IStObjServiceClassFactoryInfo, object>() );
            }

            static object Create( IServiceProvider provider, IStObjServiceClassFactoryInfo c, Dictionary<IStObjServiceClassFactoryInfo, object> cache )
            {
                if( !cache.TryGetValue( c, out var result ) )
                {
                    var ctor = c.GetSingleConstructor();
                    var parameters = ctor.GetParameters();
                    var values = new object?[parameters.Length];
                    for( int i = 0; i < parameters.Length; ++i )
                    {
                        var p = parameters[i];
                        var mapped = c.Assignments.Where( a => a.Position == p.Position ).FirstOrDefault();
                        if( mapped == null )
                        {
                            values[i] = provider.GetService( p.ParameterType );
                        }
                        else
                        {
                            if( mapped.Value == null )
                            {
                                values[i] = null;
                            }
                            else if( mapped.IsEnumerated )
                            {
                                values[i] = mapped.Value.Select( v => provider.GetService( v ) ).ToArray();
                            }
                            else
                            {
                                values[i] = provider.GetService( mapped.Value[0] );
                            }
                        }
                    }
                    result = ctor.Invoke( values );
                    cache.Add( c, result );
                }
                return result;
            }
        }

        readonly Dictionary<Type, IStObjServiceFinalManualMapping> _serviceManualMap;
        readonly List<IStObjServiceFinalManualMapping> _serviceManualList;
        readonly IReadOnlyDictionary<Type, IStObjServiceClassFactory> _exposedManualServiceMap;

        class ServiceManualMapTypeAdapter : IReadOnlyDictionary<Type, IStObjServiceClassFactory>
        {
            readonly Dictionary<Type, IStObjServiceFinalManualMapping> _map;

            public ServiceManualMapTypeAdapter( Dictionary<Type, IStObjServiceFinalManualMapping> map )
            {
                _map = map;
            }

            public IStObjServiceClassFactory this[Type key] => _map[key];

            public IEnumerable<Type> Keys => _map.Keys;

            public IEnumerable<IStObjServiceClassFactory> Values => _map.Values;

            public int Count => _map.Count;

            public bool ContainsKey( Type key ) => _map.ContainsKey( key );

            public IEnumerator<KeyValuePair<Type, IStObjServiceClassFactory>> GetEnumerator()
            {
                return _map.Select( kv => new KeyValuePair<Type, IStObjServiceClassFactory>( kv.Key, kv.Value ) ).GetEnumerator();
            }

            public bool TryGetValue( Type key, [MaybeNullWhen(false)]out IStObjServiceClassFactory value )
            {
                value = null;
                if( !_map.TryGetValue( key, out var c ) ) return false;
                value = c;
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<Type, IStObjServiceFinalManualMapping> ManualMappings => _serviceManualMap;

        /// <inheritdoc/>
        public IReadOnlyList<IStObjServiceFinalManualMapping> ManualMappingList => _serviceManualList;

        internal IStObjServiceFinalManualMapping CreateServiceFinalManualMapping( IStObjServiceClassFactoryInfo c )
        {
            Debug.Assert( _serviceManualList.IndexOf( x => x.ClassType == c.ClassType ) < 0, "Unique registration check must be done by the caller." );
            var r = new StObjServiceFinalManualMapping( _serviceManualList.Count, c );
            _serviceManualList.Add( r );
            return r;
        }

        internal void RegisterServiceFinalManualMapping( Type t, IStObjServiceFinalManualMapping mapping ) => _serviceManualMap.Add( t, mapping );

        IReadOnlyDictionary<Type, IStObjServiceClassFactory> IStObjServiceMap.ManualMappings => _exposedManualServiceMap;

        IReadOnlyList<IStObjServiceClassFactory> IStObjServiceMap.ManualMappingList => _serviceManualList;

        #endregion

    }
}
