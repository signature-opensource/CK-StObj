using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        public static PrimaryPocoType CreatePrimaryPoco( PocoTypeSystem s, IPocoFamilyInfo family )
        {
            return new PrimaryPocoType( s, family, family.PrimaryInterface.PocoInterface );
        }

        internal sealed class PrimaryPocoType : PocoType, IConcretePocoType
        {
            sealed class Null : NullBasicRelay, IConcretePocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new PrimaryPocoType NonNullable => Unsafe.As<PrimaryPocoType>( base.NonNullable );

                public IPocoFamilyInfo Family => NonNullable.Family;

                public IConcretePocoType PrimaryInterface => NonNullable.PrimaryInterface.Nullable;

                public IReadOnlyList<IConcretePocoField> Fields => NonNullable.Fields;

                public IEnumerable<IConcretePocoType> AllowedTypes => NonNullable.AllowedTypes
                                                                        .Concat( NonNullable.AllowedTypes.Select( t => t.Nullable ) );

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IConcretePocoType IConcretePocoType.Nullable => this;

                IConcretePocoType IConcretePocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.Nullable => this;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.Nullable => this;

                IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.NonNullable => NonNullable;
            }

            public PrimaryPocoType( PocoTypeSystem s,
                                     IPocoFamilyInfo family,
                                     Type primaryInterface )
                : base( s, primaryInterface, primaryInterface.ToCSharpName(), PocoTypeKind.IPoco, t => new Null( t ) )
            {
                Family = family;
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IPocoFamilyInfo Family { get; }

            public IConcretePocoType PrimaryInterface => this;

            IConcretePocoType IConcretePocoType.Nullable => Nullable;

            IConcretePocoType IConcretePocoType.NonNullable => this;

            [AllowNull]
            public IReadOnlyList<IConcretePocoField> Fields { get; internal set; }

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => Fields;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            [AllowNull]
            public IEnumerable<IConcretePocoType> AllowedTypes { get; internal set; }

            IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.Nullable => Nullable;

            IUnionPocoType<IConcretePocoType> IUnionPocoType<IConcretePocoType>.NonNullable => this;
        }
    }

}



