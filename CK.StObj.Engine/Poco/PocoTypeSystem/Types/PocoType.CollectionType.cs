using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Setup
{

    partial class PocoType
    {
        internal static CollectionType1 CreateCollection( IActivityMonitor monitor,
                                                          PocoTypeSystem s,
                                                          Type tCollection,
                                                          string typeName,
                                                          PocoTypeKind kind,
                                                          IPocoType itemType )
        {
            return new CollectionType1( monitor, s, tCollection, typeName, kind, itemType );
        }

        internal static DictionaryType CreateDictionary( IActivityMonitor monitor,
                                                         PocoTypeSystem s,
                                                         Type tCollection,
                                                         string typeName,
                                                         IPocoType itemType1,
                                                         IPocoType itemType2 )
        {
            return new DictionaryType( monitor, s, tCollection, typeName, itemType1, itemType2 );
        }

        sealed class NullCollection : NullReferenceType, ICollectionPocoType
        {
            public NullCollection( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new ICollectionPocoType NonNullable => Unsafe.As<ICollectionPocoType>( base.NonNullable );

            public IReadOnlyList<IPocoType> ItemTypes => NonNullable.ItemTypes;

            ICollectionPocoType ICollectionPocoType.NonNullable => NonNullable;

            ICollectionPocoType ICollectionPocoType.Nullable => this;
        }

        // List, HashSet, Array.
        // This auto implements IReadOnlyList<IPocoType> and IPocoType.ITypeRef.
        internal sealed class CollectionType1 : PocoType, ICollectionPocoType, IReadOnlyList<IPocoType>, IPocoType.ITypeRef
        {
            readonly IPocoType _itemType;
            readonly IPocoFieldDefaultValue _def;
            readonly IPocoType.ITypeRef? _nextRef;

            public CollectionType1( IActivityMonitor monitor,
                                    PocoTypeSystem s,
                                    Type tCollection,
                                    string typeName,
                                    PocoTypeKind kind,
                                    IPocoType itemType )
                : base( s, tCollection, typeName, kind, t => new NullCollection( t ) )
            {
                Debug.Assert( kind == PocoTypeKind.List || kind == PocoTypeKind.HashSet || kind == PocoTypeKind.Array );
                _itemType = itemType;
                _nextRef = ((PocoType)itemType.NonNullable).AddBackRef( this );
                _def = tCollection.IsArray
                        ? new FieldDefaultValue( $"System.Array.Empty<{itemType.CSharpName}>()" )
                        : new FieldDefaultValue( $"new {CSharpName}()" );
                // Sets the initial IsExchangeable status.
                if( !itemType.IsExchangeable )
                {
                    SetNotExchangeable( monitor, $"since '{itemType}' is not." );
                }
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( base.Nullable );

            public IReadOnlyList<IPocoType> ItemTypes => this;

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            int IReadOnlyCollection<IPocoType>.Count => 1;

            #region ITypeRef auto implementation
            public IPocoType.ITypeRef? NextRef => _nextRef;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _itemType;

            #endregion

            public override bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false )
            {
                if( !ignoreRootTypeIsNullable && type.IsNullable ) return false;
                if( !IsPurelyGeneratedType )
                {
                    if( Kind == PocoTypeKind.Array )
                    {
                        // Array is totally invariant in the poco world.
                        if( !type.Type.IsSZArray ) return false;
                        Debug.Assert( type.ElementType != null );
                        return _itemType.IsSameType( type.ElementType );
                    }
                    if( Type != type.Type ) return false;
                    Debug.Assert( type.GenericTypeArguments.Count == 1 );
                    return _itemType.IsSameType( type.GenericTypeArguments[0] );
                }
                // The purely generated type are currently only for Poco List, Set (and Dictionary).
                Debug.Assert( _itemType.Kind == PocoTypeKind.IPoco );
                Debug.Assert( Type == IDynamicAssembly.PurelyGeneratedType, "This one cannot do any job :)." );
                // We could resolve the PocoType and expect this PocoType in return...
                // ...or we can "reproduce" the "external" to actual type mapping: only the abstractions
                // are mapped to the generated type.
                if( type.Type.IsGenericType && !type.Type.IsValueType )
                {
                    var tGen = type.Type.GetGenericTypeDefinition();
                    if( (Kind == PocoTypeKind.List && (tGen == typeof( IReadOnlyList<> ) || tGen == typeof( IList<> )))
                        ||
                        (Kind == PocoTypeKind.HashSet && (tGen == typeof( IReadOnlySet<> ) || tGen == typeof( ISet<> ))) )
                    {
                        return _itemType.IsSameType( type.GenericTypeArguments[0] );
                    }
                }
                return false;
            }

            public override bool IsReadableType( IExtNullabilityInfo type )
            {
                if( Kind == PocoTypeKind.Array )
                {
                    // Fix the dangerous array covariance: type can be read
                    // if IsAssignableFrom accepts it: this supports object and IReadOnyList<ElementType>, but
                    // we forbid array of covariant types and IList<> or ICollection<> since checking the
                    // bool IsReadOnly is a barely known practice.
                    if( !type.Type.IsAssignableFrom( Type ) ) return false;
                    if( type.Type.IsArray ) return type.ElementType!.Type == _itemType.Type;
                    if( type.Type.IsGenericType )
                    {
                        var tGen = type.Type.GetGenericTypeDefinition();
                        // Allowing only IReadOnlyList<> here forbids all others that are safe (IEnumerable<>,
                        // IReadOnlyCollection<>,...) but these types are not currently supported by Poco so
                        // it is safer to be strict.
                        if( tGen != typeof( IReadOnlyList<> ) ) return false;
                    }
                    return true;
                }

                if( !IsPurelyGeneratedType )
                {
                    // Rely on the actual type and don't handle more adaptation
                    // than the actual type supports. Our CovariantHelpers implementation for
                    // value types do their job here: a IList<int> can be read as a IReadOnlyList<int?>
                    // or a IReadOnlyList<object>.
                    return type.Type.IsAssignableFrom( Type );
                }
                // We are on our wrappers. Since we did not generate dynamic types for them, we must
                // reproduce here their capabilities.
                // The purely generated type are currently only for Poco List, Set (and Dictionary).
                Debug.Assert( _itemType.Kind == PocoTypeKind.IPoco );

                if( type.Type.IsGenericType && !type.Type.IsValueType )
                {
                    if( Kind == PocoTypeKind.List )
                    {
                        var tGen = type.Type.GetGenericTypeDefinition();
                        if( tGen == typeof( IReadOnlyList<> ) )
                        {
                            // This is full covariance.
                            return _itemType.IsReadableType( type.GenericTypeArguments[0] );
                        }
                        if( tGen == typeof( IList<> ) )
                        {
                            // Since the item type is IPoco, we can use IsWritableType
                            // because no other variations can exist.
                            return _itemType.IsSameType( type.GenericTypeArguments[0], ignoreRootTypeIsNullable: true );
                        }
                        if( tGen == typeof( List<> ) )
                        {
                            var other = type.GenericTypeArguments[0];
                            return (other.IsNullable || !_itemType.IsNullable) && _itemType.Type == other.Type;
                        }
                    }
                    else 
                    {
                        Debug.Assert( Kind == PocoTypeKind.HashSet );
                        var tGen = type.Type.GetGenericTypeDefinition();
                        if( tGen == typeof( IReadOnlySet<> ) )
                        {
                            // This is full covariance.
                            return _itemType.IsReadableType( type.GenericTypeArguments[0] );
                        }
                        if( tGen == typeof( ISet<> ) )
                        {
                            return _itemType.IsSameType( type.GenericTypeArguments[0], ignoreRootTypeIsNullable: true );
                        }
                        if( tGen == typeof( HashSet<> ) )
                        {
                            var other = type.GenericTypeArguments[0];
                            return (other.IsNullable || !_itemType.IsNullable) && _itemType.Type == other.Type;
                        }
                    }
                }
                return false;
            }

            public override bool IsWritableType( IExtNullabilityInfo type )
            {
                if( type.IsNullable ) return false;
                if( !IsPurelyGeneratedType )
                {
                    if( Kind == PocoTypeKind.Array ) return IsSameType( type, true );
                    if( !Type.IsAssignableFrom( type.Type ) ) return false;
                    return true;
                }
                return IsSameType( type, true );
            }

            IPocoType IReadOnlyList<IPocoType>.this[int index]
            {
                get
                {
                    Throw.CheckOutOfRangeArgument( index == 0 );
                    return _itemType;
                }
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            IEnumerator<IPocoType> IEnumerable<IPocoType>.GetEnumerator() => new CKEnumeratorMono<IPocoType>( _itemType );

            IEnumerator IEnumerable.GetEnumerator() => new CKEnumeratorMono<IPocoType>( _itemType );
        }

        // Dictionary.
        // Auto implements the IPocoType.ITypeRef for the Key. The Value uses a dedicated PocoTypeRef.
        internal sealed class DictionaryType : PocoType, ICollectionPocoType, IPocoType.ITypeRef
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoType.ITypeRef? _nextRefKey;
            readonly IPocoFieldDefaultValue _def;

            public DictionaryType( IActivityMonitor monitor,
                                   PocoTypeSystem s,
                                   Type tCollection,
                                   string typeName,
                                   IPocoType keyType,
                                   IPocoType valueType )
                : base( s, tCollection, typeName, PocoTypeKind.Dictionary, t => new NullCollection( t ) )
            {
                _itemTypes = new[] { keyType, valueType };
                Debug.Assert( !keyType.IsNullable );
                _nextRefKey = ((PocoType)keyType).AddBackRef( this );
                var valueRef = new PocoTypeRef( this, valueType, 1 );
                _def = new FieldDefaultValue( $"new {CSharpName}()" );
                // Sets the initial IsExchangeable status.
                if( !keyType.IsExchangeable ) OnNoMoreExchangeable( monitor, this );
                else if( !valueType.IsExchangeable ) OnNoMoreExchangeable( monitor, valueRef );
            }

            // Base OnNoMoreExchangeable method is fine here.
            // protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new NullCollection Nullable => Unsafe.As<NullCollection>( base.Nullable );

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            #region ITypeRef auto implementation for Key type.

            IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRefKey;

            int IPocoType.ITypeRef.Index => 0;

            IPocoType IPocoType.ITypeRef.Owner => this;

            IPocoType IPocoType.ITypeRef.Type => _itemTypes[0];
            #endregion


            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            public override bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false )
            {
                if( !ignoreRootTypeIsNullable && type.IsNullable ) return false;
                if( !IsPurelyGeneratedType )
                {
                    if( Type != type.Type ) return false;
                    Debug.Assert( type.GenericTypeArguments.Count == 2 );
                    // No need to check the key here since null is not allowed for it.
                    return _itemTypes[1].IsSameType( type.GenericTypeArguments[1] );
                }
                // See CollectonType1 above.
                Debug.Assert( _itemTypes[1].Kind == PocoTypeKind.IPoco );
                Debug.Assert( Type == IDynamicAssembly.PurelyGeneratedType, "This one cannot do any job :)." );
                if( type.Type.IsGenericType && !type.Type.IsValueType )
                {
                    var tGen = type.Type.GetGenericTypeDefinition();
                    if( tGen == typeof( IReadOnlyDictionary<,> ) || tGen == typeof( IDictionary<,> ) )
                    {
                        if( _itemTypes[0].Type != type.GenericTypeArguments[0].Type ) return false;
                        return _itemTypes[1].IsSameType( type.GenericTypeArguments[1] );
                    }
                }
                return false;
            }

            public override bool IsReadableType( IExtNullabilityInfo type )
            {
                if( !IsPurelyGeneratedType )
                {
                    return base.IsReadableType( type );
                }
                // We are on our wrappers. Since we did not generate dynamic types for them, we must
                // reproduce here their capabilities.
                // The purely generated type are currently only for Poco List, Set (and Dictionary) but not array.
                Debug.Assert( _itemTypes[1].Kind == PocoTypeKind.IPoco );
                Debug.Assert( Kind != PocoTypeKind.Array );

                if( type.Type.IsGenericType && !type.Type.IsValueType )
                {
                    var tGen = type.Type.GetGenericTypeDefinition();
                    if( tGen == typeof( IReadOnlyDictionary<,> ) )
                    {
                        // TKey is invariant. 
                        if( _itemTypes[0].Type != type.GenericTypeArguments[0].Type ) return false;
                        // This is full covariance (on the TValue).
                        return _itemTypes[1].IsReadableType( type.GenericTypeArguments[1] );
                    }
                    if( tGen == typeof( IDictionary<,> ) )
                    {
                        // TKey is invariant. 
                        if( _itemTypes[0].Type != type.GenericTypeArguments[0].Type ) return false;
                        return _itemTypes[1].IsWritableType( type.GenericTypeArguments[1] );
                    }
                    if( tGen == typeof( Dictionary<,> ) )
                    {
                        // TKey is invariant. 
                        if( _itemTypes[0].Type != type.GenericTypeArguments[0].Type ) return false;
                        var other = type.GenericTypeArguments[0];
                        return (other.IsNullable || !_itemTypes[1].IsNullable) && _itemTypes[1].Type == other.Type;
                    }
                }
                return false;
            }

            public override bool IsWritableType( IExtNullabilityInfo type )
            {
                if( type.IsNullable ) return false;
                if( !IsPurelyGeneratedType )
                {
                    return Type.IsAssignableFrom( type.Type );
                }
                return IsSameType( type, true );
            }

        }

    }
}
