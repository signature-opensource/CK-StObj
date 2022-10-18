using CK.Core;
using System;
using System.Collections.Generic;
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
            private readonly IConcretePocoType _primary;

            sealed class Null : NullBasicRelay, IConcretePocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new ConcretePocoType NonNullable => Unsafe.As<ConcretePocoType>( base.NonNullable );

                public IPocoFamilyInfo Family => NonNullable.Family;

                public IConcretePocoType PrimaryInterface => NonNullable._primary.Nullable;

                IConcretePocoType IConcretePocoType.Nullable => this;
                IConcretePocoType IConcretePocoType.NonNullable => NonNullable;

                public IReadOnlyList<IConcretePocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                ICompositePocoType ICompositePocoType.Nullable => this;
                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                public IEnumerable<IConcretePocoType> AllowedTypes => NonNullable._primary.Nullable.AllowedTypes;

                IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.Nullable => this;
                IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.NonNullable => NonNullable;
            }

            public ConcretePocoType( PocoTypeSystem s,
                                     PrimaryPocoType primary,
                                     Type tInterface )
                : base( s, tInterface, tInterface.ToCSharpName(), PocoTypeKind.IPoco, t => new Null( t ) )
            {
                _primary = primary;
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoFamilyInfo Family => _primary.Family;

            public IConcretePocoType PrimaryInterface => _primary;

            public IReadOnlyList<IConcretePocoField> Fields => _primary.Fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _primary.Fields;

            IConcretePocoType IConcretePocoType.Nullable => Nullable;

            IConcretePocoType IConcretePocoType.NonNullable => this;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            public IEnumerable<IConcretePocoType> AllowedTypes => _primary.AllowedTypes;

            IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.Nullable => Nullable;

            IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.NonNullable => this;
        }

    }
}
