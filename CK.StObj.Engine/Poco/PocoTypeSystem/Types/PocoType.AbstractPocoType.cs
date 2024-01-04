using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
                                                              IPocoType[] abstractAndPrimary )
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

            public IEnumerable<IAbstractPocoType> Specializations => NonNullable.Specializations.Select( a => a.Nullable );

            public IEnumerable<IAbstractPocoType> Generalizations => NonNullable.Generalizations.Select( a => a.Nullable );

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => NonNullable.PrimaryPocoTypes.Select( a => a.Nullable );

            public IEnumerable<IPocoType> AllowedTypes => NonNullable.AllowedTypes.Concat( NonNullable.AllowedTypes.Select( a => a.Nullable ) );

            public ImmutableArray<IAbstractPocoField> Fields => NonNullable.Fields;

            IAbstractPocoType IAbstractPocoType.Nullable => this;

            IAbstractPocoType IAbstractPocoType.NonNullable => NonNullable;

            IOneOfPocoType IOneOfPocoType.Nullable => this;

            IOneOfPocoType IOneOfPocoType.NonNullable => NonNullable;
        }

        // For all AbstractPoco except IPoco and IClosedPoco.
        internal sealed class AbstractPocoType1 : PocoType, IAbstractPocoType
        {
            readonly IPocoType[] _abstractAndPrimary;
            readonly int _abstractCount;
            ImmutableArray<IAbstractPocoField> _fields;
            object _generalizations;
            int _exchangeableCount;

            public AbstractPocoType1( IActivityMonitor monitor,
                                      PocoTypeSystem s,
                                      Type tAbstract,
                                      int abstractCount,
                                      IPocoType[] abstractAndPrimary )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, t => new NullAbstractPoco( t ) )
            {
                _abstractAndPrimary = abstractAndPrimary;
                _abstractCount = abstractCount;
                _generalizations = s;
                int exchangeableCount = 0;
                for( int i = abstractCount; i < abstractAndPrimary.Length; i++ )
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
                    Throw.DebugAssert( r.Owner == this && _abstractAndPrimary.Skip( _abstractCount ).Contains( r.Type ) );
                    if( --_exchangeableCount == 0 )
                    {
                        SetNotExchangeable( monitor, "no more exchangeable Poco implement it." );
                    }
                }
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> Specializations => _abstractAndPrimary.Take( _abstractCount ).Cast<IAbstractPocoType>();

            public IEnumerable<IAbstractPocoType> Generalizations
            {
                get
                {
                    if( _generalizations is not IEnumerable<IAbstractPocoType> g )
                    {
                        var ts = (PocoTypeSystem)_generalizations;
                        _generalizations = g = _type.GetInterfaces()
                                                    .Where( t => t != typeof( IPoco ) )
                                                    .Select( ts.FindByType )
                                                    .Where( i => i != null )
                                                    .Cast<IAbstractPocoType>()
                                                    .ToArray();
                    }
                    return g;
                }
            }

            sealed class Field : IAbstractPocoField
            {
                readonly IPrimaryPocoField _field;
                readonly PropertyInfo _prop;

                public Field( IPrimaryPocoField f, PropertyInfo prop )
                {
                    _field = f;
                    _prop = prop;
                }

                public string Name => _field.Name;

                public IPocoType Type => _field.Type;

                public PropertyInfo Originator => _prop;
            }

            public ImmutableArray<IAbstractPocoField> Fields
            {
                get
                {
                    if( _fields.IsDefault )
                    {
                        var props = Type.GetProperties();
                        var b = ImmutableArray.CreateBuilder<IAbstractPocoField>( props.Length );
                        foreach( var p in props )
                        {
                            var n = p.Name;
                            var primaries = _abstractAndPrimary.AsSpan( _abstractCount );
                            foreach( var tP in primaries )
                            {
                                var t = Unsafe.As<IPrimaryPocoType>( tP );
                                var f = t.Fields.FirstOrDefault( f => f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly && f.Name == n );
                                if( f != null )
                                {
                                    b.Add( new Field( f, p ) );
                                    break;
                                }
                            }
                        }
                        _fields = b.Count == props.Length ? b.MoveToImmutable() : b.ToImmutableArray();
                    }
                    return _fields;
                }
            }

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _abstractAndPrimary.Skip( _abstractCount ).Cast<IPrimaryPocoType>();

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => _abstractAndPrimary;

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

        // Only for IPoco and IClosedPoco.
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
                Throw.DebugAssert( tAbstract == typeof( IPoco ) || tAbstract == typeof( IClosedPoco ) );
                _abstracts = abstracts;
                _primaries = primaries;
                int exchanchableCount = 0;
                int counAbstract = abstracts.Count;
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
                    Throw.DebugAssert( r.Owner == this && _primaries.Contains( r.Type ) );
                    if( --_exchangeableCount == 0 )
                    {
                        SetNotExchangeable( monitor, "no more exchangeable Poco implement it." );
                    }
                }
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> Specializations => _abstracts;

            public IEnumerable<IAbstractPocoType> Generalizations => Array.Empty<IAbstractPocoType>();

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _primaries;

            public ImmutableArray<IAbstractPocoField> Fields => ImmutableArray<IAbstractPocoField>.Empty;

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => ((IEnumerable<IPocoType>)_abstracts).Concat( _primaries );

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
