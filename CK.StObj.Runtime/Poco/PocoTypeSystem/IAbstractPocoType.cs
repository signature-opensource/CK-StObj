using CK.Core;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup;

/// <summary>
/// An abstract <see cref="IPoco"/>. See <see cref="IPocoDirectory.OtherInterfaces"/>.
/// </summary>
public interface IAbstractPocoType : IOneOfPocoType, IBaseCompositeType
{
    /// <summary>
    /// Gets the set of all other abstract IPoco types that specialize this one.
    /// <para>
    /// These are nullable types if this type is nullable.
    /// </para>
    /// </summary>
    IEnumerable<IAbstractPocoType> AllSpecializations { get; }

    /// <summary>
    /// Gets the set of abstract IPoco types that generalize this one excluding the <see cref="IPoco"/>
    /// and any <see cref="IPocoType.ImplementationLess"/> abstract poco.
    /// <para>
    /// These are nullable types if this type is nullable.
    /// </para>
    /// </summary>
    IEnumerable<IAbstractPocoType> Generalizations { get; }

    /// <summary>
    /// Gets the set of abstract IPoco types that generalize this one including any <see cref="IPocoType.ImplementationLess"/> abstract poco
    /// (the <see cref="IPoco"/> is excluded).
    /// <para>
    /// These are nullable types if this type is nullable.
    /// </para>
    /// </summary>
    IEnumerable<IAbstractPocoType> AllGeneralizations { get; }

    /// <summary>
    /// Gets the minimal set of <see cref="Generalizations"/> (no implementation less abstract poco),
    /// considering inheritance, generic parameter variance based on <see cref="IPocoType.IsSubTypeOf(IPocoType)"/>.
    /// <para>
    /// This uses and caches <see cref="PocoTypeExtensions.ComputeMinimal{T}(IEnumerable{T})"/>.
    /// </para>
    /// <para>
    /// The returned types are nullable if this one is the <see cref="Nullable"/>.
    /// </para>
    /// </summary>
    IEnumerable<IAbstractPocoType> MinimalGeneralizations { get; }

    /// <summary>
    /// Gets whether this interface is a generic type.
    /// </summary>
    [MemberNotNullWhen( true, nameof( GenericTypeDefinition ) )]
    bool IsGenericType { get; }

    /// <summary>
    /// Gets the generic type definition if this interface is a generic type.
    /// </summary>
    IPocoGenericTypeDefinition? GenericTypeDefinition { get; }

    /// <summary>
    /// Gets a non empty list of generic arguments if <see cref="IsGenericType"/> is true.
    /// </summary>
    IReadOnlyList<(IPocoGenericParameter Parameter, IPocoType Type)> GenericArguments { get; }

    /// <summary>
    /// Gets the set of <see cref="IPrimaryPocoType"/> of
    /// primary interfaces that are compatible with this abstract type.
    /// <para>
    /// These are nullable types if this type is nullable.
    /// </para>
    /// <para>
    /// Use <see cref="IOneOfPocoType.AllowedTypes"/> for the full set of compatible types
    /// (primary and abstract Poco types).
    /// </para>
    /// </summary>
    IReadOnlyList<IPrimaryPocoType> PrimaryPocoTypes { get; }

    /// <summary>
    /// Gets the fields.
    /// </summary>
    new ImmutableArray<IAbstractPocoField> Fields { get; }

    /// <inheritdoc cref="IPocoType.Nullable" />
    new IAbstractPocoType Nullable { get; }

    /// <inheritdoc cref="IPocoType.NonNullable" />
    new IAbstractPocoType NonNullable { get; }

    /// <inheritdoc cref="IPocoType.ObliviousType"/>
    /// <remarks>
    /// <see cref="IAbstractPocoType"/> returns the <see cref="Nullable"/>.
    /// </remarks>
    new IAbstractPocoType ObliviousType { get; }

}
