using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static UnionType CreateUnion( IActivityMonitor monitor, PocoTypeSystem s, IReadOnlyList<IPocoType> allowedTypes )
        {
            return new UnionType( s, allowedTypes );
        }

        internal sealed class UnionType : PocoType, IUnionPocoType
        {
            sealed class Null : NullReferenceType, IUnionPocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new UnionType NonNullable => Unsafe.As<UnionType>( base.NonNullable );

                public IEnumerable<IPocoType> AllowedTypes => NonNullable.AllowedTypes.Concat( NonNullable.AllowedTypes.Select( a => a.Nullable ) );

                IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.NonNullable => NonNullable;
                IUnionPocoType IUnionPocoType.NonNullable => NonNullable;

                IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.Nullable => this;
                IUnionPocoType IUnionPocoType.Nullable => this;
            }
            IReadOnlyList<IPocoType> _allowedTypes;
            DefaultValueInfo _defInfo;

            public UnionType( PocoTypeSystem s, IReadOnlyList<IPocoType> allowedTypes )
                : base( s,
                        typeof(object),
                        "object",
                        PocoTypeKind.UnionType,
                        t => new Null( t ) )
            {
                _allowedTypes = allowedTypes;
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            IReadOnlyList<IPocoType> AllowedTypes => _allowedTypes;

            IEnumerable<IPocoType> IAnyOfPocoType<IPocoType>.AllowedTypes => _allowedTypes;

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.Nullable => Nullable;
            IUnionPocoType IUnionPocoType.Nullable => Nullable;

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.NonNullable => this;
            IUnionPocoType IUnionPocoType.NonNullable => this;


        }
    }

}
