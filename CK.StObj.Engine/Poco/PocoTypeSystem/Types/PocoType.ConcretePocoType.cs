using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static ConcretePocoType CreatePoco( PocoTypeSystem s, PrimaryPocoType primary, Type tInterface )
        {
            return new ConcretePocoType( s, primary, tInterface );
        }

        internal sealed class ConcretePocoType : PocoType, IConcretePocoType
        {
            sealed class Null : NullReferenceType, IConcretePocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new ConcretePocoType NonNullable => Unsafe.As<ConcretePocoType>( base.NonNullable );

                public IPocoFamilyInfo FamilyInfo => NonNullable.FamilyInfo;

                public IPrimaryPocoType PrimaryInterface => NonNullable.PrimaryInterface.Nullable;

                IConcretePocoType IConcretePocoType.Nullable => this;
                IConcretePocoType IConcretePocoType.NonNullable => NonNullable;

                public IReadOnlyList<IPrimaryPocoField> Fields => NonNullable.Fields;

                public IEnumerable<IConcretePocoType> AllowedTypes => NonNullable.PrimaryInterface.Nullable.AllowedTypes;

                IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.Nullable => this;
                IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.NonNullable => NonNullable;
            }

            readonly PrimaryPocoType _primary;

            public ConcretePocoType( PocoTypeSystem s,
                                     PrimaryPocoType primary,
                                     Type tInterface )
                : base( s, tInterface, tInterface.ToCSharpName(), PocoTypeKind.IPoco, t => new Null( t ) )
            {
                _primary = primary;
            }

            public override DefaultValueInfo DefaultValueInfo => _primary.DefaultValueInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoFamilyInfo FamilyInfo => _primary.FamilyInfo;

            public IPrimaryPocoType PrimaryInterface => _primary;

            public override bool IsWritableType( Type type ) => _primary.IsWritableType( type );

            public override bool IsReadableType( Type type ) => _primary.IsReadableType( type );

            public IReadOnlyList<IPrimaryPocoField> Fields => _primary.Fields;

            IConcretePocoType IConcretePocoType.Nullable => Nullable;

            IConcretePocoType IConcretePocoType.NonNullable => this;

            public IEnumerable<IConcretePocoType> AllowedTypes => _primary.AllowedTypes;

            IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.Nullable => Nullable;

            IAnyOfPocoType<IConcretePocoType> IAnyOfPocoType<IConcretePocoType>.NonNullable => this;
        }

    }
}
