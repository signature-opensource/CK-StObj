using CK.Core;
using System;
using System.Collections.Immutable;

namespace CK.Setup
{
    public sealed partial class CKTypeKindDetector
    {
        sealed class StructurallyExcludedType : IInternalCachedType
        {
            public static readonly StructurallyExcludedType Instance = new StructurallyExcludedType();

            public CKTypeKind RawKind => CKTypeKind.IsExcludedType;

            public Type Type => GetType();

            public string CSharpName => GetType().Name;

            public CKTypeKind NonDefinerKind => CKTypeKind.IsExcludedType;

            public CKTypeKind ValidKind => CKTypeKind.None;

            public ICachedType? Base => null;

            public ImmutableArray<ICachedType> DirectBases => ImmutableArray<ICachedType>.Empty;

            public ImmutableArray<ICachedType> AllBases => ImmutableArray<ICachedType>.Empty;

            public bool IsGenericType => false;

            public bool IsGenericTypeDefinition => false;

            public ICachedType? GenericDefinition => null;

            public CKTypeKind InternalKind => CKTypeKind.IsExcludedType;

            public ImmutableArray<Type> AllPublicInterfaces => ImmutableArray<Type>.Empty;

            public IInternalCachedType? InternalBase => null;

            public ImmutableArray<IInternalCachedType> InternalDirectBases => ImmutableArray<IInternalCachedType>.Empty;

            public ImmutableArray<IInternalCachedType> InternalAllBases => ImmutableArray<IInternalCachedType>.Empty;

            public ImmutableArray<IInternalCachedType> InternalGenericArguments => ImmutableArray<IInternalCachedType>.Empty;

            public ImmutableArray<ICachedType> GenericArguments => ImmutableArray<ICachedType>.Empty;

            public bool MergeKind( IActivityLineEmitter monitor, CKTypeKind k )
            {
                throw new NotSupportedException();
            }
        }
    }
}
