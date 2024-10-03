using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CK.Setup;

sealed partial class PocoTypeSystem : IPocoTypeSetManager
{
    public IPocoTypeSet Empty => _emptyTypeSet;

    public IPocoTypeSet EmptySerializable => _emptySerializableTypeSet;

    public IPocoTypeSet EmptyExchangeable => _emptyExchangableTypeSet;

    public IPocoTypeSet AllExchangeable => _allExchangeableTypeSet;

    public IPocoTypeSet AllSerializable => _allSerializableTypeSet;

    public IPocoTypeSet All => _allTypeSet;

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

    static bool ImplementationLessFilter( IPocoType t ) => !t.ImplementationLess;

    IReadOnlyList<int> GetZeroFlagArray()
    {
        if( _zeros == null )
        {
            _zeros = new PocoTypeRawSet( this );
        }
        return _zeros.FlagArray;
    }

    sealed class RootNone : IPocoTypeSet
    {
        readonly PocoTypeSystem _typeSystem;
        readonly Func<IPocoType, bool> _lowLevelFilter;
        readonly bool _allowEmptyRecords;
        readonly bool _allowEmptyPocos;
        readonly bool _autoIncludeCollections;

        internal RootNone( PocoTypeSystem typeSystem,
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

        public int Count => 0;

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

        public IReadOnlyPocoTypeSet NonNullableTypes => IReadOnlyPocoTypeSet.Empty;

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

        public IPocoTypeSet IncludeAndExclude( IEnumerable<IPocoType> include, IEnumerable<IPocoType> exclude )
        {
            if( !include.Any() && !exclude.Any() ) return this;
            // The working set is a new empty raw set.
            return CreateTypeSet( workingSet: new PocoTypeRawSet( _typeSystem ),
                                  _allowEmptyRecords,
                                  _allowEmptyPocos,
                                  allowedTypes: include,
                                  excludedTypes: exclude,
                                  _autoIncludeCollections,
                                  withAbstractReadOnlyFieldTypes: false,
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

        public bool IsSupersetOf( IPocoTypeSet other ) => SameContentAs( other );

        public IReadOnlyList<int> FlagArray => _typeSystem.GetZeroFlagArray();

        public IEnumerator<IPocoType> GetEnumerator() => IReadOnlyPocoTypeSet.Empty.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

        public int Count => _raw.Count;

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
                    : _raw.Count == other.Count;
        }

        public IReadOnlyPocoTypeSet NonNullableTypes => _raw.NonNullableTypes;

        public bool Contains( IPocoType t ) => _raw.Contains( t );

        public IPocoTypeSet Include( IEnumerable<IPocoType> types, bool withAbstractReadOnlyFieldTypes = false )
        {
            if( !types.Any() ) return this;
            return CreateTypeSet( _raw.Clone(),
                                  _allowEmptyRecords,
                                  _allowEmptyPocos,
                                  allowedTypes: types,
                                  excludedTypes: null,
                                  _autoIncludeCollections,
                                  withAbstractReadOnlyFieldTypes,
                                  _lowLevelFilter );
        }

        public IPocoTypeSet IncludeAndExclude( IEnumerable<IPocoType> include, IEnumerable<IPocoType> exclude )
        {
            if( !include.Any() && !exclude.Any() ) return this;
            return CreateTypeSet( _raw.Clone(),
                                  _allowEmptyRecords,
                                  _allowEmptyPocos,
                                  allowedTypes: include,
                                  excludedTypes: exclude,
                                  _autoIncludeCollections,
                                  withAbstractReadOnlyFieldTypes: false,
                                  _lowLevelFilter );
        }

        public IPocoTypeSet Exclude( IEnumerable<IPocoType> disallowedTypes )
        {
            if( !disallowedTypes.Any() ) return this;
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

        public bool IsSupersetOf( IPocoTypeSet other )
        {
            Throw.CheckNotNullArgument( other );
            Throw.CheckArgument( TypeSystem == other.TypeSystem );
            if( other == this ) return true;
            Throw.CheckArgument( "Invalid IPocoTypeSet implementation.", other is RootNone || other is TypeSet );
            return other is TypeSet o ? _raw.IsSupersetOf( o._raw ) : true;
        }

        public IEnumerator<IPocoType> GetEnumerator() => _raw.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _raw.GetEnumerator();

        public IReadOnlyList<int> FlagArray => _raw.FlagArray;
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
            var allower = new PocoTypeIncludeVisitor<PocoTypeRawSet>( workingSet.TypeSystem,
                                                                      workingSet,
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
            Throw.DebugAssert( $"Rule 3, Type = {t}.", t is not IUnionPocoType u3 || u3.AllowedTypes.Any( set.Contains ) );
            Throw.DebugAssert( $"Rule 4, Type = {t}.", t is not IBasicRefPocoType r4 || r4.BaseTypes.All( set.Contains ) );
            Throw.DebugAssert( $"Rule 5, Type = {t}.", t.IsRegular || t.RegularType == null || set.Contains( t.RegularType ) );
            Throw.DebugAssert( $"Rule 6, Type = {t}.", t is not ICollectionPocoType c6 || c6.ItemTypes.All( set.Contains ) );
            Throw.DebugAssert( $"Rule 7, Type = {t}.", t is not IEnumPocoType e7 || set.Contains( e7.UnderlyingType ) );
            Throw.DebugAssert( $"Rule 8, Type = {t}.", t is not IAbstractPocoType a8 || a8.GenericArguments.All( a => set.Contains( a.Type ) ) );
            Throw.DebugAssert( $"Rule 9, Type = {t}.", t is not IAbstractPocoType a9 || a9.PrimaryPocoTypes.Any( set.Contains ) );
            Throw.DebugAssert( $"Rule 10a, Type = {t}.", t is not ISecondaryPocoType s10a || set.Contains( s10a.PrimaryPocoType ) );
            Throw.DebugAssert( $"Rule 10b, Type = {t}.", t is not IPrimaryPocoType s10b || s10b.SecondaryTypes.All( set.Contains ) );
            Throw.DebugAssert( $"Rule 11, Type = {t}.", t is not IPrimaryPocoType p11 || p11.AbstractTypes.All( set.Contains ) );
            Throw.DebugAssert( $"Rule 12, Type = {t}.", t.Kind != PocoTypeKind.AnonymousRecord || ((IRecordPocoType)t).Fields.Any( f => set.Contains( f.Type ) ) );
            if( !set.AllowEmptyRecords )
            {
                Throw.DebugAssert( "Rule 12.", t is not IRecordPocoType r13 || r13.Fields.Any( f => set.Contains( f.Type ) ) );
            }
            if( !set.AllowEmptyPocos )
            {
                Throw.DebugAssert( "Rule 13.", t is not IPrimaryPocoType p14 || p14.Fields.Any( f => set.Contains( f.Type ) ) );
            }
        }
    }

}
