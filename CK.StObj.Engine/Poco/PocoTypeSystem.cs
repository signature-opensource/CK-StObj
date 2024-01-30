using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    sealed class PocoTypeSystem : IPocoTypeSystem
    {
        readonly IPocoDirectory _pocoDirectory;
        readonly IReadOnlyList<IPocoType> _allTypes;
        readonly IReadOnlyList<IPocoType> _nonNullableTypes;
        readonly Dictionary<object, IPocoType> _typeCache;
        readonly Dictionary<Type, PocoType.PocoGenericTypeDefinition> _typeDefinitions;

        internal PocoTypeSystem( IPocoDirectory pocoDirectory,
                                 IReadOnlyList<IPocoType> allTypes,
                                 IReadOnlyList<IPocoType> nonNullableTypes,
                                 Dictionary<object, IPocoType> typeCache,
                                 Dictionary<Type, PocoType.PocoGenericTypeDefinition> typeDefinitions )
        {
            _pocoDirectory = pocoDirectory;
            _allTypes = allTypes;
            _nonNullableTypes = nonNullableTypes;
            _typeCache = typeCache;
            _typeDefinitions = typeDefinitions;
        }

        public IPocoDirectory PocoDirectory => _pocoDirectory;

        public IPocoType ObjectType => _allTypes[0];

        public IReadOnlyList<IPocoType> AllTypes => _allTypes;

        public IReadOnlyList<IPocoType> AllNonNullableTypes => _nonNullableTypes;

        public IPocoType? FindByType( Type type ) => _typeCache.GetValueOrDefault( type );

        public IPocoGenericTypeDefinition? FindGenericTypeDefinition( Type type ) => _typeDefinitions.GetValueOrDefault( type );

        public T? FindByType<T>( Type type ) where T: class, IPocoType => _typeCache.GetValueOrDefault( type ) as T;
    }

}
