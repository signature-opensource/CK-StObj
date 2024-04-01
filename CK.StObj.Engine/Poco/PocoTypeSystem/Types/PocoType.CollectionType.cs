using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Setup
{

    partial class PocoType
    {
        internal static IPocoType CreateListOrSetOrArray( PocoTypeSystemBuilder s,
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

        internal static IPocoType CreateAbstractCollection( PocoTypeSystemBuilder s,
                                                             Type tCollection,
                                                             string csharpName,
                                                             string implTypeName,
                                                             PocoTypeKind kind,
                                                             IPocoType concreteCollection,
                                                             IPocoType? obliviousType,
                                                             IPocoType? finalType )
        {
            return new AbstractCollection( s,
                                           tCollection,
                                           csharpName,
                                           implTypeName,
                                           kind,
                                           (ICollectionPocoType)concreteCollection,
                                           (ICollectionPocoType?)obliviousType,
                                           (ICollectionPocoType?)finalType );
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

            public override ICollectionPocoType? RegularType => NonNullable.RegularType?.Nullable;

            public ICollectionPocoType? ConcreteCollection => NonNullable.ConcreteCollection?.Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => NonNullable;

            ICollectionPocoType ICollectionPocoType.Nullable => this;

            ICollectionPocoType? ICollectionPocoType.StructuralFinalType => NonNullable.StructuralFinalType;

            ICollectionPocoType? ICollectionPocoType.FinalType => NonNullable.FinalType;
        }

        // List, HashSet, Array.
        // This auto implements IPocoType.ITypeRef for the type parameter.
        sealed class ListOrSetOrArrayType : PocoType, ICollectionPocoType, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoFieldDefaultValue _def;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly string _implTypeName;
            readonly ICollectionPocoType _obliviousType;
            readonly ICollectionPocoType _finalType;
            readonly ICollectionPocoType _regularType;
            bool _implementationLess;

            public ListOrSetOrArrayType( PocoTypeSystemBuilder s,
                                         Type tCollection,
                                         string csharpName,
                                         string implTypeName,
                                         PocoTypeKind kind,
                                         IPocoType itemType,
                                         ICollectionPocoType? obliviousType,
                                         ICollectionPocoType? finalType,
                                         ICollectionPocoType? regularType )
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
                                   regularType == null || (!regularType.IsNullable && regularType.Kind == kind && !regularType.IsAbstractCollection) );
                Throw.DebugAssert( "The provided regular collection can have the same item type only if we are abstract.",
                                   regularType == null || (kind != PocoTypeKind.Array && csharpName[0] == 'I') || regularType.ItemTypes[0] != itemType );
                Throw.DebugAssert( "If we are the regular collection, we are not abstract and our item is regular.",
                                   regularType != null || ((kind == PocoTypeKind.Array || csharpName[0] != 'I') && itemType.IsRegular) );
                _regularType = regularType ?? this;
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

            public override ICollectionPocoType? RegularType => _regularType;

            public override ICollectionPocoType? StructuralFinalType => _finalType;

            public ICollectionPocoType ConcreteCollection => this;

            ICollectionPocoType? ICollectionPocoType.FinalType => _implementationLess ? null : _finalType;

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

        // IList, ISet and IDictionary.
        // This auto implements IPocoType.ITypeRef for the concrete collection: abstracts are bound to
        // their concrete, it is the concrete that handles implementation less and exclusion.
        // The PocoTypeSet rules is that Abstract => Concrete. To enforce the Oblivious rule we
        // unfortunately need an independent PocoTypeRef to the oblivious if this one is not the oblivious.
        sealed class AbstractCollection : PocoType, ICollectionPocoType, IPocoType.ITypeRef
        {
            readonly IPocoFieldDefaultValue _def;
            readonly IPocoType.ITypeRef? _nextRef;
            readonly string _implTypeName;
            readonly ICollectionPocoType _concreteCollection;
            readonly ICollectionPocoType _obliviousType;
            readonly ICollectionPocoType _finalType;

            public AbstractCollection( PocoTypeSystemBuilder s,
                                       Type tCollection,
                                       string csharpName,
                                       string implTypeName,
                                       PocoTypeKind kind,
                                       ICollectionPocoType concreteCollection,
                                       ICollectionPocoType? obliviousType,
                                       ICollectionPocoType? finalType )
                : base( s, tCollection, csharpName, kind, static t => new NullCollection( t ) )
            {
                Throw.DebugAssert( kind == PocoTypeKind.List || kind == PocoTypeKind.HashSet || kind == PocoTypeKind.Dictionary );
                Throw.DebugAssert( !concreteCollection.IsNullable && !concreteCollection.IsAbstractCollection );
                if( obliviousType != null )
                {
                    Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.Kind == kind );
                    _obliviousType = obliviousType;
                    // Registers the back reference to the oblivious type.
                    _ = new PocoTypeRef( this, obliviousType, -1 );
                }
                else
                {
                    _obliviousType = Nullable;
                }
                Throw.DebugAssert( finalType == null || finalType.IsStructuralFinalType );
                _finalType = finalType ?? Nullable;
                _implTypeName = implTypeName;
                _concreteCollection = concreteCollection;
                _nextRef = ((PocoType)concreteCollection).AddBackRef( this );
                Throw.DebugAssert( "A collection always has a default value.", concreteCollection.DefaultValueInfo.DefaultValue != null );
                _def = implTypeName != null
                        ? new FieldDefaultValue( $"new {implTypeName}()" )
                        : concreteCollection.DefaultValueInfo.DefaultValue;
                // Initial implementation less check.
                if( concreteCollection.ImplementationLess ) SetImplementationLess();
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( _nullable );

            public override string ImplTypeName => _implTypeName;

            public override ICollectionPocoType ObliviousType => _obliviousType;

            public override ICollectionPocoType? RegularType => _concreteCollection.RegularType;

            public override ICollectionPocoType? StructuralFinalType => _finalType;

            public ICollectionPocoType ConcreteCollection => _concreteCollection;

            ICollectionPocoType? ICollectionPocoType.FinalType => _concreteCollection.ImplementationLess ? null : StructuralFinalType;

            public bool IsAbstractCollection => true;

            public bool IsAbstractReadOnly => false;

            public IReadOnlyList<IPocoType> ItemTypes => _concreteCollection.ItemTypes;

            public override bool ImplementationLess => _concreteCollection.ImplementationLess;

            protected override void OnBackRefImplementationLess( IPocoType.ITypeRef r )
            {
                Throw.DebugAssert( r.Owner == this && r.Type == _concreteCollection || r.Type == _obliviousType );
                // Don't challenge _concreteCollection.ImplementationLess here because it is alreay true: always
                // propagate the call even if this is propagated twice.
                SetImplementationLess();
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            #region ITypeRef auto implementation
            public IPocoType.ITypeRef? NextRef => _nextRef;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _concreteCollection;

            #endregion

            public override bool CanReadFrom( IPocoType type ) => _concreteCollection.CanReadFrom( type );
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
            readonly ICollectionPocoType _regularType;
            bool _implementationLess;

            public DictionaryType( PocoTypeSystemBuilder s,
                                   Type tCollection,
                                   string csharpName,
                                   string implTypeName,
                                   IPocoType keyType,
                                   IPocoType valueType,
                                   ICollectionPocoType? obliviousType,
                                   ICollectionPocoType? finalType,
                                   ICollectionPocoType? regularType )
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
                                    regularType == null || (!regularType.IsNullable && !regularType.IsAbstractCollection) );
                Throw.DebugAssert( "The provided regular collection can have the same item type only if we are abstract.",
                                   regularType == null || csharpName[0] == 'I' || regularType.ItemTypes[0] != keyType || regularType.ItemTypes[1] != valueType );
                Throw.DebugAssert( "If we are the regular collection, we are not abstract and our items are regular.",
                                   regularType != null || (csharpName[0] != 'I' && keyType.IsRegular && valueType.IsRegular) );
                _regularType = regularType ?? this;

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

            public override ICollectionPocoType? RegularType => _regularType;

            public ICollectionPocoType ConcreteCollection => this;

            public override ICollectionPocoType? StructuralFinalType => _finalType;

            ICollectionPocoType? ICollectionPocoType.FinalType => _implementationLess ? null : _finalType;

            public bool IsAbstractCollection => CSharpName[0] == 'I';

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

            public override ICollectionPocoType? RegularType => null;

            public ICollectionPocoType? ConcreteCollection => null;

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
