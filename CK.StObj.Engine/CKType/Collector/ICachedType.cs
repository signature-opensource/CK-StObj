using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    public interface ICachedType
    {
        /// <summary>
        /// Gets the kind of this type.
        /// </summary>
        CKTypeKind RawKind { get; }

        /// <summary>
        /// Gets the type.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the CSharpName without reference type nullability information.
        /// </summary>
        string CSharpName { get; }

        /// <summary>
        /// Gets <see cref="CKTypeKind.None"/> if this type is a [CKTypeDefiner] or a [CKTypeSuperDefiner], otherwise <see cref="RawKind"/>
        /// is returned.
        /// </summary>
        CKTypeKind NonDefinerKind { get; }

        /// <summary>
        /// Gets <see cref="CKTypeKind.None"/> if this type is a [CKTypeDefiner] or a [CKTypeSuperDefiner] or <see cref="CKTypeKind.IsExcludedType"/>
        /// or <see cref="CKTypeKind.HasError"/>, otherwise <see cref="RawKind"/> is returned.
        /// </summary>
        CKTypeKind ValidKind { get; }

        /// <summary>
        /// Gets the base type if this is a class that inherits from another class:
        /// ultimate base <see cref="object"/> is skipped.
        /// </summary>
        ICachedType? Base { get; }

        /// <summary>
        /// Gets the direct base types. This starts with <see cref="Base"/> if there is one.
        /// </summary>
        ImmutableArray<ICachedType> DirectBases { get; }

        /// <summary>
        /// Gets all the base types (flattened <see cref="DirectBases"/>).
        /// </summary>
        ImmutableArray<ICachedType> AllBases { get; }

        /// <summary>
        /// Gets whether this is a generic type: <see cref="GenericDefinition"/> is not null.
        /// </summary>
        [MemberNotNullWhen( true, nameof( GenericDefinition ) )]
        bool IsGenericType { get; }

        /// <summary>
        /// See <see cref="Type.IsGenericTypeDefinition"/>.
        /// </summary>
        bool IsGenericTypeDefinition { get; }

        /// <summary>
        /// Gets the gereric type definition if <see cref="IsGenericType"/> is true.
        /// </summary>
        ICachedType? GenericDefinition { get; }

        /// <summary>
        /// Gets the generic type arguments if any.
        /// </summary>
        ImmutableArray<ICachedType> GenericArguments { get; }
    }

}
