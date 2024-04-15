using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace CK.Setup
{
    public sealed partial class CKTypeKindDetector
    {
        sealed partial class CachedType : IInternalCachedType
        {
            internal const BindingFlags AllMemberBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            readonly Type _type;
            readonly IInternalCachedType? _genDefinition;
            readonly ImmutableArray<CustomAttributeData> _attributesData;
            readonly ImmutableArray<Type> _allPublicInterfaces;
            readonly ImmutableArray<IInternalCachedType> _directBases;
            // Holds GenericArguments or ElementType.
            readonly ImmutableArray<IInternalCachedType> _subTypes;
            ImmutableArray<IInternalCachedType> _allBases;
            ImmutableArray<Type> _genericArguments;
            string? _csharpName;
            ImmutableArray<MemberInfo> _allMembers;
            CKTypeKind _kind;

            // For closed generic.
            internal CachedType( Type type, IInternalCachedType genDefinition )
            {
                Throw.DebugAssert( type.IsGenericType && genDefinition.Type.IsGenericTypeDefinition );
                _type = type;
                _genDefinition = genDefinition;
                _kind = genDefinition.InternalKind;
                _attributesData = ImmutableArray<CustomAttributeData>.Empty;
                _directBases = _genDefinition.InternalDirectBases;
                _subTypes = ImmutableArray<IInternalCachedType>.Empty;
            }

            // For value type. 
            internal CachedType( Type type, CKTypeKind k, ImmutableArray<CustomAttributeData> attributesData )
            {
                _type = type;
                _kind = k;
                _attributesData = attributesData;
                _genericArguments = ImmutableArray<Type>.Empty;
                _directBases = ImmutableArray<IInternalCachedType>.Empty;
                _allBases = ImmutableArray<IInternalCachedType>.Empty;
            }

            // For classes and interfaces.
            internal CachedType( Type type,
                                 CKTypeKind k,
                                 ImmutableArray<CustomAttributeData> attributesData,
                                 ImmutableArray<Type> allPublicInterfaces,
                                 ImmutableArray<IInternalCachedType> directBases,
                                 ImmutableArray<IInternalCachedType> allBases )
            {
                Throw.DebugAssert( (k & HasBaseType) == 0 || directBases.Length > 0 );
                _type = type;
                _kind = k;
                _attributesData = attributesData;
                _allPublicInterfaces = allPublicInterfaces;
                _directBases = directBases;
                _allBases = allBases;
            }

            public bool MergeKind( IActivityLineEmitter monitor, CKTypeKind k )
            {
                var updated = _kind | k;
                string? error = (updated & MaskPublicInfo).GetCombinationError( _type.IsClass );
                if( error != null )
                {
                    monitor.Error( $"Type '{_type}' is already registered as a '{ToStringFull( _kind )}'. It can not be defined as {ToStringFull( k )}. Error: {error}" );
                    return false;
                }
                _kind = updated;
                return true;
            }

            public CKTypeKind InternalKind => _kind;

            public CKTypeKind RawKind => _kind & MaskPublicInfo;

            public CKTypeKind NonDefinerKind => (_kind & (IsDefiner | IsSuperDefiner)) == 0 ? _kind & MaskPublicInfo : CKTypeKind.None;

            public CKTypeKind ValidKind => (_kind & (IsDefiner | IsSuperDefiner | CKTypeKind.IsExcludedType | CKTypeKind.HasError)) == 0
                                            ? _kind & MaskPublicInfo
                                            : CKTypeKind.None;

            public string CSharpName => _csharpName ??= _type.ToCSharpName();

            public Type Type => _type;

            public ICachedType? Base => InternalBase;

            public IInternalCachedType? InternalBase => (_kind & HasBaseType) != 0 ? _directBases[0] : null;

            [MemberNotNullWhen( true, nameof( GenericDefinition ) )]
            public bool IsGenericType => _genDefinition != null;

            public bool IsGenericTypeDefinition => _type.IsGenericTypeDefinition;

            public ICachedType? GenericDefinition => _genDefinition;

            public ImmutableArray<ICachedType> GenericArguments => ImmutableArray<ICachedType>.CastUp( _subTypes );

            public ImmutableArray<IInternalCachedType> InternalGenericArguments => _subTypes;

            public ImmutableArray<MemberInfo> AllMembers
            {
                get
                {
                    if( _allMembers.IsDefault )
                    {
                        _allMembers = _type.GetMembers( AllMemberBindingFlags ).ToImmutableArray();
                    }
                    return _allMembers;
                }
            }

            public ImmutableArray<ICachedType> DirectBases => ImmutableArray<ICachedType>.CastUp( _directBases );

            public ImmutableArray<IInternalCachedType> InternalDirectBases => _directBases;

            public ImmutableArray<IInternalCachedType> InternalAllBases
            {
                get
                {
                    if( _allBases.IsDefault )
                    {
                        _allBases = _genDefinition != null
                                        ? _genDefinition.InternalAllBases
                                        : CreateAllBases( _directBases, _allPublicInterfaces );
                    }
                    return _allBases;
                }
            }

            public ImmutableArray<ICachedType> AllBases => ImmutableArray<ICachedType>.CastUp( InternalAllBases );

            public ImmutableArray<Type> AllPublicInterfaces => _allPublicInterfaces;

            public ImmutableArray<CustomAttributeData> AttributesData => _attributesData;

            internal static ImmutableArray<IInternalCachedType> CreateAllBases( ImmutableArray<IInternalCachedType> directBases, ImmutableArray<Type> allPublicInterfaces )
            {
                var allBasesBuilder = new List<IInternalCachedType>( 4 + allPublicInterfaces.Length );
                foreach( var d in directBases )
                {
                    foreach( var above in d.InternalDirectBases )
                    {
                        if( !allBasesBuilder.Contains( above ) )
                        {
                            allBasesBuilder.Add( above );
                            foreach( var allAbove in d.InternalAllBases )
                            {
                                if( !allBasesBuilder.Contains( allAbove ) )
                                {
                                    allBasesBuilder.Add( allAbove );
                                }
                            }
                        }
                    }
                }
                var allBases = allBasesBuilder.ToImmutableArray();
                return allBases;
            }
        }
    }
}
