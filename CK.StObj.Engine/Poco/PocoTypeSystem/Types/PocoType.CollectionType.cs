using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Setup
{

    partial class PocoType
    {
        internal static ListOrSetOrArrayType CreateCollection( IActivityMonitor monitor,
                                                               PocoTypeSystem s,
                                                               Type tCollection,
                                                               string csharpName,
                                                               string implTypeName,
                                                               PocoTypeKind kind,
                                                               IPocoType itemType,
                                                               bool isProperType,
                                                               IPocoType? obliviousType )
        {
            return new ListOrSetOrArrayType( monitor, s, tCollection, csharpName, implTypeName, kind, itemType, isProperType, (ICollectionPocoType?)obliviousType );
        }

        internal static DictionaryType CreateDictionary( IActivityMonitor monitor,
                                                         PocoTypeSystem s,
                                                         Type tCollection,
                                                         string csharpName,
                                                         string implTypeName,
                                                         IPocoType itemType1,
                                                         IPocoType itemType2,
                                                         bool isProperType,
                                                         IPocoType? obliviousType )
        {
            return new DictionaryType( monitor, s, tCollection, csharpName, implTypeName, itemType1, itemType2, isProperType, (ICollectionPocoType?)obliviousType );
        }

        sealed class NullCollection : NullReferenceType, ICollectionPocoType
        {
            public NullCollection( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new ICollectionPocoType NonNullable => Unsafe.As<ICollectionPocoType>( base.NonNullable );

            public bool IsAbstractCollection => NonNullable.IsAbstractCollection;

            public IReadOnlyList<IPocoType> ItemTypes => NonNullable.ItemTypes;

            ICollectionPocoType ICollectionPocoType.ObliviousType => Unsafe.As<ICollectionPocoType>( ObliviousType );

            ICollectionPocoType ICollectionPocoType.NonNullable => NonNullable;

            ICollectionPocoType ICollectionPocoType.Nullable => this;
        }

        // List, HashSet, Array.
        // This auto implements IPocoType.ITypeRef.
        internal sealed class ListOrSetOrArrayType : PocoType, ICollectionPocoType, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemType;
            readonly IPocoFieldDefaultValue _def;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly string _implTypeName;
            readonly bool _isProperType;
            readonly ICollectionPocoType _obliviousType;


            public ListOrSetOrArrayType( IActivityMonitor monitor,
                                         PocoTypeSystem s,
                                         Type tCollection,
                                         string csharpName,
                                         string implTypeName,
                                         PocoTypeKind kind,
                                         IPocoType itemType,
                                         bool isProperType,
                                         ICollectionPocoType? obliviousType )
                : base( s, tCollection, csharpName, kind, t => new NullCollection( t ) )
            {
                Debug.Assert( kind == PocoTypeKind.List || kind == PocoTypeKind.HashSet || kind == PocoTypeKind.Array );
                _obliviousType = obliviousType ?? this;
                _implTypeName = implTypeName;
                _isProperType = isProperType;
                _itemType = new[] { itemType };
                if( itemType.Kind != PocoTypeKind.Any )
                {
                    _nextRef = ((PocoType)itemType.NonNullable).AddBackRef( this );
                }
                _def = kind == PocoTypeKind.Array
                        ? new FieldDefaultValue( $"System.Array.Empty<{itemType.CSharpName}>()" )
                        : new FieldDefaultValue( $"new {implTypeName}()" );
                // Sets the initial IsExchangeable status.
                if( !itemType.IsExchangeable )
                {
                    SetNotExchangeable( monitor, $"since '{itemType}' is not." );
                }
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override bool IsProperType => _isProperType;

            public override IPocoType ObliviousType => _obliviousType;

            public bool IsAbstractCollection => Kind! != PocoTypeKind.Array && CSharpName[0] == 'I';

            ICollectionPocoType ICollectionPocoType.ObliviousType => _obliviousType;

            public IReadOnlyList<IPocoType> ItemTypes => _itemType;

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            #region ITypeRef auto implementation
            public IPocoType.ITypeRef? NextRef => _nextRef;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _itemType[0];

            #endregion

            public override bool IsWritableType( IPocoType type )
            {
                // Poco Collections are implementations. We don't support any contravariance.
                return type == this;
            }

            public override bool IsReadableType( IPocoType type )
            {
                if( type == this || type.Kind == PocoTypeKind.Any ) return true;
                // It must be the same kind of collection. Array is invariant.
                if( type.Kind != Kind || Kind == PocoTypeKind.Array ) return false;
                var cType = (ICollectionPocoType)type;
                // Covariance applies to IList or ISet.
                if( cType.IsAbstractCollection )
                {
                    return ItemTypes[0].IsReadableType( cType.ItemTypes[0] );
                }
                Throw.DebugAssert( "Otherwise, type == this.", ItemTypes[0] != cType.ItemTypes[0] );
                // Save the non nullable <: nullable type value
                // but only for reference types.
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
        internal sealed class DictionaryType : PocoType, ICollectionPocoType, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoType.ITypeRef? _nextRefKey;
            readonly IPocoFieldDefaultValue _def;
            readonly string _implTypeName;
            readonly ICollectionPocoType _obliviousType;
            readonly bool _isProperType;

            public DictionaryType( IActivityMonitor monitor,
                                   PocoTypeSystem s,
                                   Type tCollection,
                                   string csharpName,
                                   string implTypeName,
                                   IPocoType keyType,
                                   IPocoType valueType,
                                   bool isProperType,
                                   ICollectionPocoType? obliviousType )
                : base( s, tCollection, csharpName, PocoTypeKind.Dictionary, t => new NullCollection( t ) )
            {
                _itemTypes = new[] { keyType, valueType };
                Debug.Assert( !keyType.IsNullable );
                _obliviousType = obliviousType ?? this;
                _def = new FieldDefaultValue( $"new {implTypeName}()" );
                // Register back references and sets the initial IsExchangeable status.
                if( keyType.Kind != PocoTypeKind.Any )
                {
                    _nextRefKey = ((PocoType)keyType).AddBackRef( this );
                    if( !keyType.IsExchangeable ) OnNoMoreExchangeable( monitor, this );
                }
                if( valueType.Kind != PocoTypeKind.Any )
                {
                    var valueRef = new PocoTypeRef( this, valueType, 1 );
                    if( IsExchangeable && !valueType.IsExchangeable ) OnNoMoreExchangeable( monitor, valueRef );
                }
                _implTypeName = implTypeName;
                _isProperType = isProperType;
            }

            // Base OnNoMoreExchangeable method is fine here.
            // protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override bool IsProperType => _isProperType;

            public override IPocoType ObliviousType => _obliviousType;

            ICollectionPocoType ICollectionPocoType.ObliviousType => Unsafe.As<ICollectionPocoType>( _obliviousType );

            public bool IsAbstractCollection => CSharpName[0] == 'I';

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            #region ITypeRef auto implementation for Key type.

            IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRefKey;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _itemTypes[0];
            #endregion

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            public override bool IsWritableType( IPocoType type )
            {
                // Poco Collections are implementations. We don't support any contravariance.
                return type == this;
            }

            public override bool IsReadableType( IPocoType type )
            {
                if( type == this || type.Kind == PocoTypeKind.Any ) return true;
                // It must be the same kind of collection. Array is invariant.
                if( type.Kind != PocoTypeKind.Dictionary ) return false;
                var cType = (ICollectionPocoType)type;
                // Covariance applies to IDictionary value only.
                if( ItemTypes[0] != cType.ItemTypes[0] ) return false;
                if( cType.IsAbstractCollection )
                {
                    return ItemTypes[1].IsReadableType( cType.ItemTypes[1] );
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

    }
}
