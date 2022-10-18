using CK.Core;
using System;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static EnumPocoType CreateEnum( PocoTypeSystem s, Type tNotNull, Type tNull, IPocoType underlyingType )
        {
            return new EnumPocoType( s, tNotNull, tNull, underlyingType );
        }

        internal sealed class EnumPocoType : PocoType, IEnumPocoType
        {
            sealed class Null : NullBasicWithType, IEnumPocoType
            {
                public Null( IPocoType notNullable, Type type )
                    : base( notNullable, type )
                {
                }

                new EnumPocoType NonNullable => Unsafe.As<EnumPocoType>( base.NonNullable );

                public IPocoType UnderlyingType => NonNullable.UnderlyingType.Nullable;

                IEnumPocoType IEnumPocoType.NonNullable => NonNullable;

                IEnumPocoType IEnumPocoType.Nullable => this;
            }

            public EnumPocoType( PocoTypeSystem s, Type tNotNull, Type tNull, IPocoType underlyingType )
                : base( s, tNotNull, tNotNull.ToCSharpName(), PocoTypeKind.Enum, t => new Null( t, tNull ) )
            {
                UnderlyingType = underlyingType;
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoType UnderlyingType { get; }

            IEnumPocoType IEnumPocoType.Nullable => Nullable;

            IEnumPocoType IEnumPocoType.NonNullable => this;

        }
    }

}
