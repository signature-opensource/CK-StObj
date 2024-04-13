using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace CK.Setup
{
    public sealed partial class CachedType
    {
        internal const BindingFlags AllMemberBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        readonly Type _type;
        readonly CachedType? _genDefinition;
        ImmutableArray<Type> _genericArguments;
        string? _csharpName;
        CKTypeKind _kind;
        ImmutableArray<MemberInfo> _allMembers;
        readonly ImmutableArray<Type> _allPublicInterfaces;
        readonly ImmutableArray<CachedType> _directBases;
        ImmutableArray<CachedType> _allBases;

        // For closed generic.
        internal CachedType( Type type, CachedType genDefinition )
        {
            Throw.DebugAssert( type.IsGenericType && genDefinition.Type.IsGenericTypeDefinition );
            _type = type;
            _genDefinition = genDefinition;
            _kind = genDefinition._kind;
            _allPublicInterfaces = genDefinition._allPublicInterfaces;
            _directBases = _genDefinition._directBases;
        }

        // For value type (non generic). 
        internal CachedType( Type type, CKTypeKind k )
        {
            _type = type;
            _kind = k;
            _genericArguments = ImmutableArray<Type>.Empty;
            _allPublicInterfaces = ImmutableArray<Type>.Empty;
            _directBases = ImmutableArray<CachedType>.Empty;
            _allBases = ImmutableArray<CachedType>.Empty;
        }

        // For classes and interfaces.
        internal CachedType( Type type,
                             CKTypeKind k,
                             ImmutableArray<Type> allPublicInterfaces,
                             ImmutableArray<CachedType> directBases,
                             ImmutableArray<CachedType> allBases )
        {
            _type = type;
            _kind = k;
            _allPublicInterfaces = allPublicInterfaces;
            _directBases = directBases;
            _allBases = allBases;
        }

        internal bool MergeKind( IActivityMonitor monitor, CKTypeKind k )
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

        internal CKTypeKind InternalKind => _kind;

        /// <summary>
        /// Gets the kind of this type.
        /// </summary>
        public CKTypeKind RawKind => _kind & MaskPublicInfo;

        /// <summary>
        /// Gets <see cref="CKTypeKind.None"/> if this type is a [CKTypeDefiner] or a [CKTypeSuperDefiner], otherwise <see cref="RawKind"/>
        /// is returned.
        /// </summary>
        public CKTypeKind NonDefinerKind => (_kind & (IsDefiner | IsSuperDefiner)) == 0 ? _kind & MaskPublicInfo : CKTypeKind.None;

        /// <summary>
        /// Gets <see cref="CKTypeKind.None"/> if this type is a [CKTypeDefiner] or a [CKTypeSuperDefiner] or <see cref="CKTypeKind.IsExcludedType"/>
        /// or <see cref="CKTypeKind.HasError"/>, otherwise <see cref="RawKind"/> is returned.
        /// </summary>
        public CKTypeKind ValidKind => (_kind & (IsDefiner | IsSuperDefiner | CKTypeKind.IsExcludedType | CKTypeKind.HasError)) == 0
                                        ? _kind & MaskPublicInfo
                                        : CKTypeKind.None;

        public string CSharpName => _csharpName ??= _type.ToCSharpName();

        public Type Type => _type;

        [MemberNotNullWhen( true, nameof( GenericDefinition ) )]
        public bool IsGenericType => _genDefinition != null;

        public bool IsGenericTypeDefinition => _type.IsGenericTypeDefinition;

        public CachedType? GenericDefinition => _genDefinition;

        public ImmutableArray<Type> GenericArguments
        {
            get
            {
                if( _genericArguments.IsDefault )
                {
                    _genericArguments = _type.GetGenericArguments().ToImmutableArray();
                }
                return _genericArguments;
            }
        }

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

        public ImmutableArray<CachedType> DirectBases => _directBases;

        public ImmutableArray<CachedType> AllBases
        {
            get
            {
                if( _allBases.IsDefault )
                {
                    _allBases = _genDefinition != null
                                    ? _genDefinition.AllBases
                                    : CreateAllBases( _directBases, _allPublicInterfaces );
                }
                return _allBases;
            }
        }

        internal static ImmutableArray<CachedType> CreateAllBases( ImmutableArray<CachedType> directBases, ImmutableArray<Type> allPublicInterfaces )
        {
            var allBasesBuilder = new List<CachedType>( 4 + allPublicInterfaces.Length );
            foreach( var d in directBases )
            {
                foreach( var above in d.DirectBases )
                {
                    if( !allBasesBuilder.Contains( above ) )
                    {
                        allBasesBuilder.Add( above );
                        foreach( var allAbove in d.AllBases )
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
