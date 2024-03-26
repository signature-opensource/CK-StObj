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
                                                               IPocoType? obliviousType,
                                                               IPocoType? finalType,
                                                               IPocoType? regularCollection )
        {
            return new ListOrSetOrArrayType( s,
                                             tCollection,
                                             csharpName,
                                             implTypeName,
                                             kind,
                                             itemType,
                                             (ICollectionPocoType?)obliviousType,
                                             (ICollectionPocoType?)finalType,
                                             (ICollectionPocoType?)regularCollection );
        }

        internal static DictionaryType CreateDictionary( PocoTypeSystemBuilder s,
                                                         Type tCollection,
                                                         string csharpName,
                                                         string implTypeName,
                                                         IPocoType itemType1,
                                                         IPocoType itemType2,
                                                         IPocoType? obliviousType,
                                                         IPocoType? finalType,
                                                         IPocoType? regularCollectionType )
        {
            return new DictionaryType( s,
                                       tCollection,
                                       csharpName,
                                       implTypeName,
                                       itemType1,
                                       itemType2,
                                       (ICollectionPocoType?)obliviousType,
                                       (ICollectionPocoType?)finalType,
                                       (ICollectionPocoType?)regularCollectionType );
        }

        internal static AbstractReadOnlyCollectionType CreateAbstractCollection( PocoTypeSystemBuilder s,
                                                                                 Type tCollection,
                                                                                 string csharpName,
                                                                                 PocoTypeKind kind,
                                                                                 IPocoType[] itemTypes,
                                                                                 IPocoType? obliviousType )
        {
            return new AbstractReadOnlyCollectionType( s, tCollection, csharpName, kind, itemTypes, (ICollectionPocoType?)obliviousType );
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

            // Same as base NullReferenceType.ObliviousType (but uses Covariant return type).
            public override ICollectionPocoType ObliviousType => NonNullable.ObliviousType;

            ICollectionPocoType ICollectionPocoType.NonNullable => NonNullable;

            ICollectionPocoType ICollectionPocoType.Nullable => this;

            ICollectionPocoType? ICollectionPocoType.StructuralFinalType => NonNullable.StructuralFinalType;

            ICollectionPocoType? ICollectionPocoType.FinalType => NonNullable.FinalType;
        }

        // List, HashSet, Array.
        // This auto implements IPocoType.ITypeRef for the type parameter.
        internal sealed class ListOrSetOrArrayType : PocoType, ICollectionPocoType, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoFieldDefaultValue _def;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly string _implTypeName;
            readonly ICollectionPocoType _obliviousType;
            readonly ICollectionPocoType _finalType;
            readonly ICollectionPocoType _regularCollection;
            bool _implementationLess;

            public ListOrSetOrArrayType( PocoTypeSystemBuilder s,
                                         Type tCollection,
                                         string csharpName,
                                         string implTypeName,
                                         PocoTypeKind kind,
                                         IPocoType itemType,
                                         ICollectionPocoType? obliviousType,
                                         ICollectionPocoType? finalType,
                                         ICollectionPocoType? regularCollection )
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
                    _obliviousType = Nullable;
                }
                Throw.DebugAssert( "A final reference type is oblivious (and as a reference type, nullable).",
                                   finalType == null || (finalType.IsNullable && finalType.IsStructuralFinalType && finalType.IsOblivious) );
                _finalType = finalType ?? Nullable;
                Throw.DebugAssert( "The regular collection has the same kind and the provided instance is not nullable and not abstract.",
                                   regularCollection == null || (!regularCollection.IsNullable && regularCollection.Kind == kind && !regularCollection.IsAbstractCollection) );
                Throw.DebugAssert( "The provided regular collection can have the same item type only if we are abstract.",
                                   regularCollection == null || (kind != PocoTypeKind.Array && csharpName[0] == 'I') || regularCollection.ItemTypes[0] != itemType );
                Throw.DebugAssert( "If we are the regular collection, we are not abstract and our item is not an anonymous record with named fields.",
                                   regularCollection != null || ((kind == PocoTypeKind.Array || csharpName[0] != 'I') && (itemType is not IAnonymousRecordPocoType a || a.IsUnnamed) ) );
                _regularCollection = regularCollection ?? this;
                _implTypeName = implTypeName;
                _itemTypes = new[] { itemType };
                _nextRef = ((PocoType)itemType.NonNullable).AddBackRef( this );
                _def = kind == PocoTypeKind.Array
                        ? new FieldDefaultValue( $"System.Array.Empty<{itemType.CSharpName}>()" )
                        : new FieldDefaultValue( $"new {implTypeName}()" );
                // Initial implementation less check.
                if( itemType.ImplementationLess ) SetImplementationLess();
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override ICollectionPocoType ObliviousType => _obliviousType;

            public override ICollectionPocoType? StructuralFinalType => _finalType;

            ICollectionPocoType? ICollectionPocoType.FinalType => _finalType;

            public bool IsAbstractCollection => Kind != PocoTypeKind.Array && CSharpName[0] == 'I';

            public bool IsAbstractReadOnly => false;

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            public override bool ImplementationLess => _implementationLess;

            internal override void SetImplementationLess()
            {
                _implementationLess = true;
                base.SetImplementationLess();
            }

            protected override void OnBackRefImplementationLess( IPocoType.ITypeRef r )
            {
                Throw.DebugAssert( r.Owner == this && r.Type == ItemTypes[0] || r.Type == _obliviousType );
                if( !_implementationLess ) SetImplementationLess();
            }

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
                var other = (ICollectionPocoType)type;
                // Covariance applies to IList or ISet: the other one must be our adapter.
                IPocoType otherItemType = other.ItemTypes[0];
                IPocoType thisItemType = ItemTypes[0];
                if( other.IsAbstractCollection )
                {
                    return thisItemType.CanReadFrom( otherItemType );
                }
                Throw.DebugAssert( "Otherwise, other would be this.", thisItemType != otherItemType );
                // Both are List<> or HashSet<>.
                // Save the non nullable <: nullable type value but only for reference types.
                // Allow List<object?> = List<object> but not List<int?> = List<int>.
                if( !thisItemType.Type.IsValueType )
                {
                    return thisItemType == otherItemType.NonNullable;
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
            readonly ICollectionPocoType _finalType;
            readonly ICollectionPocoType _regularCollection;
            ICollectionPocoType? _abstractReadOnlyCollection;
            bool _implementationLess;

            public DictionaryType( PocoTypeSystemBuilder s,
                                   Type tCollection,
                                   string csharpName,
                                   string implTypeName,
                                   IPocoType keyType,
                                   IPocoType valueType,
                                   ICollectionPocoType? obliviousType,
                                   ICollectionPocoType? finalType,
                                   ICollectionPocoType? regularCollection )
                : base( s, tCollection, csharpName, PocoTypeKind.Dictionary, static t => new NullCollection( t ) )
            {
                _itemTypes = new[] { keyType, valueType };
                Throw.DebugAssert( !keyType.IsNullable && keyType.IsReadOnlyCompliant && !keyType.IsPolymorphic );
                if( obliviousType != null )
                {
                    Throw.DebugAssert( obliviousType.IsOblivious
                                       && obliviousType.Kind == PocoTypeKind.Dictionary
                                       && obliviousType.ItemTypes[1].IsOblivious );
                    _obliviousType = obliviousType;
                    // Registers the back reference to the oblivious type.
                    _ = new PocoTypeRef( this, obliviousType, -1 );
                }
                else
                {
                    _obliviousType = Nullable;
                }

                Throw.DebugAssert( "A final reference type is oblivious (and as a reference type, nullable).",
                                   finalType == null || (finalType.IsNullable && finalType.IsStructuralFinalType && finalType.IsOblivious) );
                _finalType = finalType ?? Nullable;

                Throw.DebugAssert( "The regular collection if provided is not nullable and not abstract.",
                                    regularCollection == null || (!regularCollection.IsNullable && !regularCollection.IsAbstractCollection) );
                Throw.DebugAssert( "The provided regular collection can have the same item type only if we are abstract.",
                                   regularCollection == null || csharpName[0] == 'I' || regularCollection.ItemTypes[0] != keyType || regularCollection.ItemTypes[1] != valueType );
                Throw.DebugAssert( "If we are the regular collection, we are not abstract and our items are not an anonymous record with named fields.",
                                   regularCollection != null || (csharpName[0] != 'I' && (keyType is not IAnonymousRecordPocoType aK || aK.IsUnnamed) && (valueType is not IAnonymousRecordPocoType vK || vK.IsUnnamed)) );
                _regularCollection = regularCollection ?? this;

                _def = new FieldDefaultValue( $"new {implTypeName}()" );
                // Register back references (key is embedded, value has its own PocoTypeRef).
                _nextRefKey = ((PocoType)keyType).AddBackRef( this );
                _ = new PocoTypeRef( this, valueType, 1 );
                _implTypeName = implTypeName;
                // Initial implementation less check.
                if( keyType.ImplementationLess || valueType.ImplementationLess ) SetImplementationLess();
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override ICollectionPocoType ObliviousType => _obliviousType;

            public override IPocoType? StructuralFinalType => _finalType;

            ICollectionPocoType? ICollectionPocoType.StructuralFinalType => _finalType;

            ICollectionPocoType? ICollectionPocoType.FinalType => _finalType;

            public bool IsAbstractCollection => CSharpName[0] == 'I';

            public bool IsAbstractReadOnly => false;

            public ICollectionPocoType? AbstractReadOnlyCollection => _abstractReadOnlyCollection;

            public void SetAbstractReadonly( AbstractReadOnlyCollectionType a ) => _abstractReadOnlyCollection = a;

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            public override bool ImplementationLess => _implementationLess;

            internal override void SetImplementationLess()
            {
                _implementationLess = true;
                base.SetImplementationLess();
            }

            protected override void OnBackRefImplementationLess( IPocoType.ITypeRef r )
            {
                Throw.DebugAssert( r.Owner == this && (r.Type == ItemTypes[0] || r.Type == ItemTypes[1] || r.Type == _obliviousType) );
                if( !_implementationLess ) SetImplementationLess();
            }

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
                var other = (ICollectionPocoType)type;
                ICollectionPocoType @this = this;
                // Covariance applies to IDictionary value only.
                if( @this.ItemTypes[0] != other.ItemTypes[0] ) return false;

                IPocoType thisItemType = @this.ItemTypes[1];
                IPocoType otherItemType = other.ItemTypes[1];
                if( other.IsAbstractCollection )
                {
                    return thisItemType.CanReadFrom( otherItemType );
                }
                Throw.DebugAssert( "Otherwise, other would be this.", thisItemType != otherItemType );
                // Save the non nullable <: nullable type value
                // but only for reference types.
                if( !thisItemType.Type.IsValueType )
                {
                    return thisItemType == otherItemType.NonNullable;
                }
                return false;
            }
        }

        // IReadOnlyList, IReadOnlySet or IReadOnlyDictionary.
        internal sealed class AbstractReadOnlyCollectionType : PocoType, ICollectionPocoType
        {
            readonly IPocoType[] _itemTypes;
            readonly ICollectionPocoType _obliviousType;

            public AbstractReadOnlyCollectionType( PocoTypeSystemBuilder s,
                                                   Type tCollection,
                                                   string csharpName,
                                                   PocoTypeKind kind,
                                                   IPocoType[] itemTypes,
                                                   ICollectionPocoType? obliviousType )
                : base( s, tCollection, csharpName, kind, static t => new NullCollection( t ) )
            {
                _itemTypes = itemTypes;
                _obliviousType = obliviousType ?? Nullable;
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public bool IsAbstractCollection => true;

            public bool IsAbstractReadOnly => true;

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            public override bool IsPolymorphic => true;

            public override ICollectionPocoType ObliviousType => _obliviousType;

            public override bool CanReadFrom( IPocoType type )
            {
                return true;
            }

            // We consider a read only collection to always be implementation less.
            // As these beasts resides on the C# side (as abstract readonly properties),
            // there is currently no point to expose them: by considering them implementation
            // less, they are excluded from any IPocoTypeSet.
            public override bool ImplementationLess => true;

            // Abstract ReadOnly collections have no final type.
            public override IPocoType? StructuralFinalType => null;

            ICollectionPocoType? ICollectionPocoType.StructuralFinalType => null;

            ICollectionPocoType? ICollectionPocoType.FinalType => null;

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;
        }

    }
}
