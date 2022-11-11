using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static AbstractPocoType1 CreateAbstractPoco( PocoTypeSystem s,
                                                              Type tAbstract,
                                                              int abstractCount,
                                                              IReadOnlyList<IPocoType> abstractAndPrimary )
        {
            Debug.Assert( abstractAndPrimary.Take(abstractCount).All( t => t is IAbstractPocoType ) );
            Debug.Assert( abstractAndPrimary.Skip(abstractCount).All( t => t is IPrimaryPocoType ) );
            return new AbstractPocoType1( s, tAbstract, abstractCount, abstractAndPrimary );
        }

        internal static AbstractPocoType2 CreateAbstractPoco( PocoTypeSystem s,
                                                              Type tAbstract,
                                                              IReadOnlyList<IAbstractPocoType> abstracts,
                                                              IReadOnlyList<IPrimaryPocoType> primaries )
        {
            return new AbstractPocoType2( s, tAbstract, abstracts, primaries );
        }

        sealed class NullAbstractPoco : NullReferenceType, IAbstractPocoType
        {
            public NullAbstractPoco( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new IAbstractPocoType NonNullable => Unsafe.As<IAbstractPocoType>( NonNullable );

            public IEnumerable<IAbstractPocoType> OtherAbstractTypes => NonNullable.OtherAbstractTypes.Select( a => a.Nullable );

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => NonNullable.PrimaryPocoTypes.Select( a => a.Nullable );

            public IEnumerable<IPocoType> AllowedTypes => NonNullable.AllowedTypes.Concat( NonNullable.AllowedTypes.Select( a => a.Nullable ) );

            IAbstractPocoType IAbstractPocoType.Nullable => this;

            IAbstractPocoType IAbstractPocoType.NonNullable => NonNullable;

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.Nullable => this;

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.NonNullable => NonNullable;
        }

        internal sealed class AbstractPocoType1 : PocoType, IAbstractPocoType
        {
            readonly IReadOnlyList<IPocoType> _abstractAndPrimary;
            readonly int _abstractCount;

            public AbstractPocoType1( PocoTypeSystem s,
                                      Type tAbstract,
                                      int abstractCount,
                                      IReadOnlyList<IPocoType> abstractAndPrimary )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractIPoco, t => new NullAbstractPoco( t ) )
            {
                _abstractAndPrimary = abstractAndPrimary;
                _abstractCount = abstractCount;
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> OtherAbstractTypes => _abstractAndPrimary.Take( _abstractCount ).Cast<IAbstractPocoType>();

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _abstractAndPrimary.Skip( _abstractCount ).Cast<IPrimaryPocoType>();

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => _abstractAndPrimary.Skip( _abstractCount ).Append( this );

            public override bool IsWritableType( IExtNullabilityInfo type )
            {
                return !type.IsNullable
                        && (Type.IsAssignableFrom( type.Type ) || _abstractAndPrimary.Skip( _abstractCount ).Any( t => t.IsWritableType( type ) ));
            }

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.Nullable => Nullable;

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.NonNullable => this;
        }

        internal sealed class AbstractPocoType2 : PocoType, IAbstractPocoType
        {
            readonly IReadOnlyList<IAbstractPocoType> _abstracts;
            readonly IReadOnlyList<IPrimaryPocoType> _primaries;

            public AbstractPocoType2( PocoTypeSystem s,
                                      Type tAbstract,
                                      IReadOnlyList<IAbstractPocoType> abstracts,
                                      IReadOnlyList<IPrimaryPocoType> primaries )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractIPoco, t => new NullAbstractPoco( t ) )
            {
                _abstracts = abstracts;
                _primaries = primaries;
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> OtherAbstractTypes => _abstracts;

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _primaries;

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => _primaries;

            public override bool IsWritableType( IExtNullabilityInfo type )
            {
                return !type.IsNullable
                       && (Type.IsAssignableFrom( type.Type ) || _primaries.Any( t => t.IsWritableType( type ) ));
            }

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.Nullable => Nullable;

            IAnyOfPocoType<IPocoType> IAnyOfPocoType<IPocoType>.NonNullable => this;
        }

    }
}
