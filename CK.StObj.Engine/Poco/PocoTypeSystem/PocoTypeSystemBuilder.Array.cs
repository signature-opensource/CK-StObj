using CK.Core;
using System;
using System.Diagnostics;

namespace CK.Setup;

public sealed partial class PocoTypeSystemBuilder
{
    IPocoType? RegisterArray( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
    {
        Throw.DebugAssert( nType.ElementType != null );

        bool valid = ctx.EnterArray( monitor, nType );

        var tItem = Register( monitor, ctx, nType.ElementType );
        if( tItem == null || !valid ) return null;

        IPocoType? nonSecondaryConcreteCollection = null;
        if( tItem is ISecondaryPocoType sec )
        {
            nonSecondaryConcreteCollection = RegisterArray( sec.PrimaryPocoType.Type.MakeArrayType(), sec.PrimaryPocoType, null );
        }
        var result = RegisterArray( nType.Type, tItem, nonSecondaryConcreteCollection );
        Throw.DebugAssert( !result.IsNullable );
        return nType.IsNullable ? result.Nullable : result;
    }

    IPocoType RegisterArray( Type t, IPocoType tItem, IPocoType? nonSecondaryConcreteCollection )
    {
        var chsarpName = tItem.CSharpName + "[]";
        if( !_typeCache.TryGetValue( chsarpName, out var result ) )
        {
            // The oblivious array type is the array of its oblivious item type
            // and is the final type. It is also its own RegularCollection as an oblivious
            // anonymous record is unnamed.
            if( !_typeCache.TryGetValue( t, out var obliviousType ) )
            {
                var oName = tItem.ObliviousType.CSharpName + "[]";
                obliviousType = PocoType.CreateListOrSetOrArray( this,
                                                                 t,
                                                                 oName,
                                                                 oName,
                                                                 PocoTypeKind.Array,
                                                                 tItem.ObliviousType,
                                                                 null,
                                                                 null,
                                                                 null,
                                                                 nonSecondaryConcreteCollection?.ObliviousType.NonNullable ).Nullable;
                _typeCache.Add( t, obliviousType );
                _typeCache.Add( oName, obliviousType.NonNullable );
            }
            // If the item is oblivious then, it is the oblivious array.
            Throw.DebugAssert( obliviousType.IsNullable );
            if( tItem.IsOblivious ) return obliviousType.NonNullable;

            // Ensures that the RegularCollection exists if the item type is not compliant.
            Throw.DebugAssert( "Only abstract read only collections can have a null regular and a read only collection cannot be an item",
                               tItem.RegularType != null );
            IPocoType? regularType = null;
            IPocoType tIRegular = tItem.RegularType;
            if( tIRegular != tItem )
            {
                Throw.DebugAssert( tItem.Kind != PocoTypeKind.SecondaryPoco );
                var rName = tIRegular.CSharpName + "[]";
                if( !_typeCache.TryGetValue( rName, out regularType ) )
                {
                    regularType = PocoType.CreateListOrSetOrArray( this,
                                                                   t,
                                                                   rName,
                                                                   rName,
                                                                   PocoTypeKind.Array,
                                                                   tIRegular,
                                                                   obliviousType,
                                                                   obliviousType,
                                                                   null,
                                                                   null );
                    _typeCache.Add( rName, regularType );
                }
                Throw.DebugAssert( !regularType.IsNullable );
            }

            result = PocoType.CreateListOrSetOrArray( this,
                                                      t,
                                                      chsarpName,
                                                      chsarpName,
                                                      PocoTypeKind.Array,
                                                      tItem,
                                                      obliviousType,
                                                      obliviousType,
                                                      regularType,
                                                      nonSecondaryConcreteCollection );
            _typeCache.Add( chsarpName, result );
        }
        Throw.DebugAssert( !result.IsNullable );
        return result;
    }
}
