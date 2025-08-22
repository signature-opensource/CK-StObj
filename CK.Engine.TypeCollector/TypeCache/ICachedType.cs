using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Centralized type information.
/// <para>
/// Nullability is modeled via peer types for both value and reference types.
/// <c>ref struct</c>, <c>Nullable&lt;T&gt;</c> and <c>void</c> have no nullables: they are their own nullable.
/// </para>
/// </summary>
public interface ICachedType : ICachedItem
{
    /// <summary>
    /// Gets the Type.
    /// On a <see cref="ICachedType"/>, when the type is a value type and <see cref="ICachedType.IsNullable"/> is true,
    /// this is a <see cref="Nullable{T}"/>.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets whether this is a generic type.
    /// </summary>
    bool IsGenericType { get; }

    /// <summary>
    /// Gets whether this is a generic type definition.
    /// </summary>
    bool IsTypeDefinition { get; }

    /// <summary>
    /// Gets whether this is a super definer type: its direct specializations are definer,
    /// only the direct specializations of its direct specializations must be considered
    /// (unless they also are marked as definer or super definer).
    /// </summary>
    bool IsSuperTypeDefiner { get; }

    /// <summary>
    /// Gets whether this type is definer: it cannot exist by itself, only its direct specializations
    /// must be considered (unless <see cref="IsSuperTypeDefiner"/> is also true).
    /// </summary>
    bool IsTypeDefiner { get; }

    /// <summary>
    /// Gets the C# name with namespaces that ends with a '?' if <see cref="ICachedType.IsNullable"/> is true.
    /// </summary>
    string CSharpName { get; }

    /// <summary>
    /// Gets the assembly that defines this type.
    /// </summary>
    CachedAssembly Assembly { get; }

    /// <summary>
    /// Gets whether this type is nullable.
    /// This is <c>null</c> for reference type. Only value types have a <see cref="Nullable"/>
    /// that is not the same as their <see cref="NonNullable"/>.
    /// </summary>
    bool? IsNullable { get; }

    /// <summary>
    /// Gets the nullable associated type (this if <see cref="IsNullable"/> is null or true).
    /// </summary>
    ICachedType Nullable { get; }

    /// <summary>
    /// Gets the non nullable associated type (this if <see cref="IsNullable"/> is null or false).
    /// </summary>
    ICachedType NonNullable { get; }

    /// <summary>
    /// Gets all the public interfaces that this type implements.
    /// <para>
    /// Type analysis heavily relies on supported interfaces but only public ones.
    /// Internal interfaces are "transparent". They can bring some CKomposable interface (IAutoService, etc.)
    /// but we ignore them totally because no public interfaces can extend them (Error CS0061: Inconsistent accessibility).
    /// Implementations are free to define and use them.
    /// </para>
    /// </summary>
    ImmutableArray<ICachedType> Interfaces { get; }

    /// <summary>
    /// Gets the subset of <see cref="Interfaces"/> that are declared on the type itself. Interface indirectly
    /// provided by another interface or the <see cref="BaseType"/> don't appear here.
    /// </summary>
    ImmutableArray<ICachedType> DirectInterfaces { get; }

    /// <summary>
    /// Gets the base type if this type is a class that inherits from a class that is not <see cref="object"/>.
    /// This is always null for value types: <c>object</c> and <c>ValueType</c> are skipped.
    /// <para>
    /// This base type is nullable if this <see cref="IsNullable"/> is true.
    /// </para>
    /// </summary>
    ICachedType? BaseType { get; }

    /// <summary>
    /// Gets the <see cref="Interfaces"/> and all the <see cref="BaseType"/> excluding types that are <see cref="IsTypeDefiner"/>.
    /// <para>
    /// No specific check is done here: a base type or interface appears here even if it comes from a Type Definer.
    /// Type Definer trait si not propagated up to their generalizations.
    /// </para>
    /// </summary>
    IReadOnlySet<ICachedType> ConcreteGeneralizations { get; }

    /// <summary>
    /// Gets the unified depth of this type based on its <see cref="Interfaces"/> and <see cref="BaseType"/>.
    /// </summary>
    int TypeDepth { get; }

    /// <summary>
    /// Gets the generic type definition if this type is a generic type.
    /// <para>
    /// This can be this type.
    /// </para>
    /// </summary>
    ICachedType? GenericTypeDefinition { get; }

    /// <summary>
    /// Gets the type that declares the current nested type or generic type parameter.
    /// See <see cref="Type.DeclaringType"/>.
    /// </summary>
    ICachedType? DeclaringType { get; }

    /// <summary>
    /// Gets the generic parameters or aguments.
    /// </summary>
    ImmutableArray<ICachedType> GenericArguments { get; }

    /// <summary>
    /// Get the <see cref="CachedMember"/> declared by this type.
    /// <para>
    /// Non public members are not collected. Only public members are actually considered by the engine.
    /// </para>
    /// <para>
    /// Binding flags are <c>Public|Instance|Static|DeclaredOnly</c> and nested
    /// types (that are returned by <see cref="Type.GetMembers(BindingFlags)"/>) are filtered out.
    /// </para>
    /// </summary>
    ImmutableArray<CachedMember> DeclaredMembers { get; }

    /// <summary>
    /// Gets the element type of array, pointer, etc. See <see cref="Type.GetElementType()"/>.
    /// </summary>
    ICachedType? ElementType { get; }

    /// <summary>
    /// Gets whether this type is not a regular visible type and should almost always be ignored.
    /// </summary>
    EngineUnhandledType EngineUnhandledType { get; }
    /// <summary>
    /// Returns the <see cref="CSharpName"/>.
    /// </summary>
    /// <returns>This type C# name.</returns>
    string ToString();
}
