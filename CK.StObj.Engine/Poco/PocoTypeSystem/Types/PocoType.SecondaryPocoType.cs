using CK.Core;
using System;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static SecondaryPocoType CreateSecondaryPocoType( PocoTypeSystemBuilder s, Type interfaceType, PrimaryPocoType primary )
        {
            return new SecondaryPocoType( s, interfaceType, primary );
        }

        internal sealed class SecondaryPocoType : PocoType, ISecondaryPocoType
        {
            readonly IPrimaryPocoType _primary;

            sealed class Null : NullReferenceType, ISecondaryPocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new SecondaryPocoType NonNullable => Unsafe.As<SecondaryPocoType>( base.NonNullable );

                public IPrimaryPocoType PrimaryPocoType => NonNullable._primary.Nullable;

                ISecondaryPocoType ISecondaryPocoType.NonNullable => NonNullable;

                IPocoType IPocoType.ObliviousType => NonNullable.ObliviousType;

                IPrimaryPocoType ISecondaryPocoType.ObliviousType => NonNullable.ObliviousType;

                ISecondaryPocoType ISecondaryPocoType.Nullable => this;
            }

            public SecondaryPocoType( PocoTypeSystemBuilder s,
                                      Type interfaceType,
                                      PrimaryPocoType primary )
                : base( s, interfaceType, interfaceType.ToCSharpName(), PocoTypeKind.SecondaryPoco, static t => new Null( t ) )
            {
                _primary = primary;
            }

            public override DefaultValueInfo DefaultValueInfo => _primary.DefaultValueInfo;

            public override bool CanReadFrom( IPocoType type )
            {
                return _primary.CanReadFrom( type );
            }

            new ISecondaryPocoType Nullable => Unsafe.As<ISecondaryPocoType>( _nullable );

            ISecondaryPocoType ISecondaryPocoType.Nullable => Nullable;

            ISecondaryPocoType ISecondaryPocoType.NonNullable => this;

            public override IPrimaryPocoType ObliviousType => _primary;

            IPrimaryPocoType ISecondaryPocoType.PrimaryPocoType => _primary;
        }
    }

}



