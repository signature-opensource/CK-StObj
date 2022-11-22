using CK.Core;
using OneOf.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Implements a reusable builder for <see cref="ExchangeableTypeName"/>.
    /// This type is completely opened to extensions.
    /// <para>
    /// Using any <see cref="ExchangeableTypeName.SimplifiedName"/> is totally optional.
    /// By default, the simplified name is the same as the <see cref="ExchangeableTypeName.Name"/>.
    /// </para>
    /// </summary>
    public class ExchangeableTypeNameBuilder
    {
        [AllowNull] FullExchangeableTypeName[]? _result;
        readonly StringBuilder _nBuilder = new StringBuilder();
        readonly StringBuilder _sBuilder = new StringBuilder();
        FullExchangeableTypeName _objectTypeName;
        bool _condemnEnumFromUnderlyingType;
        int _exchangeableCount;

        /// <summary>
        /// Generates an array of <see cref="FullExchangeableTypeName"/> for types that are
        /// exchangeable at the type system level.
        /// <para>
        /// This can be called multiple times.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="typeSystem">The type system to process.</param>
        /// <param name="condemnEnumFromUnderlyingType">
        /// True to forbid exchangeability for enums if their underlying type becomes not exchangeable.
        /// False to keep them separate.
        /// </param>
        /// <param name="objectName">Name of the "object" (<see cref="PocoTypeKind.Any"/>).</param>
        /// <returns>The resulting <see cref="ExchangeableTypeNameMap"/>.</returns>
        public virtual ExchangeableTypeNameMap Generate( IActivityMonitor monitor,
                                                         IPocoTypeSystem typeSystem,
                                                         bool condemnEnumFromUnderlyingType,
                                                         string objectName = "object" )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( typeSystem );
            Throw.CheckNotNullOrWhiteSpaceArgument( objectName );

            _condemnEnumFromUnderlyingType = condemnEnumFromUnderlyingType;

            _exchangeableCount = typeSystem.AllNonNullableTypes.Count;
            _result = new FullExchangeableTypeName[_exchangeableCount];
            _objectTypeName = _result[typeSystem.ObjectType.Index >> 1] = new FullExchangeableTypeName( objectName );
            foreach( var t in typeSystem.AllNonNullableTypes )
            {
                ref var n = ref _result[t.Index >> 1];
                if( n.IsInitialized ) continue;
                if( t.IsExchangeable )
                {
                    if( t.Kind != PocoTypeKind.Any && !IsExchangeable( monitor, t ) )
                    {
                        SetNotExchangeable( monitor, t, "IsExchangeable returned false." );
                    }
                }
                else
                {
                    --_exchangeableCount;
                    n = FullExchangeableTypeName.Unexchangeable;
                }
            }
            foreach( var t in typeSystem.AllNonNullableTypes )
            {
                ref var n = ref _result[t.Index >> 1];
                if( n.IsInitialized ) continue;
                if( t is ICollectionPocoType c )
                {
                    n = MakeCollectionName( monitor, c );
                }
                else
                {
                    switch( t.Kind )
                    {
                        case PocoTypeKind.Record:
                            n = MakeRecordName( monitor, (IRecordPocoType)t, GetFieldTypeName );
                            break;
                        case PocoTypeKind.AnonymousRecord:
                            n = MakeAnonymousRecordName( monitor, (IRecordPocoType)t, GetFieldTypeName );
                            break;
                        case PocoTypeKind.Enum:
                            n = MakeEnumName( monitor, (IEnumPocoType)t );
                            break;
                        case PocoTypeKind.Basic:
                            n = MakeBasicName( monitor, t );
                            break;
                        case PocoTypeKind.IPoco:
                            n = MakePrimaryPocoName( monitor, (IPrimaryPocoType)t );
                            break;
                        case PocoTypeKind.AbstractIPoco:
                            n = MakeAbstractPocoName( monitor, (IAbstractPocoType)t );
                            break;
                        case PocoTypeKind.UnionType:
                            n = MakeUnionTypeName( monitor, (IUnionPocoType)t, GetTypeName );
                            break;
                        default: Throw.NotSupportedException( t.ToString() ); break;
                    }
                    if( !n.IsInitialized || !n.IsExchangeable )
                    {
                        Throw.InvalidOperationException( $"Invalid name made for '{t}'." );
                    }
                }
            }
            return new ExchangeableTypeNameMap( _result, typeSystem, _exchangeableCount );
        }

        FullExchangeableTypeName GetFieldTypeName( IPocoField f ) => _result![f.Type.Index >> 1];
        FullExchangeableTypeName GetTypeName( IPocoType t ) => _result![t.Index >> 1];

        internal void SetNotExchangeable( IActivityMonitor monitor, IPocoType t, string? reason = null )
        {
            Debug.Assert( _result != null && (!_result[t.Index >> 1].IsInitialized || !_result[t.Index >> 1].IsExchangeable) );
            ref var n = ref _result[t.Index >> 1];
            if( !n.IsInitialized )
            {
                --_exchangeableCount;
                using( monitor.OpenInfo( $"{t.ToString()} is not exchangeable.{reason}" ) )
                {
                    n = FullExchangeableTypeName.Unexchangeable;
                    var backRef = t.FirstBackReference;
                    while( backRef != null )
                    {
                        if( backRef.Owner is ICollectionPocoType )
                        {
                            SetNotExchangeable( monitor, backRef.Owner );
                        }
                        else
                        {
                            switch( backRef.Owner.Kind )
                            {
                                case PocoTypeKind.IPoco:
                                case PocoTypeKind.AnonymousRecord:
                                case PocoTypeKind.Record:
                                    {
                                        var record = (ICompositePocoType)t;
                                        if( !record.Fields.Any( f => f.IsExchangeable && !_result[f.Type.Index >> 1].IsInitialized ) )
                                        {
                                            SetNotExchangeable( monitor, backRef.Owner, $" No more of the {record.Fields} fields are exchangeable." );
                                        }
                                        break;
                                    }
                                case PocoTypeKind.AbstractIPoco:
                                case PocoTypeKind.UnionType:
                                    {
                                        var u = (IOneOfPocoType)t;
                                        if( !u.AllowedTypes.Any( v => v.IsExchangeable && !_result[v.Index >> 1].IsInitialized ) )
                                        {
                                            SetNotExchangeable( monitor, backRef.Owner, $" No more allowed types are exchangeable." );
                                        }
                                        break;
                                    }
                                case PocoTypeKind.Enum:
                                    if( _condemnEnumFromUnderlyingType )
                                    {
                                        SetNotExchangeable( monitor, backRef.Owner, $" The underlying type is not exchangeable." );
                                    }
                                    break;
                                default: Throw.NotSupportedException( backRef.Owner.ToString() ); break;
                            }
                        }
                        backRef = backRef.NextRef;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the name of the "object" (<see cref="PocoTypeKind.Any"/>).
        /// Note that this type is the only one that is necessarily exchangeable.
        /// </summary>
        protected FullExchangeableTypeName ObjectTypeName => _objectTypeName;

        /// <summary>
        /// Gets whether a type (that is <see cref="IPocoType.IsExchangeable"/>) must be
        /// kept exchangeable or forbidden.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type that may become not exchangeable.</param>
        /// <returns>True if the type must be exchanged, false otherwise.</returns>
        protected virtual bool IsExchangeable( IActivityMonitor monitor, IPocoType t )
        {
            return true;
        }

        /// <summary>
        /// Returns <see cref="ObjectTypeName"/>.
        /// Abstractions are usually exchanged with the "object" type but this may be overridden.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="abstractPoco">The abstract Poco.</param>
        /// <returns>By default, the "object" exchanged name.</returns>
        protected virtual FullExchangeableTypeName MakeAbstractPocoName( IActivityMonitor monitor, IAbstractPocoType abstractPoco )
        {
            return ObjectTypeName;
        }

        /// <summary>
        /// Returns the <see cref="IPocoType.CSharpName"/> by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="basic">The basic type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeBasicName( IActivityMonitor monitor, IPocoType basic )
        {
            return new FullExchangeableTypeName( basic.CSharpName );
        }

        /// <summary>
        /// Returns the <see cref="IPocoType.CSharpName"/> by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="enumeration">The enumeration type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeEnumName( IActivityMonitor monitor, IEnumPocoType enumeration )
        {
            return new FullExchangeableTypeName( enumeration.CSharpName );
        }

        /// <summary>
        /// Routes the call to <see cref="MakeArrayName(IPocoType, in FullExchangeableTypeName)"/>, <see cref="MakeListName(IPocoType, in FullExchangeableTypeName)"/>,
        /// <see cref="MakeSetName(IPocoType, in FullExchangeableTypeName)"/>, <see cref="MakeDynamicObject(IPocoType, FullExchangeableTypeName)"/> or
        /// <see cref="MakeMap(FullExchangeableTypeName, IPocoType, FullExchangeableTypeName)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="collection">The collection type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeCollectionName( IActivityMonitor monitor, ICollectionPocoType collection )
        {
            Debug.Assert( _result != null );
            switch( collection.Kind )
            {
                case PocoTypeKind.Array:
                    {
                        var item = collection.ItemTypes[0];
                        ref var nItem = ref _result[item.Index>>1];
                        Debug.Assert( nItem.IsExchangeable );
                        return MakeArrayName( item, nItem );
                    }
                case PocoTypeKind.List:
                    {
                        var item = collection.ItemTypes[0];
                        ref var nItem = ref _result[item.Index >> 1];
                        Debug.Assert( nItem.IsExchangeable );
                        return MakeListName( item, nItem );
                    }
                case PocoTypeKind.HashSet:
                    {
                        var item = collection.ItemTypes[0];
                        ref var nItem = ref _result[item.Index >> 1];
                        Debug.Assert( nItem.IsExchangeable );
                        return MakeSetName( item, nItem );
                    }
                case PocoTypeKind.Dictionary:
                    {
                        var k = collection.ItemTypes[0];
                        var v = collection.ItemTypes[1];
                        ref var nV = ref _result[v.Index >> 1];
                        Debug.Assert( nV.IsExchangeable );
                        if( k.Type == typeof( string ) )
                        {
                            return MakeDynamicObject( v, nV );
                        }
                        ref var nK = ref _result[k.Index >> 1];
                        Debug.Assert( nK.IsExchangeable );
                        return MakeMap( k, nK.NonNullable, v, nV );
                    }
            };
            return Throw.NotSupportedException<FullExchangeableTypeName>();
        }

        /// <summary>
        /// Returns <see cref="IPocoFamilyInfo.Name"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="primary">The primary Poco type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakePrimaryPocoName( IActivityMonitor monitor, IPrimaryPocoType primary )
        {
            return new FullExchangeableTypeName( primary.FamilyInfo.Name );
        }

        /// <summary>
        /// Returns the <see cref="IPocoType.CSharpName"/> by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="record">The enumeration type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeRecordName( IActivityMonitor monitor,
                                                                   IRecordPocoType record,
                                                                   Func<IPocoField, FullExchangeableTypeName> fieldTypeNames )
        {
            return new FullExchangeableTypeName( record.CSharpName );
        }

        /// <summary>
        /// Returns "R(n1,n2,...)" type name: field names are erased by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="record">The record.</param>
        /// <param name="fieldTypeNames">Fields type name provider.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeAnonymousRecordName( IActivityMonitor monitor,
                                                                            IRecordPocoType record,
                                                                            Func<IPocoField,FullExchangeableTypeName> fieldTypeNames )
        {
            _nBuilder.Append( "R(" );
            _sBuilder.Append( "R(" );
            bool atLeastOne = false;
            bool hasSimplified = false;
            foreach( var f in record.Fields )
            {
                if( atLeastOne )
                {
                    _nBuilder.Append( ',' );
                    _sBuilder.Append( ',' );
                }
                else atLeastOne = true;
                var nF = fieldTypeNames( f );
                hasSimplified |= nF.HasSimplifiedNames;
                _nBuilder.Append( f.Type.IsNullable ? nF.Nullable.Name : nF.Name );
                _sBuilder.Append( f.Type.IsNullable ? nF.Nullable.SimplifiedName : nF.SimplifiedName );
            }
            _nBuilder.Append( ')' );
            _sBuilder.Append( ')' );

            return hasSimplified
                        ? new FullExchangeableTypeName( new ExchangeableTypeName( _nBuilder.ToString(), _sBuilder.ToString() ) )
                        : new FullExchangeableTypeName( _nBuilder.ToString() );
        }

        /// <summary>
        /// Returns the <see cref="ObjectTypeName"/>.
        /// Union types are usually exchanged with the "object" type but this may be overridden.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="union">The union type.</param>
        /// <param name="typeNames">Type names provider.</param>
        /// <returns>By default, the "object" exchanged name.</returns>
        protected virtual FullExchangeableTypeName MakeUnionTypeName( IActivityMonitor monitor,
                                                                       IUnionPocoType union,
                                                                       Func<IPocoType, FullExchangeableTypeName> typeNames )
        {
            return ObjectTypeName;
        }

        /// <summary>
        /// Returns a "ItemName[]" pattern.
        /// </summary>
        /// <param name="item">The item type.</param>
        /// <param name="itemName">The exchanged name of the item type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeArrayName( IPocoType item, in FullExchangeableTypeName itemName )
        {
            var name = (item.IsNullable ? itemName.Nullable.Name : itemName.Name) + "[]";
            return itemName.HasSimplifiedNames
                    ? new FullExchangeableTypeName( new ExchangeableTypeName( name, (item.IsNullable ? itemName.Nullable.SimplifiedName : itemName.SimplifiedName) + "[]" ) )
                    : new FullExchangeableTypeName( name );
        }

        /// <summary>
        /// Returns a "L(T)" pattern.
        /// </summary>
        /// <param name="item">The item type.</param>
        /// <param name="itemName">The exchanged name of the item type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeListName( IPocoType item, in FullExchangeableTypeName itemName )
        {
            var name = $"L({(item.IsNullable ? itemName.Nullable.Name : itemName.Name)})";
            return itemName.HasSimplifiedNames
                    ? new FullExchangeableTypeName( new ExchangeableTypeName( name, $"L({(item.IsNullable ? itemName.Nullable.SimplifiedName : itemName.SimplifiedName)})" ) )
                    : new FullExchangeableTypeName( name );
        }

        /// <summary>
        /// Returns a "S(T)" pattern.
        /// </summary>
        /// <param name="item">The item type.</param>
        /// <param name="itemName">The exchanged name of the item type.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeSetName( IPocoType item, in FullExchangeableTypeName itemName )
        {
            var name = $"S({(item.IsNullable ? itemName.Nullable.Name : itemName.Name)})";
            return itemName.HasSimplifiedNames
                    ? new FullExchangeableTypeName( new ExchangeableTypeName( name, $"S({(item.IsNullable ? itemName.Nullable.SimplifiedName : itemName.SimplifiedName)})" ) )
                    : new FullExchangeableTypeName( name );
        }

        /// <summary>
        /// Returns a "M(TKey,TValue)" pattern. Note that key is necessarily not nullable.
        /// </summary>
        /// <param name="key">The key type.</param>
        /// <param name="keyName">The exchanged name of the key.</param>
        /// <param name="value">The type of the value.</param>
        /// <param name="valueName">The exchanged name of the value.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeMap( IPocoType key, in ExchangeableTypeName keyName, IPocoType value, in FullExchangeableTypeName valueName )
        {
            var nameK = $"M({keyName.Name},{(value.IsNullable ? valueName.Nullable.Name : valueName.Name)})";
            return keyName.HasSimplifiedNames || valueName.HasSimplifiedNames
                ? new FullExchangeableTypeName( new ExchangeableTypeName( nameK, $"M({keyName.SimplifiedName},{(value.IsNullable ? valueName.Nullable.SimplifiedName : valueName.SimplifiedName)})" ) )
                : new FullExchangeableTypeName( nameK );
        }

        /// <summary>
        /// Returns a "O(TValue)" pattern. This is map of string to TValue.
        /// </summary>
        /// <param name="value">The type of the value.</param>
        /// <param name="valueName">The exchanged name of the value.</param>
        /// <returns>The exchangeable name for the type.</returns>
        protected virtual FullExchangeableTypeName MakeDynamicObject( IPocoType value, in FullExchangeableTypeName valueName )
        {
            var name = $"O({(value.IsNullable ? valueName.Nullable.Name : valueName.Name)})";
            return valueName.HasSimplifiedNames
                ? new FullExchangeableTypeName( new ExchangeableTypeName( name, $"O({(value.IsNullable ? valueName.Nullable.SimplifiedName : valueName.SimplifiedName)})" ) )
                : new FullExchangeableTypeName( name );
        }


    }

}
