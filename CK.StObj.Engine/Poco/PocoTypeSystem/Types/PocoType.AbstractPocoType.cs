using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static AbstractPocoType CreateAbstractPoco( IActivityMonitor monitor,
                                                             PocoTypeSystem s,
                                                             Type tAbstract,
                                                             int abstractCount,
                                                             IPocoType[] abstractAndPrimary,
                                                             PocoGenericTypeDefinition? genericTypeDefinition )
        {
            Throw.DebugAssert( abstractAndPrimary.Take( abstractCount ).All( t => t is IAbstractPocoType ) );
            Throw.DebugAssert( abstractAndPrimary.Skip( abstractCount ).All( t => t is IPrimaryPocoType ) );
            return new AbstractPocoType( monitor, s, tAbstract, abstractCount, abstractAndPrimary, genericTypeDefinition );
        }

        internal static AbstractPocoBaseAndClosed CreateAbstractPocoBaseOrClosed( IActivityMonitor monitor,
                                                                                  PocoTypeSystem s,
                                                                                  Type tAbstract,
                                                                                  List<IAbstractPocoType> abstracts,
                                                                                  IPrimaryPocoType[] primaries )
        {
            Throw.DebugAssert( tAbstract == typeof( IPoco ) || tAbstract == typeof( IClosedPoco ) );
            return new AbstractPocoBaseAndClosed( monitor, s, tAbstract, abstracts, primaries );
        }

        internal static OrphanAbstractPoco CreateOrphanAbstractPoco( PocoTypeSystem s,
                                                                     Type tAbstract,
                                                                     IReadOnlyList<IAbstractPocoType> generalizations,
                                                                     PocoGenericTypeDefinition? genericTypeDefinition,
                                                                     (IPocoGenericParameter Parameter, IPocoType Type)[]? genericArguments )
        {
            return new OrphanAbstractPoco( s, tAbstract, generalizations, genericTypeDefinition, genericArguments );
        }

        sealed class NullAbstractPoco : NullReferenceType, IAbstractPocoType
        {
            public NullAbstractPoco( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new IAbstractPocoType NonNullable => Unsafe.As<IAbstractPocoType>( base.NonNullable );

            public IEnumerable<IAbstractPocoType> Specializations => NonNullable.Specializations.Select( a => a.Nullable );

            public IEnumerable<IAbstractPocoType> Generalizations => NonNullable.Generalizations.Select( a => a.Nullable );

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => NonNullable.MinimalGeneralizations.Select( a => a.Nullable );

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => NonNullable.PrimaryPocoTypes.Select( a => a.Nullable );

            public IEnumerable<IPocoType> AllowedTypes => NonNullable.AllowedTypes.Concat( NonNullable.AllowedTypes.Select( a => a.Nullable ) );

            public ImmutableArray<IAbstractPocoField> Fields => NonNullable.Fields;

            public bool IsGenericType => NonNullable.IsGenericType;

            public IPocoGenericTypeDefinition? GenericTypeDefinition => NonNullable.GenericTypeDefinition;

            public IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments => NonNullable.GenericArguments;

            IAbstractPocoType IAbstractPocoType.Nullable => this;

            IAbstractPocoType IAbstractPocoType.NonNullable => NonNullable;

            IOneOfPocoType IOneOfPocoType.Nullable => this;

            IOneOfPocoType IOneOfPocoType.NonNullable => NonNullable;

        }

        interface IAbstractPocoImpl 
        {
            void AddOrphanSpecializations( OrphanAbstractPoco s );
        }

        // For all AbstractPoco that have implementations except IPoco and IClosedPoco.
        internal sealed class AbstractPocoType : PocoType, IAbstractPocoType, IAbstractPocoImpl
        {
            readonly IPocoType[] _abstractAndPrimary;
            readonly int _abstractCount;
            readonly IPocoGenericTypeDefinition? _genericTypeDefinition;
            (IPocoGenericParameter Parameter, IPocoType Type)[] _genericArguments;
            List<OrphanAbstractPoco>? _orphanSpecializations;
            ImmutableArray<IAbstractPocoField> _fields;
            object _generalizations;
            List<IAbstractPocoType>? _minimalGeneralizations;
            int _exchangeableCount;

            public AbstractPocoType( IActivityMonitor monitor,
                                     PocoTypeSystem s,
                                     Type tAbstract,
                                     int abstractCount,
                                     IPocoType[] abstractAndPrimary,
                                     PocoGenericTypeDefinition? genericDefinitionType )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, static t => new NullAbstractPoco( t ) )
            {
                _abstractAndPrimary = abstractAndPrimary;
                _abstractCount = abstractCount;
                _genericTypeDefinition = genericDefinitionType;
                _genericArguments = Array.Empty<(IPocoGenericParameter, IPocoType)>();
                genericDefinitionType?.AddInstance( this );
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

            [MemberNotNullWhen( true, nameof(GenericTypeDefinition), nameof(GenericArguments))]
            public bool IsGenericType => _genericTypeDefinition != null;

            public IPocoGenericTypeDefinition? GenericTypeDefinition => _genericTypeDefinition;

            public IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments => _genericArguments;


            public IEnumerable<IAbstractPocoType> Specializations
            {
                get
                {
                    var o = _abstractAndPrimary.Take( _abstractCount ).Cast<IAbstractPocoType>();
                    return _orphanSpecializations != null ? o.Concat( _orphanSpecializations ) : o;
                }
            }

            void IAbstractPocoImpl.AddOrphanSpecializations( OrphanAbstractPoco s ) => (_orphanSpecializations ??= new List<OrphanAbstractPoco>()).Add( s );

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

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => _minimalGeneralizations ??= ComputeMinimal( Generalizations );

            internal static List<IAbstractPocoType> ComputeMinimal( IEnumerable<IAbstractPocoType> abstractTypes )
            {
                var result = new List<IAbstractPocoType>( abstractTypes );
                for( int i = 0; i < result.Count; i++ )
                {
                    var a = result[i];
                    int j = 0;
                    while( j < i )
                    {
                        if( result[j].CanReadFrom( a ) )
                        {
                            result.RemoveAt( i-- );
                            goto skip;
                        }
                        ++j;
                    }
                    while( ++j < result.Count )
                    {
                        if( result[j].CanReadFrom( a ) )
                        {
                            result.RemoveAt( i-- );
                            goto skip;
                        }
                    }
                    skip:;
                }
                return result;
            }


            sealed class Field : IAbstractPocoField
            {
                readonly AbstractPocoType _owner;
                readonly PropertyInfo _prop;
                readonly IPocoType _type;

                public Field( AbstractPocoType owner, PropertyInfo prop, IPocoType type )
                {
                    _owner = owner;
                    _prop = prop;
                    _type = type;
                }

                public string Name => _prop.Name;

                public IPocoType Type => _type;

                public IEnumerable<IPrimaryPocoField> Implementations => _owner.PrimaryPocoTypes.Select( p => p.Fields.First( f => f.Name == _prop.Name ) );

                public PropertyInfo Originator => _prop;
            }

            public ImmutableArray<IAbstractPocoField> Fields => _fields;

            internal bool CreateFields( IActivityMonitor monitor, PocoTypeSystem pocoTypeSystem )
            {
                Throw.DebugAssert( _fields.IsDefault );
                bool success = true;
                var props = Type.GetProperties();
                var b = ImmutableArray.CreateBuilder<IAbstractPocoField>( props.Length );
                foreach( var p in props )
                {
                    var primaries = _abstractAndPrimary.AsSpan( _abstractCount );
                    foreach( var tP in primaries )
                    {
                        var t = Unsafe.As<IPrimaryPocoType>( tP );
                        // Ensures that this field is actually implemented and is not an "optional property".
                        if( t.Fields.Any( f => f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly && f.Name == p.Name ) )
                        {
                            var mainType = pocoTypeSystem.Register( monitor, p );
                            if( success &= (mainType != null) )
                            {
                                b.Add( new Field( this, p, mainType! ) );
                                break;
                            }
                        }
                    }
                }
                if( !success ) return false;
                _fields = b.Count == props.Length ? b.MoveToImmutable() : b.ToImmutableArray();
                return true;
            }

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _abstractAndPrimary.Skip( _abstractCount ).Cast<IPrimaryPocoType>();

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => _abstractAndPrimary;

            public override bool CanReadFrom( IPocoType type )
            {
                if( base.CanReadFrom( type ) ) return true;
                return _genericTypeDefinition != null && IsGenericReadable( type, _genericTypeDefinition, _genericArguments );
            }

            internal static bool IsGenericReadable( IPocoType type,
                                                    IPocoGenericTypeDefinition typeDefinition,
                                                    (IPocoGenericParameter Parameter, IPocoType Type)[] _arguments )
            {
                if( type is IAbstractPocoType other && typeDefinition == other.GenericTypeDefinition )
                {
                    for( int i = 0; i < _arguments.Length; ++i )
                    {
                        var a = _arguments[i];
                        if( (a.Parameter.Attributes & GenericParameterAttributes.Covariant) != 0 )
                        {
                            if( !a.Type.CanReadFrom( other.GenericArguments[i].Type ) ) return false;
                        }
                        else if( (a.Parameter.Attributes & GenericParameterAttributes.Contravariant) != 0 )
                        {
                            if( !a.Type.CanWriteTo( other.GenericArguments[i].Type ) ) return false;
                        }
                        else if( a.Type != other.GenericArguments[i].Type ) return false;
                    }
                    return true;
                }
                return false;
            }

            internal void SetGenericArguments( (IPocoGenericParameter Parameter, IPocoType Type)[] arguments )
            {
                Throw.DebugAssert( _genericTypeDefinition != null && _genericTypeDefinition.Parameters.Count == arguments.Length );
                _genericArguments = arguments;
            }

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;
        }

        // Only for IPoco and IClosedPoco.
        internal sealed class AbstractPocoBaseAndClosed : PocoType, IAbstractPocoType, IAbstractPocoImpl
        {
            readonly List<IAbstractPocoType> _abstracts;
            readonly IReadOnlyList<IPrimaryPocoType> _primaries;
            int _exchangeableCount;

            public AbstractPocoBaseAndClosed( IActivityMonitor monitor,
                                      PocoTypeSystem s,
                                      Type tAbstract,
                                      List<IAbstractPocoType> abstracts,
                                      IPrimaryPocoType[] primaries )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, static t => new NullAbstractPoco( t ) )
            {
                _abstracts = abstracts;
                _primaries = primaries;
                int exchanchableCount = 0;
                int counAbstract = abstracts.Count;
                for( int i = 0; i < primaries.Length; i++ )
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

            void IAbstractPocoImpl.AddOrphanSpecializations( OrphanAbstractPoco s ) => _abstracts.Add( s );

            public IEnumerable<IAbstractPocoType> Generalizations => Array.Empty<IAbstractPocoType>();

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => Array.Empty<IAbstractPocoType>();

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => _primaries;

            public ImmutableArray<IAbstractPocoField> Fields => ImmutableArray<IAbstractPocoField>.Empty;

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes => ((IEnumerable<IPocoType>)_abstracts).Concat( _primaries );

            public bool IsGenericType => false;

            public IPocoGenericTypeDefinition? GenericTypeDefinition => null;

            public IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments => Array.Empty<(IPocoGenericParameter, IPocoType)>();

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;

        }

        // Orphans (no PrimaryPoco). Not exchangeable.
        internal sealed class OrphanAbstractPoco : PocoType, IAbstractPocoType, IAbstractPocoImpl
        {
            readonly IPocoGenericTypeDefinition? _genericTypeDefinition;
            readonly IReadOnlyList<IAbstractPocoType> _generalizations;
            readonly (IPocoGenericParameter Parameter, IPocoType Type)[] _genericArguments;
            List<OrphanAbstractPoco>? _orphanSpecializations;
            List<IAbstractPocoType>? _minimalGeneralizations;

            public OrphanAbstractPoco( PocoTypeSystem s,
                                       Type tAbstract,
                                       IReadOnlyList<IAbstractPocoType> generalizations,
                                       PocoGenericTypeDefinition? genericDefinitionType,
                                       (IPocoGenericParameter Parameter, IPocoType Type)[]? genericArguments )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, static t => new NullAbstractPoco( t ), isExchangeable: false )
            {
                _genericTypeDefinition = genericDefinitionType;
                _genericArguments = genericArguments ?? Array.Empty<(IPocoGenericParameter, IPocoType)>();
                _generalizations = generalizations;
                foreach( var g in generalizations )
                {
                    ((IAbstractPocoImpl)g).AddOrphanSpecializations( this );
                }
                Throw.DebugAssert( !AllowedTypes.Any() );
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public IEnumerable<IAbstractPocoType> Specializations => (IEnumerable<IAbstractPocoType>?)_orphanSpecializations ?? Array.Empty<IAbstractPocoType>();

            void IAbstractPocoImpl.AddOrphanSpecializations( OrphanAbstractPoco s ) => (_orphanSpecializations ??= new List<OrphanAbstractPoco>()).Add( s );

            public IEnumerable<IAbstractPocoType> Generalizations => _generalizations;

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => _minimalGeneralizations ??= AbstractPocoType.ComputeMinimal( _generalizations );

            public bool IsGenericType => _genericTypeDefinition != null;

            public IPocoGenericTypeDefinition? GenericTypeDefinition => _genericTypeDefinition;

            public IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments => _genericArguments;

            public IEnumerable<IPrimaryPocoType> PrimaryPocoTypes => Array.Empty<IPrimaryPocoType>();

            public ImmutableArray<IAbstractPocoField> Fields => ImmutableArray<IAbstractPocoField>.Empty;

            public IEnumerable<IPocoType> AllowedTypes => Specializations;

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;

            public override bool CanReadFrom( IPocoType type )
            {
                if( base.CanReadFrom( type ) ) return true;
                return _genericTypeDefinition != null && AbstractPocoType.IsGenericReadable( type, _genericTypeDefinition, _genericArguments );
            }

        }
    }
}
