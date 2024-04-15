using CK.Core;
using System;
using System.Collections.Immutable;

namespace CK.Setup
{
    interface IInternalCachedType : ICachedType
    {
        ImmutableArray<Type> AllPublicInterfaces { get; }

        IInternalCachedType? InternalBase { get; }

        ImmutableArray<IInternalCachedType> InternalDirectBases { get; }

        ImmutableArray<IInternalCachedType> InternalAllBases { get; }

        ImmutableArray<IInternalCachedType> InternalGenericArguments { get; }

        CKTypeKind InternalKind { get; }

        bool MergeKind( IActivityLineEmitter monitor, CKTypeKind k );
    }

}
