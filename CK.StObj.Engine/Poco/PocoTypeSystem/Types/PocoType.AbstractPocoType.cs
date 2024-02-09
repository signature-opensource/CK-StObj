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
                                                             PocoTypeSystemBuilder s,
                                                             Type tAbstract,
                                                             int abstractCount,
                                                             IPocoType[] abstractAndPrimary,
                                                             PocoGenericTypeDefinition? genericTypeDefinition )
        {
            Throw.DebugAssert( abstractAndPrimary.Take( abstractCount ).All( t => t is IAbstractPocoType ) );
            Throw.DebugAssert( abstractAndPrimary.Skip( abstractCount ).All( t => t is IPrimaryPocoType ) );
            return new AbstractPocoType( s, tAbstract, abstractCount, abstractAndPrimary, genericTypeDefinition );
        }

        internal static AbstractPocoBase CreateAbstractPocoBase( IActivityMonitor monitor,
                                                                 PocoTypeSystemBuilder s,
                                                                 List<IAbstractPocoType> abstracts,
                                                                 IPrimaryPocoType[] primaries )
        {
            return new AbstractPocoBase( s, abstracts, primaries );
        }

        internal static ImplementationLessAbstractPoco CreateImplementationLessAbstractPoco( PocoTypeSystemBuilder s,
                                                                                             Type tAbstract,
                                                                                             IReadOnlyList<IAbstractPocoType> generalizations,
                                                                                             PocoGenericTypeDefinition? genericTypeDefinition,
                                                                                             (IPocoGenericParameter Parameter, IPocoType Type)[]? genericArguments )
        {
            return new ImplementationLessAbstractPoco( s, tAbstract, generalizations, genericTypeDefinition, genericArguments );
        }

        sealed class NullAbstractPoco : NullReferenceType, IAbstractPocoType, IReadOnlyList<IPrimaryPocoType>
        {
            public NullAbstractPoco( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new IAbstractPocoType NonNullable => Unsafe.As<IAbstractPocoType>( base.NonNullable );

            public IEnumerable<IAbstractPocoType> Specializations => NonNullable.Specializations.Select( a => a.Nullable );

            public IEnumerable<IAbstractPocoType> Generalizations => NonNullable.Generalizations.Select( a => a.Nullable );

            public IEnumerable<IAbstractPocoType> AllGeneralizations => NonNullable.AllGeneralizations.Select( a => a.Nullable );

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => NonNullable.MinimalGeneralizations.Select( a => a.Nullable );

            public IReadOnlyList<IPrimaryPocoType> PrimaryPocoTypes => this;

            public IEnumerable<IPocoType> AllowedTypes => NonNullable.AllowedTypes.Concat( NonNullable.AllowedTypes.Select( a => a.Nullable ) );

            public ImmutableArray<IAbstractPocoField> Fields => NonNullable.Fields;

            public bool IsGenericType => NonNullable.IsGenericType;

            public IPocoGenericTypeDefinition? GenericTypeDefinition => NonNullable.GenericTypeDefinition;

            public IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments => NonNullable.GenericArguments;

            IAbstractPocoType IAbstractPocoType.Nullable => this;

            IAbstractPocoType IAbstractPocoType.NonNullable => NonNullable;

            IOneOfPocoType IOneOfPocoType.Nullable => this;

            IOneOfPocoType IOneOfPocoType.NonNullable => NonNullable;

            #region Primaries auto implementation.
            int IReadOnlyCollection<IPrimaryPocoType>.Count => NonNullable.PrimaryPocoTypes.Count;

            IPrimaryPocoType IReadOnlyList<IPrimaryPocoType>.this[int index] => NonNullable.PrimaryPocoTypes[index].Nullable;

            IEnumerator<IPrimaryPocoType> IEnumerable<IPrimaryPocoType>.GetEnumerator() => NonNullable.PrimaryPocoTypes.Select( t => t.Nullable ).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => NonNullable.PrimaryPocoTypes.Select( t => t.Nullable ).GetEnumerator();
            #endregion
        }

        interface IAbstractPocoImpl 
        {
            void AddImplementationLessSpecialization( ImplementationLessAbstractPoco s );
        }

        // For all AbstractPoco that have implementations except IPoco.
        internal sealed class AbstractPocoType : PocoType, IAbstractPocoType, IAbstractPocoImpl
        {
            readonly IPocoType[] _abstractAndPrimary;
            readonly IPocoGenericTypeDefinition? _genericTypeDefinition;
            ArraySegment<IPrimaryPocoType> _primaries;
            (IPocoGenericParameter Parameter, IPocoType Type)[] _genericArguments;
            List<ImplementationLessAbstractPoco>? _implLessSpecializations;
            ImmutableArray<IAbstractPocoField> _fields;
            object _allGeneralizations;
            List<IAbstractPocoType>? _minimalGeneralizations;

            public AbstractPocoType( PocoTypeSystemBuilder s,
                                     Type tAbstract,
                                     int abstractCount,
                                     IPocoType[] abstractAndPrimary,
                                     PocoGenericTypeDefinition? genericDefinitionType )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, static t => new NullAbstractPoco( t ) )
            {
                _abstractAndPrimary = abstractAndPrimary;
                _primaries = new ArraySegment<IPrimaryPocoType>( Unsafe.As<IPrimaryPocoType[]>( _abstractAndPrimary ), abstractCount, abstractAndPrimary.Length - abstractCount );
                _genericTypeDefinition = genericDefinitionType;
                _genericArguments = Array.Empty<(IPocoGenericParameter, IPocoType)>();
                genericDefinitionType?.AddInstance( this );
                _allGeneralizations = s;
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
                    var o = _abstractAndPrimary.Take( _abstractAndPrimary.Length - _primaries.Count ).Cast<IAbstractPocoType>();
                    return _implLessSpecializations != null ? o.Concat( _implLessSpecializations ) : o;
                }
            }

            void IAbstractPocoImpl.AddImplementationLessSpecialization( ImplementationLessAbstractPoco s ) => (_implLessSpecializations ??= new List<ImplementationLessAbstractPoco>()).Add( s );

            public IEnumerable<IAbstractPocoType> AllGeneralizations
            {
                get
                {
                    if( _allGeneralizations is not IEnumerable<IAbstractPocoType> g )
                    {
                        var ts = (PocoTypeSystemBuilder)_allGeneralizations;
                        _allGeneralizations = g = _type.GetInterfaces()
                                                    .Where( t => t != typeof( IPoco ) )
                                                    .Select( ts.FindByType )
                                                    .Where( i => i != null )
                                                    .Cast<IAbstractPocoType>()
                                                    .ToArray();
                    }
                    return g;
                }
            }

            public IEnumerable<IAbstractPocoType> Generalizations => AllGeneralizations.Where( g => !g.ImplementationLess );

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

                public bool IsReadOnly => !(_prop.CanWrite || _prop.PropertyType.IsByRef);
            }

            public ImmutableArray<IAbstractPocoField> Fields => _fields;

            internal bool CreateFields( IActivityMonitor monitor, PocoTypeSystemBuilder pocoTypeSystem )
            {
                Throw.DebugAssert( _fields.IsDefault );
                bool success = true;
                var props = Type.GetProperties();
                var b = ImmutableArray.CreateBuilder<IAbstractPocoField>( props.Length );
                foreach( var p in props )
                {
                    foreach( var t in _primaries )
                    {
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

            public IReadOnlyList<IPrimaryPocoType> PrimaryPocoTypes => _primaries;

            IAbstractPocoType IAbstractPocoType.Nullable => Nullable;

            IAbstractPocoType IAbstractPocoType.NonNullable => this;

            public IEnumerable<IPocoType> AllowedTypes
            {
                get
                {
                    return _implLessSpecializations != null ? _abstractAndPrimary.Concat( _implLessSpecializations ) : _abstractAndPrimary;
                }
            }

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
                bool hasImplementationLess = false;
                for( int i = 0; i < arguments.Length; i++ )
                {
                    IPocoType t = arguments[i].Type;
                    hasImplementationLess |= t.ImplementationLess;
                    _ = new PocoTypeRef( this, t, ~i );
                }
                _genericArguments = arguments;
                // Initial check for implementation less.
                if( hasImplementationLess && _primaries.Count != 0 )
                {
                    SetImplementationLess();
                }
            }

            public override bool ImplementationLess => _primaries.Count == 0;

            protected override void OnBackRefImplementationLess( IPocoType.ITypeRef r )
            {
                if( _primaries.Count != 0 ) SetImplementationLess();
            }

            internal override void SetImplementationLess()
            {
                Throw.DebugAssert( !ImplementationLess );
                // ImplementationLess propagation MUST occur only during the Initialize step.
                Throw.DebugAssert( "Generalizations has not been computed yet.", _allGeneralizations is not IEnumerable<IAbstractPocoType> );
                foreach( var p in _primaries.Cast<PrimaryPocoType>() )
                {
                    p.OnAbstractImplementationLess( this );
                }
                _primaries = ArraySegment<IPrimaryPocoType>.Empty;
                base.SetImplementationLess();
            }

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;
        }

        // Only for IPoco.
        internal sealed class AbstractPocoBase : PocoType, IAbstractPocoType, IAbstractPocoImpl
        {
            readonly List<IAbstractPocoType> _abstracts;
            readonly IReadOnlyList<IPrimaryPocoType> _primaries;

            public AbstractPocoBase( PocoTypeSystemBuilder s,
                                     List<IAbstractPocoType> abstracts,
                                     IPrimaryPocoType[] primaries )
                : base( s, typeof(IPoco), typeof( IPoco ).ToCSharpName(), PocoTypeKind.AbstractPoco, static t => new NullAbstractPoco( t ) )
            {
                _abstracts = abstracts;
                _primaries = primaries;
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public override bool ImplementationLess => _primaries.Count == 0;

            public IEnumerable<IAbstractPocoType> Specializations => _abstracts;

            void IAbstractPocoImpl.AddImplementationLessSpecialization( ImplementationLessAbstractPoco s ) => _abstracts.Add( s );

            public IEnumerable<IAbstractPocoType> Generalizations => Array.Empty<IAbstractPocoType>();

            public IEnumerable<IAbstractPocoType> AllGeneralizations => Array.Empty<IAbstractPocoType>();

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => Array.Empty<IAbstractPocoType>();

            public IReadOnlyList<IPrimaryPocoType> PrimaryPocoTypes => _primaries;

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

        // ImplementationLess (no PrimaryPoco).
        internal sealed class ImplementationLessAbstractPoco : PocoType, IAbstractPocoType, IAbstractPocoImpl
        {
            readonly IPocoGenericTypeDefinition? _genericTypeDefinition;
            readonly IReadOnlyList<IAbstractPocoType> _allGeneralizations;
            readonly (IPocoGenericParameter Parameter, IPocoType Type)[] _genericArguments;
            List<ImplementationLessAbstractPoco>? _implLessSpecializations;
            List<IAbstractPocoType>? _minimalGeneralizations;

            public ImplementationLessAbstractPoco( PocoTypeSystemBuilder s,
                                                   Type tAbstract,
                                                   IReadOnlyList<IAbstractPocoType> generalizations,
                                                   PocoGenericTypeDefinition? genericDefinitionType,
                                                   (IPocoGenericParameter Parameter, IPocoType Type)[]? genericArguments )
                : base( s, tAbstract, tAbstract.ToCSharpName(), PocoTypeKind.AbstractPoco, static t => new NullAbstractPoco( t ) )
            {
                _genericTypeDefinition = genericDefinitionType;
                _genericArguments = genericArguments ?? Array.Empty<(IPocoGenericParameter, IPocoType)>();
                _allGeneralizations = generalizations;
                foreach( var g in generalizations )
                {
                    ((IAbstractPocoImpl)g).AddImplementationLessSpecialization( this );
                }
                Throw.DebugAssert( !AllowedTypes.Any() );
            }

            new NullAbstractPoco Nullable => Unsafe.As<NullAbstractPoco>( base.Nullable );

            public override bool ImplementationLess => true;

            public IEnumerable<IAbstractPocoType> Specializations => (IEnumerable<IAbstractPocoType>?)_implLessSpecializations ?? Array.Empty<IAbstractPocoType>();

            void IAbstractPocoImpl.AddImplementationLessSpecialization( ImplementationLessAbstractPoco s ) => (_implLessSpecializations ??= new List<ImplementationLessAbstractPoco>()).Add( s );

            public IEnumerable<IAbstractPocoType> Generalizations => _allGeneralizations.Where( g => !g.ImplementationLess );

            public IEnumerable<IAbstractPocoType> AllGeneralizations => _allGeneralizations;

            public IEnumerable<IAbstractPocoType> MinimalGeneralizations => _minimalGeneralizations ??= AbstractPocoType.ComputeMinimal( Generalizations );

            public bool IsGenericType => _genericTypeDefinition != null;

            public IPocoGenericTypeDefinition? GenericTypeDefinition => _genericTypeDefinition;

            public IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments => _genericArguments;

            public IReadOnlyList<IPrimaryPocoType> PrimaryPocoTypes => Array.Empty<IPrimaryPocoType>();

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
