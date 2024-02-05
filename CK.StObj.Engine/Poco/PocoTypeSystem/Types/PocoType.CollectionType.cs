using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Setup
{

    partial class PocoType
    {
        internal static ListOrSetOrArrayType CreateCollection( PocoTypeSystemBuilder s,
                                                               Type tCollection,
                                                               string csharpName,
                                                               string implTypeName,
                                                               PocoTypeKind kind,
                                                               IPocoType itemType,
                                                               IPocoType? obliviousType )
        {
            return new ListOrSetOrArrayType( s, tCollection, csharpName, implTypeName, kind, itemType, (ICollectionPocoType?)obliviousType );
        }

        internal static DictionaryType CreateDictionary( PocoTypeSystemBuilder s,
                                                         Type tCollection,
                                                         string csharpName,
                                                         string implTypeName,
                                                         IPocoType itemType1,
                                                         IPocoType itemType2,
                                                         IPocoType? obliviousType )
        {
            return new DictionaryType( s, tCollection, csharpName, implTypeName, itemType1, itemType2, (ICollectionPocoType?)obliviousType );
        }

        internal static AbstractReadOnlyCollectionType CreateAbstractCollection( PocoTypeSystemBuilder s,
                                                                         Type tCollection,
                                                                         string csharpName,
                                                                         ICollectionPocoType mutable )
        {
            Throw.DebugAssert( mutable.Kind is PocoTypeKind.List or PocoTypeKind.HashSet or PocoTypeKind.Dictionary
                               && !mutable.IsNullable
                               && mutable.IsAbstractCollection
                               && mutable.MutableCollection == mutable );
            return new AbstractReadOnlyCollectionType( s, tCollection, csharpName, mutable );
        }

        sealed class NullCollection : NullReferenceType, ICollectionPocoType
        {
            public NullCollection( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new ICollectionPocoType NonNullable => Unsafe.As<ICollectionPocoType>( base.NonNullable );

            public bool IsAbstractCollection => NonNullable.IsAbstractCollection;

            public bool IsAbstractReadOnly => NonNullable.IsAbstractReadOnly;

            public IReadOnlyList<IPocoType> ItemTypes => NonNullable.ItemTypes;

            public ICollectionPocoType MutableCollection => NonNullable.MutableCollection.Nullable;

            public ICollectionPocoType? AbstractReadOnlyCollection => NonNullable.AbstractReadOnlyCollection?.NonNullable;

            ICollectionPocoType ICollectionPocoType.ObliviousType => Unsafe.As<ICollectionPocoType>( ObliviousType );

            ICollectionPocoType ICollectionPocoType.NonNullable => NonNullable;

            ICollectionPocoType ICollectionPocoType.Nullable => this;

        }

        interface IRegularCollection : ICollectionPocoType
        {
            void SetAbstractReadonly( AbstractReadOnlyCollectionType a );
        }

        // List, HashSet, Array.
        // This auto implements IPocoType.ITypeRef for the type parameter.
        internal sealed class ListOrSetOrArrayType : PocoType, IRegularCollection, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoFieldDefaultValue _def;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly string _implTypeName;
            readonly ICollectionPocoType _obliviousType;
            ICollectionPocoType? _abstractReadOnlyCollection;
            string? _standardName;

            public ListOrSetOrArrayType( PocoTypeSystemBuilder s,
                                         Type tCollection,
                                         string csharpName,
                                         string implTypeName,
                                         PocoTypeKind kind,
                                         IPocoType itemType,
                                         ICollectionPocoType? obliviousType )
                : base( s, tCollection, csharpName, kind, static t => new NullCollection( t ) )
            {
                Debug.Assert( kind == PocoTypeKind.List || kind == PocoTypeKind.HashSet || kind == PocoTypeKind.Array );
                if( obliviousType != null )
                {
                    Throw.DebugAssert( obliviousType.IsOblivious
                                       && obliviousType.Kind == kind
                                       && obliviousType.ItemTypes[0].IsOblivious );
                    _obliviousType = obliviousType;
                    // Registers the back reference to the oblivious type.
                    _ = new PocoTypeRef( this, obliviousType, -1 );
                }
                else
                {
                    _obliviousType = this;
                }
                _implTypeName = implTypeName;
                _itemTypes = new[] { itemType };
                _nextRef = ((PocoType)itemType.NonNullable).AddBackRef( this );
                _def = kind == PocoTypeKind.Array
                        ? new FieldDefaultValue( $"System.Array.Empty<{itemType.CSharpName}>()" )
                        : new FieldDefaultValue( $"new {implTypeName}()" );
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override string StandardName => _standardName ??= $"{Kind switch { PocoTypeKind.Array => 'A', PocoTypeKind.List => 'L', _ => 'S'}}({_itemTypes[0].StandardName})";

            public override ICollectionPocoType ObliviousType => _obliviousType;

            public bool IsAbstractCollection => Kind! != PocoTypeKind.Array && CSharpName[0] == 'I';

            public bool IsAbstractReadOnly => false;

            public ICollectionPocoType MutableCollection => this;

            public ICollectionPocoType? AbstractReadOnlyCollection => _abstractReadOnlyCollection;

            public void SetAbstractReadonly( AbstractReadOnlyCollectionType a ) => _abstractReadOnlyCollection = a;

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            #region ITypeRef auto implementation
            public IPocoType.ITypeRef? NextRef => _nextRef;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _itemTypes[0];

            #endregion

            public override bool CanReadFrom( IPocoType type )
            {
                // type.IsNullable may be true: we don't care.
                if( type.NonNullable == this || type.Kind == PocoTypeKind.Any ) return true;
                // It must be the same kind of collection. Array is invariant.
                if( type.Kind != Kind || Kind == PocoTypeKind.Array ) return false;
                var cType = (ICollectionPocoType)type;
                // Covariance applies to IList or ISet.
                if( cType.IsAbstractCollection )
                {
                    return ItemTypes[0].CanReadFrom( cType.ItemTypes[0] );
                }
                Throw.DebugAssert( "Otherwise, type == this.", ItemTypes[0] != cType.ItemTypes[0] );
                // Both are List<> or HashSet<>.
                // Save the non nullable <: nullable type value but only for reference types.
                // Allow List<object?> = List<object> but not List<int?> = List<int>.
                var tItem = ItemTypes[0];
                if( !tItem.Type.IsValueType )
                {
                    var targetValue = cType.ItemTypes[0].NonNullable;
                    return tItem == targetValue;
                }
                return false;
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );
        }

        // Dictionary.
        // Auto implements the IPocoType.ITypeRef for the Key. The Value uses a dedicated PocoTypeRef.
        internal sealed class DictionaryType : PocoType, IRegularCollection, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoType.ITypeRef? _nextRefKey;
            readonly IPocoFieldDefaultValue _def;
            readonly string _implTypeName;
            readonly ICollectionPocoType _obliviousType;
            ICollectionPocoType? _abstractReadOnlyCollection;
            string? _standardName;

            public DictionaryType( PocoTypeSystemBuilder s,
                                   Type tCollection,
                                   string csharpName,
                                   string implTypeName,
                                   IPocoType keyType,
                                   IPocoType valueType,
                                   ICollectionPocoType? obliviousType )
                : base( s, tCollection, csharpName, PocoTypeKind.Dictionary, static t => new NullCollection( t ) )
            {
                _itemTypes = new[] { keyType, valueType };
                Debug.Assert( !keyType.IsNullable );
                if( obliviousType != null )
                {
                    Throw.DebugAssert( obliviousType.IsOblivious
                                       && obliviousType.Kind == PocoTypeKind.Dictionary
                                       && obliviousType.ItemTypes.All( i => i.IsOblivious ) );
                    _obliviousType = obliviousType;
                    // Registers the back reference to the oblivious type.
                    _ = new PocoTypeRef( this, obliviousType, -1 );
                }
                else
                {
                    _obliviousType = this;
                }
                _def = new FieldDefaultValue( $"new {implTypeName}()" );
                // Register back references (key is embedded, value has its own PocoTypeRef).
                _nextRefKey = ((PocoType)keyType).AddBackRef( this );
                _ = new PocoTypeRef( this, valueType, 1 );
                _implTypeName = implTypeName;
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override string StandardName
            {
                get
                {
                    if( _standardName == null )
                    {
                        var k = _itemTypes[0];
                        _standardName = k.Type == typeof( string )
                                            ? $"O({_itemTypes[1].StandardName})"
                                            : $"M({k.StandardName},{_itemTypes[1].StandardName})";
                    }
                    return _standardName;
                }
            }

            public override ICollectionPocoType ObliviousType => _obliviousType;

            public bool IsAbstractCollection => CSharpName[0] == 'I';

            public bool IsAbstractReadOnly => false;

            public ICollectionPocoType MutableCollection => this;

            public ICollectionPocoType? AbstractReadOnlyCollection => _abstractReadOnlyCollection;

            public void SetAbstractReadonly( AbstractReadOnlyCollectionType a ) => _abstractReadOnlyCollection = a;

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            #region ITypeRef auto implementation for Key type.

            IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRefKey;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _itemTypes[0];
            #endregion

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            public override bool CanReadFrom( IPocoType type )
            {
                // type.IsNullable may be true: we don't care.
                if( type.NonNullable == this || type.Kind == PocoTypeKind.Any ) return true;
                // It must be the same kind of collection. Array is invariant.
                if( type.Kind != PocoTypeKind.Dictionary ) return false;
                var cType = (ICollectionPocoType)type;
                // Covariance applies to IDictionary value only.
                if( ItemTypes[0] != cType.ItemTypes[0] ) return false;
                if( cType.IsAbstractCollection )
                {
                    return ItemTypes[1].CanReadFrom( cType.ItemTypes[1] );
                }
                Throw.DebugAssert( "Otherwise, type == this.", ItemTypes[1] != cType.ItemTypes[1] );
                // Save the non nullable <: nullable type value
                // but only for reference types.
                IPocoType tItem = ItemTypes[1];
                if( tItem.Type.IsValueType )
                {
                    var targetValue = ItemTypes[1].NonNullable;
                    return tItem == targetValue;
                }
                return false;
            }
        }

        // IReadOnlyList, IReadOnlySet or IReadOnlyDictionary.
        internal sealed class AbstractReadOnlyCollectionType : PocoType, ICollectionPocoType
        {
            readonly ICollectionPocoType _mutable;

            public AbstractReadOnlyCollectionType( PocoTypeSystemBuilder s,
                                                   Type tCollection,
                                                   string csharpName,
                                                   ICollectionPocoType mutable )
                : base( s, tCollection, csharpName, mutable.Kind, static t => new NullCollection( t ) )
            {
                _mutable = mutable;
                // Registers the back reference to the oblivious type.
                _ = new PocoTypeRef( this, _mutable.ObliviousType, -1 );
                ((IRegularCollection)mutable.NonNullable).SetAbstractReadonly( this );
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public bool IsAbstractCollection => true;

            public bool IsAbstractReadOnly => true;

            public override string StandardName => _mutable.StandardName;

            public ICollectionPocoType MutableCollection => _mutable;

            public ICollectionPocoType? AbstractReadOnlyCollection => this;

            public IReadOnlyList<IPocoType> ItemTypes => _mutable.ItemTypes;

            public override ICollectionPocoType ObliviousType => _mutable.ObliviousType;

            public override bool CanReadFrom( IPocoType type ) => _mutable.CanReadFrom( type );

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;
        }

    }
}
