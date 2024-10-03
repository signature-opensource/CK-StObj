using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Setup;

sealed partial class StObjObjectEngineMap
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

    IStObjFinalClass? IStObjServiceMap.ToLeaf( Type t )
    {
        if( _serviceSimpleMap.TryGetValue( t, out var service ) ) return service;
        return _serviceToObjectMap.TryGetValue( t, out var realObject ) ? realObject : null;
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
    public IReadOnlyDictionary<Type, IStObjServiceFinalSimpleMapping> Mappings => _serviceSimpleMap;

    /// <inheritdoc />
    public IReadOnlyList<IStObjServiceFinalSimpleMapping> MappingList => _serviceSimpleList;

    internal void RegisterFinalSimpleMapping( Type t, AutoServiceClassInfo c )
    {
        if( c.MappingListIndex == -1 )
        {
            c.MappingListIndex = _serviceSimpleList.Count;
            _serviceSimpleList.Add( c );
        }
        _serviceSimpleMap.Add( t, c );
    }

    IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.Mappings => _exposedServiceMap;

    IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.MappingList => _serviceSimpleList;

    #endregion

}
