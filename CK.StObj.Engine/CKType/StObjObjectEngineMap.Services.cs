using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    partial class StObjObjectEngineMap
    {
        readonly CKTypeKindDetector _typeKindDetector;

        IStObjServiceMap IStObjMap.Services => this;


        #region Service Manual mappings.

        readonly Dictionary<Type, IStObjServiceFinalManualMapping> _serviceManualMap;
        readonly List<IStObjServiceFinalManualMapping> _serviceManualList;
        readonly ServiceManualMapTypeAdapter _exposedManualServiceMap;

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

            public bool TryGetValue( Type key, out IStObjServiceClassFactory value )
            {
                value = null;
                if( !_map.TryGetValue( key, out var c ) ) return false;
                value = c;
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal Dictionary<Type, IStObjServiceFinalManualMapping> ServiceManualMappings => _serviceManualMap;

        internal IReadOnlyList<IStObjServiceFinalManualMapping> ServiceManualList => _serviceManualList;

        class StObjServiceFinalManualMapping : IStObjServiceFinalManualMapping
        {
            readonly IStObjServiceClassFactoryInfo _c;

            public StObjServiceFinalManualMapping( int number, IStObjServiceClassFactoryInfo c )
            {
                Number = number;
                _c = c;
            }

            public int Number { get; }

            public Type ClassType => _c.ClassType;

            public bool IsScoped => _c.IsScoped;

            public IReadOnlyList<IStObjServiceParameterInfo> Assignments => _c.Assignments;

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
                    var values = new object[parameters.Length];
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

        internal IStObjServiceFinalManualMapping CreateServiceFinalManualMapping( IStObjServiceClassFactoryInfo c )
        {
            var r = new StObjServiceFinalManualMapping( _serviceManualList.Count + 1, c );
            _serviceManualList.Add( r );
            return r;
        }
        IReadOnlyDictionary<Type, IStObjServiceClassFactory> IStObjServiceMap.ManualMappings => _exposedManualServiceMap;

        #endregion

        #region Service to Object mappings.

        readonly Dictionary<Type,MutableItem> _serviceToObjectMap;
        readonly ServiceObjectMappingTypeAdapter _serviceToObjectMapExposed;

        internal void RegisterServiceFinalObjectMapping( Type t, CKTypeInfo typeInfo )
        {
            Debug.Assert( typeInfo is RealObjectClassInfo );
            _serviceToObjectMap.Add( t, _map[typeInfo.Type] );
        }

        class ServiceObjectMappingTypeAdapter : IReadOnlyDictionary<Type, object>
        {
            readonly Dictionary<Type, MutableItem> _map;

            public ServiceObjectMappingTypeAdapter( Dictionary<Type, MutableItem> map )
            {
                _map = map;
            }

            public object this[Type key] => _map.GetValueWithDefault( key, null )?.InitialObject;

            public IEnumerable<Type> Keys => _map.Keys;

            public IEnumerable<object> Values => _map.Values.Select( m => m.InitialObject );

            public int Count => _map.Count;

            public bool ContainsKey( Type key ) => _map.ContainsKey( key );

            public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _map.Select( kv => new KeyValuePair<Type, object>( kv.Key, kv.Value.InitialObject ) ).GetEnumerator(); 

            public bool TryGetValue( Type key, out object value )
            {
                if( _map.TryGetValue( key, out var m ) )
                {
                    value = m.InitialObject;
                    return true;
                }
                value = null;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        IReadOnlyDictionary<Type, object> IStObjServiceMap.ObjectMappings => _serviceToObjectMapExposed;

        // Direct access for code generation.
        internal IReadOnlyCollection<KeyValuePair<Type,MutableItem>> ObjectMappings => _serviceToObjectMap;

        #endregion

        #region Service to Type mappings (Simple).

        readonly Dictionary<Type, AutoServiceClassInfo> _serviceMap;
        readonly ServiceMapTypeAdapter _exposedServiceMap;

        class ServiceMapTypeAdapter : IReadOnlyDictionary<Type, IStObjServiceClassDescriptor>
        {
            readonly Dictionary<Type, AutoServiceClassInfo> _map;

            public ServiceMapTypeAdapter( Dictionary<Type, AutoServiceClassInfo> map )
            {
                _map = map;
            }

            public IStObjServiceClassDescriptor this[Type key]
            {
                get
                {
                    _map.TryGetValue( key, out var c );
                    return c;
                }
            }
            public IEnumerable<Type> Keys => _map.Keys;

            public IEnumerable<IStObjServiceClassDescriptor> Values => _map.Values;

            public int Count => _map.Count;

            public bool ContainsKey( Type key ) => _map.ContainsKey( key );

            public IEnumerator<KeyValuePair<Type, IStObjServiceClassDescriptor>> GetEnumerator()
            {
                return _map.Select( kv => new KeyValuePair<Type, IStObjServiceClassDescriptor>( kv.Key, kv.Value ) ).GetEnumerator();
            }

            public bool TryGetValue( Type key, out IStObjServiceClassDescriptor value )
            {
                value = null;
                if( !_map.TryGetValue( key, out var c ) ) return false;
                value = c;
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Direct access to the mutable service mapping.
        /// </summary>
        internal Dictionary<Type, AutoServiceClassInfo> ServiceSimpleMappings => _serviceMap;

        IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappings => _exposedServiceMap;

        #endregion

    }
}
