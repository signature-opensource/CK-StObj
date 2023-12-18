using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static AbstractPocoType1 CreateAbstractPoco( IActivityMonitor monitor,
                                                              PocoTypeSystem s,
                                                              Type tAbstract,
                                                              int abstractCount,
                                                              IReadOnlyList<IPocoType> abstractAndPrimary )
        {
            Debug.Assert( abstractAndPrimary.Take(abstractCount).All( t => t is IAbstractPocoType ) );
            Debug.Assert( abstractAndPrimary.Skip(abstractCount).All( t => t is IPrimaryPocoType ) );
            return new AbstractPocoType1( monitor, s, tAbstract, abstractCount, abstractAndPrimary );
        }

        internal static AbstractPocoType2 CreateAbstractPoco( IActivityMonitor monitor,
                                                              PocoTypeSystem s,
                                                              Type tAbstract,
                                                              IReadOnlyList<IAbstractPocoType> abstracts,
                                                              IReadOnlyList<IPrimaryPocoType> primaries )
        {
            return new AbstractPocoType2( monitor, s, tAbstract, abstracts, primaries );
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

            IOneOfPocoType IOneOfPocoType.Nullable => this;

            IOneOfPocoType IOneOfPocoType.NonNullable => NonNullable;
        }

        internal sealed class AbstractPocoType1 : PocoType, IAbstractPocoType
        {
            readonly IReadOnlyList<IPocoType> _abstractAndPrimary;
            readonly int _abstractCount;
            int _exchangeableCount;

            public AbstractPocoType1( IActivityMonitor monitor,
                                      PocoTypeSystem s,
                                      Type tAbstract,
                                      int abstractCount,
                                      IReadOnlyList<IPocoType> abstractAndPrimary )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, t => new NullAbstractPoco( t ) )
            {
                _abstractAndPrimary = abstractAndPrimary;
                _abstractCount = abstractCount;
                int exchangeableCount = 0;
                for( int i = 0; i < abstractAndPrimary.Count; i++ )
                {
                    IPocoType t = abstractAndPrimary[i];
                    _ = new PocoTypeRef( this, t, i );
                    if( t.IsExchangeable ) ++exchangeableCount;
                }
                if( (_exchangeableCount = exchangeableCount) == 0 )
                {
                    SetNotExchangeable( monitor, "no exchangeable Poco exist that implement it." );
                }
            }

            protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )
            {
                if( IsExchangeable )
                {
                    Debug.Assert( r.Owner == this && _abstractAndPrimary.Contains( r.Type ) );
                    if( --_exchangeableCount == 0 )
                    {
                        SetNotExchangeable( monitor, "no more exchangeable Poco implement it." );
                    }
                }
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> OtherAbstractTypes => _abstractAndPrimary.Take( _abstractCount ).Cast<IAbstractPocoType>();

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _abstractAndPrimary.Skip( _abstractCount ).Cast<IPrimaryPocoType>();

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => _abstractAndPrimary;

            //public override bool IsWritableType( IExtNullabilityInfo type )
            //{
            //    return !type.IsNullable
            //            && (Type.IsAssignableFrom( type.Type ) || _abstractAndPrimary.Skip( _abstractCount ).Any( t => t.IsWritableType( type ) ));
            //}

            /// <summary>
            /// <c>Type.IsAssignableFrom( type.Type )</c> is not enough.
            /// A primary interface may not extend this abstract interface (the abstract is defined on a secondary interface):
            /// We must check that the proposed type is compatible with any of our primary poco.
            /// <para>
            /// The base IsReadableType that simply challenges <c>type.Type.IsAssignableFrom( Type )</c> is fine: we don't need
            /// to override it.
            /// </para>
            /// </summary>
            /// <param name="type">The potential contravariant type.</param>
            /// <returns>True if the type is contravariant, false otherwise.</returns>
            public override bool IsWritableType( IPocoType type )
            {
                return type == this
                       || (!type.IsNullable
                            && (Type.IsAssignableFrom( type.Type )
                                || _abstractAndPrimary.Skip( _abstractCount ).Any( t => t.IsWritableType( type ) )));
            }

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;
        }

        internal sealed class AbstractPocoType2 : PocoType, IAbstractPocoType
        {
            readonly IReadOnlyList<IAbstractPocoType> _abstracts;
            readonly IReadOnlyList<IPrimaryPocoType> _primaries;
            int _exchangeableCount;

            public AbstractPocoType2( IActivityMonitor monitor,
                                      PocoTypeSystem s,
                                      Type tAbstract,
                                      IReadOnlyList<IAbstractPocoType> abstracts,
                                      IReadOnlyList<IPrimaryPocoType> primaries )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, t => new NullAbstractPoco( t ) )
            {
                _abstracts = abstracts;
                _primaries = primaries;
                int exchanchableCount = 0;
                int counAbstract = abstracts.Count;
                for( int i = 0; i < counAbstract; i++ )
                {
                    IAbstractPocoType t = abstracts[i];
                    _ = new PocoTypeRef( this, t, i );
                    if( t.IsExchangeable ) ++exchanchableCount;
                }
                for( int i = 0; i < primaries.Count; i++ )
                {
                    IPrimaryPocoType t = primaries[i];
                    _ = new PocoTypeRef( this, t, counAbstract + i );
                    if( t.IsExchangeable ) ++exchanchableCount;
                }
                if( (_exchangeableCount = exchanchableCount) == 0 )
                {
                    SetNotExchangeable( monitor, "no exchangeable Poco implement it." );
                }
            }

            protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )
            {
                if( IsExchangeable )
                {
                    Debug.Assert( r.Owner == this && _abstracts.Contains( r.Type ) || _primaries.Contains( r.Type ) );
                    if( --_exchangeableCount == 0 )
                    {
                        SetNotExchangeable( monitor, "no more exchangeable Poco implement it." );
                    }
                }
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> OtherAbstractTypes => _abstracts;

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _primaries;

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => ((IEnumerable<IPocoType>)_abstracts).Concat( _primaries );

            #region Type against IExtNullabilityInfo. Should be replaced by an Adapter factory.
            //public override bool IsWritableType( IExtNullabilityInfo type )
            //{
            //    return !type.IsNullable
            //           && (Type.IsAssignableFrom( type.Type ) || _primaries.Any( t => t.IsWritableType( type ) ));
            //}
            #endregion Waiting for the "Adapter factory".

            // See AbstractPocoType1.
            public override bool IsWritableType( IPocoType type )
            {
                return type == this
                       || (!type.IsNullable
                            && (Type.IsAssignableFrom( type.Type )
                                || _primaries.Any( t => t.IsWritableType( type ) )));
            }

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;
        }

    }
}
