using CK.CodeGen;
using CK.Core;
using System;
using System.Diagnostics;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed partial class PocoPropertyInfo
        {



            CovarianceAdapter? GetCovariantAdapter( IActivityMonitor monitor,
                                                    PocoPropertyImpl a )
            {
                Debug.Assert( PocoPropertyKind != PocoPropertyKind.IPoco && PocoPropertyKind != PocoPropertyKind.None );
                Debug.Assert( !IsNullable || a.IsNullable, "writable is nullable => readable is nullable: check has already been done." );

                if( PocoPropertyKind == PocoPropertyKind.Union )
                {
                    Debug.Assert( _unionTypes != null );
                    if( a.UnionTypes != null )
                    {
                        // For the moment, they must be exactly the same as the writable one.
                        // (This may be changed once to allow a kind of covariance: as long as the readonly
                        // is a subset of the writable AND is nullable, we could accept.)
                        if( !CheckUnionTypeEquality( monitor, a ) )
                        {
                            return null;
                        }
                    }
                    // The read only property is not defined as a Union.
                    // Accepts it if its property type is assignable from the writable one.
                    if( a.PocoPropertyKind == PocoPropertyKind.Any
                        || a.NullableTypeTree.Type.IsAssignableFrom( PropertyNullableTypeTree.Type ) )
                    {
                        return new VoidAdapter();
                    }
                    monitor.Error( $"{a.NullableTypeTree} {a} is not compatible with property type {_propertyNullableTypeTree} {_best}." );
                    return null;
                }
                // Trivial case: abstraction is object or is assignable from the readonly type.
                if( a.PocoPropertyKind == PocoPropertyKind.Any
                    || a.NullableTypeTree.Type.IsAssignableFrom( c.PropertyNullableTypeTree.Type ) )
                {
                    return new VoidAdapter();
                }
                Debug.Assert( PocoPropertyKind == PocoPropertyKind.Basic
                              || PocoPropertyKind == PocoPropertyKind.ValueTuple
                              || PocoPropertyKind == PocoPropertyKind.Enum
                              || PocoPropertyKind == PocoPropertyKind.StandardCollection );
                return FindAdapter( monitor, _b)

            }

            abstract class CovarianceAdapter
            {
                public abstract CovarianceAdapter? Inner { get; }
                protected abstract void Write( ICodeWriter w, string vName );
            }

            sealed class VoidAdapter : CovarianceAdapter
            {
                public override CovarianceAdapter? Inner => null;
                protected override void Write( ICodeWriter w, string vName ) => w.Append( vName );
            }

            sealed class ValueTypeCastAdapter : CovarianceAdapter
            {
                public ValueTypeCastAdapter( Type type, bool toNullable, CovarianceAdapter inner )
                {
                    TypeCSharpName = type.ToCSharpName();
                    if( toNullable ) TypeCSharpName += "?";
                    Inner = inner;
                }

                public string TypeCSharpName { get; }
                public override CovarianceAdapter Inner { get; }

                protected override void Write( ICodeWriter w, string vName )
                {
                    w.Append("((").Append( TypeCSharpName ).Append(")").Append( vName ).Append(")");
                }
            }

            static CovarianceAdapter? FindAdapter( IActivityMonitor monitor, NullableTypeTree c, NullableTypeTree a )
            {
                if( c.Kind.IsNullable() && !a.Kind.IsNullable() )
                {
                    monitor.Error( $"Type '{a}' must be nullable since '{c}' is nullable." );
                    return null;
                }
                if( c.Type.IsValueType )
                {
                    return TryFindValueTypeAdapter( monitor, c, a );
                }
                if( a.Type.IsAssignableFrom( c.Type ) )
                {
                    return new VoidAdapter();
                }
                return r;
            }

            static CovarianceAdapter? TryFindValueTypeAdapter( IActivityMonitor monitor, NullableTypeTree c, NullableTypeTree a )
            {
                bool toNullable = !c.Kind.IsNullable() && a.Kind.IsNullable();
                CovarianceAdapter r = new VoidAdapter();
                if( c.Type == a.Type )
                {
                    r = new VoidAdapter();
                    if( toNullable ) r = new ValueTypeCastAdapter( a.Type, true, r );
                }
                else
                {
                    if( c.Type == typeof( int ) )
                    {
                        if( a.Type == typeof( long )
                            || a.Type == typeof( ulong )
                            || a.Type == typeof( Decimal ) )
                        {
                            r = new ValueTypeCastAdapter( a.Type, toNullable, r );
                        }
                    }
                    else
                    {
                        monitor.Error( $"Type {a.Type} cannot be automatically converted into {c.Type}." );
                        return null;
                    }
                }
                return r;
            }
        }

    }
}
