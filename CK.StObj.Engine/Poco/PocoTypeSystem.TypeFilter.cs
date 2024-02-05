using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CK.Setup
{
    sealed partial class PocoTypeSystem : IPocoTypeSetManager
    {
        public IPocoTypeSet None => _noneTypeSet;

        public IPocoTypeSet NoneSerializable => _noneSerializableTypeSet;

        public IPocoTypeSet NoneExchangeable => _noneExchangableTypeSet;

        public IPocoTypeSet AllExchangeable => _allExchangeableTypeSet;

        public IPocoTypeSet AllSerializable => _allSerializableTypeSet;

        public IPocoTypeSet All => _allTypeFilter;

        public IPocoTypeSet CreateNone( bool allowEmptyRecords,
                                        bool allowEmptyPocos,
                                        bool autoIncludeCollections,
                                        Func<IPocoType, bool> lowLevelFilter )
        {
            Throw.CheckNotNullArgument( lowLevelFilter );
            return new RootNone( this, allowEmptyRecords, allowEmptyPocos, autoIncludeCollections, lowLevelFilter );
        }

        public IPocoTypeSet CreateAll( bool allowEmptyRecords, bool allowEmptyPocos, bool autoIncludeCollections, Func<IPocoType, bool> lowLevelFilter )
        {
            Throw.CheckNotNullArgument( lowLevelFilter );
            return CreateTypeSet( new PocoTypeRawSet( this, true ),
                                  allowEmptyRecords,
                                  allowEmptyPocos,
                                  allowedTypes: null,
                                  excludedTypes: null,
                                  autoIncludeCollections,
                                  withAbstractReadOnlyFieldTypes: false,
                                  lowLevelFilter );
        }

        static bool NoPocoFilter( IPocoType t ) => true;

        sealed class RootNone : IPocoTypeSet
        {
            readonly IPocoTypeSystem _typeSystem;
            readonly Func<IPocoType, bool> _lowLevelFilter;
            readonly bool _allowEmptyRecords;
            readonly bool _allowEmptyPocos;
            readonly bool _autoIncludeCollections;

            internal RootNone( IPocoTypeSystem typeSystem,
                               bool allowEmptyRecords,
                               bool allowEmptyPocos,
                               bool autoIncludeCollections,
                               Func<IPocoType, bool> lowLevelFilter )
            {
                _typeSystem = typeSystem;
                _lowLevelFilter = lowLevelFilter;
                _allowEmptyRecords = allowEmptyRecords;
                _allowEmptyPocos = allowEmptyPocos;
                _autoIncludeCollections = autoIncludeCollections;
            }

            public IPocoTypeSystem TypeSystem => _typeSystem;

            public bool AllowEmptyRecords => _allowEmptyRecords;

            public bool AllowEmptyPocos => _allowEmptyPocos;

            public bool AutoIncludeCollections => _autoIncludeCollections;

            public bool SameContentAs( IPocoTypeSet other )
            {
                Throw.CheckNotNullArgument( other );
                Throw.CheckArgument( TypeSystem == other.TypeSystem );
                return other.NonNullableTypes.Count == 0;
            }

            public bool Contains( IPocoType t ) => false;

            public IReadOnlyCollection<IPocoType> NonNullableTypes => Array.Empty<IPocoType>();

            public Func<IPocoType, bool> LowLevelFilter => _lowLevelFilter;

            public IPocoTypeSet Include( IEnumerable<IPocoType> types, bool withAbstractReadOnlyFieldTypes = false )
            {
                if( !types.Any() ) return this;
                // The working set is a new empty raw set.
                return CreateTypeSet( workingSet: new PocoTypeRawSet( _typeSystem ),
                                      _allowEmptyRecords,
                                      _allowEmptyPocos,
                                      allowedTypes: types,
                                      excludedTypes: null,
                                      _autoIncludeCollections,
                                      withAbstractReadOnlyFieldTypes,
                                      _lowLevelFilter );
            }

            public IPocoTypeSet Exclude( IEnumerable<IPocoType> disallowedTypes ) => this;

            public IPocoTypeSet ExcludeEmptyRecords()
            {
                return _allowEmptyRecords
                            ? new RootNone( _typeSystem, false, _allowEmptyPocos, _autoIncludeCollections, _lowLevelFilter )
                            : this;
            }

            public IPocoTypeSet ExcludeEmptyPocos()
            {
                return _allowEmptyPocos
                            ? new RootNone( _typeSystem, _allowEmptyRecords, false, _autoIncludeCollections, _lowLevelFilter )
                            : this;
            }

            public IPocoTypeSet ExcludeEmptyRecordsAndPocos()
            {
                return _allowEmptyRecords || _allowEmptyPocos
                            ? new RootNone( _typeSystem, false, false, _autoIncludeCollections, _lowLevelFilter )
                            : this;
            }
        }

        // RootAll is basically a wrapper around the TypeSystem.AllNonNullableTypes, there is
        // no low level filter, AllowEmptyRecords, AllowEmptyPocos are AutoIncludeCollections are true. 
        sealed class RootAll : IPocoTypeSet
        {
            readonly IPocoTypeSystem _typeSystem;

            internal RootAll( IPocoTypeSystem typeSystem )
            {
                _typeSystem = typeSystem;
            }

            public IPocoTypeSystem TypeSystem => _typeSystem;

            public bool AllowEmptyRecords => true;

            public bool AllowEmptyPocos => true;

            public bool AutoIncludeCollections => true;

            public bool SameContentAs( IPocoTypeSet other )
            {
                Throw.CheckNotNullArgument( other );
                Throw.CheckArgument( TypeSystem == other.TypeSystem );
                return other.NonNullableTypes.Count == _typeSystem.AllNonNullableTypes.Count;
            }

            public bool Contains( IPocoType t ) => true;

            public IReadOnlyCollection<IPocoType> NonNullableTypes => _typeSystem.AllNonNullableTypes;

            public IPocoTypeSet Include( IEnumerable<IPocoType> types, bool withAbstractReadOnlyFieldTypes = false )
            {
                return this;
            }

            public IPocoTypeSet Exclude( IEnumerable<IPocoType> disallowedTypes )
            {
                if( !disallowedTypes.Any() ) return this;
                return CreateTypeSet( new PocoTypeRawSet( _typeSystem, true ),
                                      allowEmptyRecords: true,
                                      allowEmptyPocos: true,
                                      allowedTypes: null,
                                      disallowedTypes,
                                      autoIncludeCollections: true,
                                      withAbstractReadOnlyFieldTypes: false,
                                      NoPocoFilter );
            }

            public IPocoTypeSet ExcludeEmptyRecords()
            {
                return CreateTypeSet( new PocoTypeRawSet( _typeSystem, true ),
                                      allowEmptyRecords: false,
                                      allowEmptyPocos: true,
                                      allowedTypes: null,
                                      excludedTypes: null,
                                      autoIncludeCollections: true,
                                      withAbstractReadOnlyFieldTypes: false,
                                      NoPocoFilter );
            }

            public IPocoTypeSet ExcludeEmptyPocos()
            {
                return CreateTypeSet( new PocoTypeRawSet( _typeSystem, true ),
                                      allowEmptyRecords: true,
                                      allowEmptyPocos: false,
                                      allowedTypes: null,
                                      excludedTypes: null,
                                      autoIncludeCollections: true,
                                      withAbstractReadOnlyFieldTypes: false,
                                      NoPocoFilter );
            }

            public IPocoTypeSet ExcludeEmptyRecordsAndPocos()
            {
                return CreateTypeSet( new PocoTypeRawSet( _typeSystem, true ),
                                      allowEmptyRecords: false,
                                      allowEmptyPocos: false,
                                      allowedTypes: null,
                                      excludedTypes: null,
                                      autoIncludeCollections: true,
                                      withAbstractReadOnlyFieldTypes: false,
                                      NoPocoFilter );
            }

        }

        internal sealed class TypeSet : IPocoTypeSet
        {
            readonly PocoTypeRawSet _raw;
            readonly Func<IPocoType, bool> _lowLevelFilter;
            readonly bool _autoIncludeCollections;
            readonly bool _allowEmptyRecords;
            readonly bool _allowEmptyPocos;

            internal TypeSet( PocoTypeRawSet raw,
                              bool allowEmptyRecords,
                              bool allowEmptyPocos,
                              bool autoIncludeCollections,
                              Func<IPocoType,bool> lowLevelFilter )
            {
                _raw = raw;
                _autoIncludeCollections = autoIncludeCollections;
                _lowLevelFilter = lowLevelFilter;
                _allowEmptyRecords = allowEmptyRecords;
                _allowEmptyPocos = allowEmptyPocos;
                CheckTypeSetRules( this );
            }

            public IPocoTypeSystem TypeSystem => _raw.TypeSystem;

            public bool AllowEmptyRecords => _allowEmptyRecords;

            public bool AllowEmptyPocos => _allowEmptyPocos;

            public bool AutoIncludeCollections => _autoIncludeCollections;

            public bool SameContentAs( IPocoTypeSet other )
            {
                Throw.CheckNotNullArgument( other );
                Throw.CheckArgument( TypeSystem == other.TypeSystem );
                return other is TypeSet s
                        ? _raw.SameContentAs( s._raw )
                        : _raw.Count == other.NonNullableTypes.Count;
            }

            public IReadOnlyCollection<IPocoType> NonNullableTypes => _raw;

            public bool Contains( IPocoType t ) => _raw.Contains( t );

            public IPocoTypeSet Include( IEnumerable<IPocoType> types, bool withAbstractReadOnlyFieldTypes = false )
            {
                // If withAbstractReadOnlyFieldTypes is true, we don't take any risk and compute the set:
                // AbstractReadOnly fields of existing types may be followed.
                if( !withAbstractReadOnlyFieldTypes && types.All( _raw.Contains ) ) return this;
                return CreateTypeSet( _raw.Clone(),
                                      _allowEmptyRecords,
                                      _allowEmptyPocos,
                                      allowedTypes: types,
                                      excludedTypes: null,
                                      _autoIncludeCollections,
                                      withAbstractReadOnlyFieldTypes,
                                      _lowLevelFilter );
            }

            public IPocoTypeSet Exclude( IEnumerable<IPocoType> disallowedTypes )
            {
                if( !disallowedTypes.Any( _raw.Contains ) ) return this;
                return CreateTypeSet( _raw.Clone(),
                                      _allowEmptyRecords,
                                      _allowEmptyPocos,
                                      allowedTypes: null,
                                      excludedTypes: disallowedTypes,
                                      _autoIncludeCollections,
                                      false,
                                      _lowLevelFilter );
            }

            public IPocoTypeSet ExcludeEmptyRecords()
            {
                return _allowEmptyRecords
                        ? CreateTypeSet( _raw.Clone(),
                                         allowEmptyRecords: false,
                                         _allowEmptyPocos,
                                         allowedTypes: null,
                                         excludedTypes: null,
                                         _autoIncludeCollections,
                                         false,
                                         _lowLevelFilter )
                        : this;
            }

            public IPocoTypeSet ExcludeEmptyPocos()
            {
                return _allowEmptyPocos
                        ? CreateTypeSet( _raw.Clone(),
                                         _allowEmptyRecords,
                                         allowEmptyPocos: false,
                                         allowedTypes: null,
                                         excludedTypes: null,
                                         _autoIncludeCollections,
                                         false,
                                         _lowLevelFilter )
                        : this;
            }

            public IPocoTypeSet ExcludeEmptyRecordsAndPocos()
            {
                return _allowEmptyRecords || _allowEmptyPocos
                        ? CreateTypeSet( _raw.Clone(),
                                         allowEmptyRecords: false,
                                         allowEmptyPocos: false,
                                         allowedTypes: null,
                                         excludedTypes: null,
                                         _autoIncludeCollections,
                                         false,
                                         _lowLevelFilter )
                        : this;
            }
        }

        static IPocoTypeSet CreateTypeSet( PocoTypeRawSet workingSet,
                                           bool allowEmptyRecords,
                                           bool allowEmptyPocos,
                                           IEnumerable<IPocoType>? allowedTypes,
                                           IEnumerable<IPocoType>? excludedTypes,
                                           bool autoIncludeCollections,
                                           bool withAbstractReadOnlyFieldTypes,
                                           Func<IPocoType, bool> lowLevelFilter )
        {
            if( allowedTypes != null )
            {
                var allower = new PocoTypeIncludeVisitor( workingSet,
                                                          autoIncludeCollections,
                                                          withAbstractReadOnlyFieldTypes );
                foreach( var type in allowedTypes )
                {
                    allower.VisitRoot( type.NonNullable, clearLastVisited: false );
                }
            }
            var e = new Excluder( workingSet, allowEmptyRecords, allowEmptyPocos, lowLevelFilter );
            if( excludedTypes != null )
            {
                foreach( var type in excludedTypes )
                {
                    e.DoExclude( type, true );
                }
            }
            return new TypeSet( workingSet, allowEmptyRecords, allowEmptyPocos, autoIncludeCollections, lowLevelFilter );
        }

        [Conditional("DEBUG")]
        static void CheckTypeSetRules( IPocoTypeSet set )
        {
            foreach( var t in set.NonNullableTypes )
            {
                // Rule 1: Nullable <=> Non nullable cannot really be tested, this is by design.
                Throw.DebugAssert( $"Rule 2, Type = {t}.", set.Contains( t.ObliviousType ) );
                Throw.DebugAssert( $"Rule 3, Type = {t}.", t is not IUnionPocoType u2 || u2.AllowedTypes.Any( set.Contains ) );
                Throw.DebugAssert( $"Rule 4, Type = {t}.", (t is not ICollectionPocoType c4 || !c4.IsAbstractReadOnly) || set.Contains( c4.MutableCollection ) );
                Throw.DebugAssert( $"Rule 5, Type = {t}.", t is not ICollectionPocoType c5 || c5.ItemTypes.All( set.Contains ) );
                Throw.DebugAssert( $"Rule 6, Type = {t}.", t is not IEnumPocoType e6 || set.Contains( e6.UnderlyingType ) );
                Throw.DebugAssert( $"Rule 7, Type = {t}.", t is not IAbstractPocoType a7 || a7.GenericArguments.All( a => set.Contains( a.Type ) ) );
                Throw.DebugAssert( $"Rule 8, Type = {t}.", t is not IAbstractPocoType a8 || a8.PrimaryPocoTypes.Any( set.Contains ) );
                Throw.DebugAssert( $"Rule 9a, Type = {t}.", t is not ISecondaryPocoType s9a || set.Contains( s9a.PrimaryPocoType ) );
                Throw.DebugAssert( $"Rule 9b, Type = {t}.", t is not IPrimaryPocoType s9b || s9b.SecondaryTypes.All( set.Contains ) );
                Throw.DebugAssert( $"Rule 10, Type = {t}.", t is not IPrimaryPocoType p10 || p10.AbstractTypes.All( set.Contains ) );
                Throw.DebugAssert( $"Rule 11, Type = {t}.", t.Kind != PocoTypeKind.AnonymousRecord || ((IRecordPocoType)t).Fields.Any( f => set.Contains( f.Type ) ) );
                if( !set.AllowEmptyRecords )
                {
                    Throw.DebugAssert( "Rule 12.", t is not IRecordPocoType r12 || r12.Fields.Any( f => set.Contains( f.Type ) ) );
                }
                if( !set.AllowEmptyPocos )
                {
                    Throw.DebugAssert( "Rule 13.", t is not IPrimaryPocoType p13 || p13.Fields.Any( f => set.Contains( f.Type ) ) );
                }
            }
        }

    }
}
