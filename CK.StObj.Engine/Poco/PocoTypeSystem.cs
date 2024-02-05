using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    sealed partial class PocoTypeSystem : IPocoTypeSystem
    {
        readonly IPocoDirectory _pocoDirectory;
        readonly IReadOnlyList<IPocoType> _allTypes;
        readonly IReadOnlyList<IPocoType> _nonNullableTypes;
        readonly Dictionary<object, IPocoType> _typeCache;
        readonly Dictionary<Type, PocoType.PocoGenericTypeDefinition> _typeDefinitions;
        readonly IPocoTypeSet _noneTypeSet;
        readonly IPocoTypeSet _noneSerializableTypeSet;
        readonly IPocoTypeSet _noneExchangableTypeSet;
        readonly IPocoTypeSet _allTypeFilter;
        readonly IPocoTypeSet _allSerializableTypeSet;
        readonly IPocoTypeSet _allExchangeableTypeSet;

        internal PocoTypeSystem( IPocoDirectory pocoDirectory,
                                 IReadOnlyList<IPocoType> allTypes,
                                 IReadOnlyList<IPocoType> nonNullableTypes,
                                 Dictionary<object, IPocoType> typeCache,
                                 Dictionary<Type, PocoType.PocoGenericTypeDefinition> typeDefinitions,
                                 HashSet<IPocoType>? nonSerialized,
                                 HashSet<IPocoType>? notExchangeable )
        {
            _pocoDirectory = pocoDirectory;
            _allTypes = allTypes;
            _nonNullableTypes = nonNullableTypes;
            _typeCache = typeCache;
            _typeDefinitions = typeDefinitions;
            // Initializes the None and Root.
            _noneTypeSet = new RootNone( this, true, true, true, NoPocoFilter );
            _allTypeFilter = new RootAll( this );

            // First handles the Serializable sets.
            if( nonSerialized == null )
            {
                // If there's no type marked as NonSerialized, the 2 sets are the same as the None and All.
                _allSerializableTypeSet = _allTypeFilter;
                _noneSerializableTypeSet = _noneTypeSet;
            }
            else
            {
                // First we build the set of all serializable types.
                _allSerializableTypeSet = _allTypeFilter.Exclude( nonSerialized );
                // Then we use it as the low level filter of the none (but serializable) set.
                _noneSerializableTypeSet = new RootNone( this, true, true, true, _allSerializableTypeSet.Contains );
            }
            // Then handles the Exchangeable sets (Exchangeable => Serializable).
            if( notExchangeable == null )
            {
                // If there's no type marked as NotEchangeable, the 2 sets are the same as the Serializable ones.
                _allExchangeableTypeSet = _allSerializableTypeSet;
                _noneExchangableTypeSet = _noneSerializableTypeSet;
            }
            else
            {
                // First we build the set of all exchangeable types on the basis on the AllSerializable one.
                _allExchangeableTypeSet = _allSerializableTypeSet.Exclude( notExchangeable );
                // Then we use it as the low level filter of the none (but exchangeable) set.
                _noneExchangableTypeSet = new RootNone( this, true, true, true, _allExchangeableTypeSet.Contains );
            }
        }

        public IPocoDirectory PocoDirectory => _pocoDirectory;

        public IPocoType ObjectType => _allTypes[0];

        public IReadOnlyList<IPocoType> AllTypes => _allTypes;

        public IReadOnlyList<IPocoType> AllNonNullableTypes => _nonNullableTypes;

        public IPocoType? FindByType( Type type ) => _typeCache.GetValueOrDefault( type );

        public IPocoGenericTypeDefinition? FindGenericTypeDefinition( Type type ) => _typeDefinitions.GetValueOrDefault( type );

        public T? FindByType<T>( Type type ) where T: class, IPocoType => _typeCache.GetValueOrDefault( type ) as T;

        public IPocoTypeSetManager SetManager => this;
    }
}
