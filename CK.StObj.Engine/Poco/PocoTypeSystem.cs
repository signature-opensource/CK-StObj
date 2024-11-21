using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CK.Setup;

sealed partial class PocoTypeSystem : IPocoTypeSystem
{
    readonly IPocoDirectory _pocoDirectory;
    readonly IReadOnlyList<IPocoType> _allTypes;
    readonly IReadOnlyList<IPocoType> _nonNullableTypes;
    readonly Dictionary<object, IPocoType> _typeCache;
    readonly Dictionary<Type, PocoType.PocoGenericTypeDefinition> _typeDefinitions;
    readonly ImmutableArray<IPocoType> _finalTypes;
    readonly IPocoTypeSet _emptyTypeSet;
    readonly IPocoTypeSet _emptySerializableTypeSet;
    readonly IPocoTypeSet _emptyExchangableTypeSet;
    readonly IPocoTypeSet _allTypeSet;
    readonly IPocoTypeSet _allSerializableTypeSet;
    readonly IPocoTypeSet _allExchangeableTypeSet;
    // Only allocates a stupid array of 0 if required by GetFlagsArray
    // from any empty set.
    PocoTypeRawSet? _zeros;

    internal PocoTypeSystem( IPocoDirectory pocoDirectory,
                             IReadOnlyList<IPocoType> allTypes,
                             IReadOnlyList<IPocoType> nonNullableTypes,
                             ImmutableArray<IPocoType> finalTypes,
                             Dictionary<object, IPocoType> typeCache,
                             Dictionary<Type, PocoType.PocoGenericTypeDefinition> typeDefinitions,
                             HashSet<IPocoType>? notSerializable,
                             HashSet<IPocoType>? notExchangeable )
    {
        _pocoDirectory = pocoDirectory;
        _allTypes = allTypes;
        _nonNullableTypes = nonNullableTypes;
        _typeCache = typeCache;
        _typeDefinitions = typeDefinitions;
        _finalTypes = finalTypes;
        // Initializes the None and Root.
        _emptyTypeSet = new RootNone( this, true, true, true, ImplementationLessFilter );
        // The All is built with the Excluder. This is NOT overkill:
        // the initialization of the Excluder takes care of abstract poco without
        // implementations and propagate this to the collections that reference them.
        var all = new PocoTypeRawSet( this, all: true );
        Throw.DebugAssert( all.OfType<IRecordPocoType>().SelectMany( r => r.Fields ).All( f => f.Owner != null ) );
        var e = new Excluder( all, true, true, ImplementationLessFilter );
        _allTypeSet = new TypeSet( all, allowEmptyRecords: true, allowEmptyPocos: true, autoIncludeCollections: true, ImplementationLessFilter );

        // First handles the Serializable sets.
        if( notSerializable == null )
        {
            // If there's no type marked as NotSerializable, the 2 sets are the same as the All and Empty.
            _allSerializableTypeSet = _allTypeSet;
            _emptySerializableTypeSet = _emptyTypeSet;
        }
        else
        {
            // First we build the set of all serializable types.
            _allSerializableTypeSet = _allTypeSet.Exclude( notSerializable );
            // Then we use it as the low level filter of the empty (but serializable) set.
            _emptySerializableTypeSet = new RootNone( this, true, true, true, _allSerializableTypeSet.Contains );
        }
        // Then handles the Exchangeable sets (Exchangeable => Serializable).
        if( notExchangeable == null )
        {
            // If there's no type marked as NotExchangeable, the 2 sets are the same as the Serializable ones.
            _allExchangeableTypeSet = _allSerializableTypeSet;
            _emptyExchangableTypeSet = _emptySerializableTypeSet;
        }
        else
        {
            // First we build the set of all exchangeable types on the basis on the AllSerializable one.
            _allExchangeableTypeSet = _allSerializableTypeSet.Exclude( notExchangeable );
            // Then we use it as the low level filter of the empty (but exchangeable) set.
            _emptyExchangableTypeSet = new RootNone( this, true, true, true, _allExchangeableTypeSet.Contains );
        }
    }

    public IPocoDirectory PocoDirectory => _pocoDirectory;

    public IReadOnlyList<IPocoType> AllTypes => _allTypes;

    public IReadOnlyList<IPocoType> AllNonNullableTypes => _nonNullableTypes;

    public IReadOnlyCollection<IPocoType> NonNullableFinalTypes => _finalTypes;

    public IPocoType? FindByType( Type type ) => _typeCache.GetValueOrDefault( type );

    public IPocoGenericTypeDefinition? FindGenericTypeDefinition( Type type ) => _typeDefinitions.GetValueOrDefault( type );

    public T? FindByType<T>( Type type ) where T : class, IPocoType => _typeCache.GetValueOrDefault( type ) as T;

    public IPocoTypeSetManager SetManager => this;
}
