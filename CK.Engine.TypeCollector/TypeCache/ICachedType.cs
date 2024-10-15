using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Centralized type information.
/// <para>
/// Nullability is modeled via peer types for both value and reference types.
/// <c>ref struct</c>, <c>Nullable&lt;T&gt;</c> and <c>void</c> have no nullables: they are their own nullable.
/// </para>
/// When <see cref="IsTypeDefinition"/> is true, the cached type doesn't have any <see cref="Interfaces"/>, <see cref="BaseType"/>,
/// <see cref="DeclaredMethodInfos"/> etc. because we don't need to work with type definitions. If it happens to be useful.
/// </summary>
public interface ICachedType
{
    /// <summary>
    /// Gets the Type.
    /// On a <see cref="ICachedType"/>, when the type is a value type and <see cref="ICachedType.IsNullable"/> is true,
    /// this is a <see cref="Nullable{T}"/>.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets whether this is a generic type: it has <see cref="GenericArguments"/> but no <see cref="GenericParameters"/>.
    /// </summary>
    bool IsGenericType { get; }

    /// <summary>
    /// Gets whether this is a generic type definition: it has <see cref="GenericParameters"/> but no <see cref="GenericArguments"/>.
    /// </summary>
    bool IsTypeDefinition { get; }

    /// <summary>
    /// Gets the C# name with namespaces that ends with a '?' if <see cref="ICachedType.IsNullable"/> is true.
    /// When <see cref="IsTypeDefinition"/> is true, the parameter names are the <see cref="CachedGenericParameter.Name"/>.
    /// </summary>
    string CSharpName { get; }

    /// <summary>
    /// Gets the assembly that defines this type.
    /// </summary>
    CachedAssembly Assembly { get; }

    /// <summary>
    /// Gets whether this type is nullable.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Gets the nullable associated type (this if <see cref="IsNullable"/> is true).
    /// </summary>
    ICachedType Nullable { get; }

    /// <summary>
    /// Gets the non nullable associated type (this if <see cref="IsNullable"/> is false).
    /// </summary>
    ICachedType NonNullable { get; }

    /// <summary>
    /// Gets the public interfaces that this type implements.
    /// <para>
    /// Type analysis heavily relies on supported interfaces but only public ones.
    /// Internal interfaces are "transparent". They can bring some CKomposable interface (IAutoService, etc.)
    /// but we ignore them totally because no public interfaces can extend them (Error CS0061: Inconsistent accessibility).
    /// Implementations are free to define and use them.
    /// </para>
    /// <para>
    /// When <see cref="IsTypeDefinition"/> is true, this is empty.
    /// </para>
    /// </summary>
    ImmutableArray<ICachedType> Interfaces { get; }

    /// <summary>
    /// Gets the base type if this type is a class that inherits from a class that is not <see cref="object"/>.
    /// <para>
    /// This base type is nullable if this <see cref="IsNullable"/> is true.
    /// </para>
    /// <para>
    /// When <see cref="IsTypeDefinition"/> is true, this is always null.
    /// </para>
    /// </summary>
    ICachedType? BaseType { get; }

    /// <summary>
    /// Gets the unified depth of this type based on its <see cref="Interfaces"/> and <see cref="BaseType"/>.
    /// </summary>
    int TypeDepth { get; }

    /// <summary>
    /// Gets the generic type definition if this type is a generic type.
    /// </summary>
    ICachedType? GenericTypeDefinition { get; }

    /// <summary>
    /// Gets the generic parameters: always empty when <see cref="IsTypeDefinition"/> is false.
    /// </summary>
    ImmutableArray<CachedGenericParameter> GenericParameters { get; }

    /// <summary>
    /// Gets the generic parameters: always empty when <see cref="IsGenericType"/> is false.
    /// </summary>
    ImmutableArray<CachedGenericArgument> GenericArguments { get; }

    /// <summary>
    /// Gets the custom attributes data.
    /// Instantiating attributes is more expensive that exploiting the <see cref="CustomAttributeData"/>
    /// but it requires more work and guards in the attribute constructor (if any) must be replicated.
    /// </summary>
    public ImmutableArray<CustomAttributeData> CustomAttributes { get; }

    /// <summary>
    /// Get the methods declared by this type. Binding flags are <c>Public|NonPublic|Instance|Static|DeclaredOnly</c>.
    /// <para>
    /// Non public methods are collected mainly to be able to emit warnings since only public methods are actually
    /// considered by the engine.
    /// </para>
    /// </summary>
    ImmutableArray<CachedMethodInfo> DeclaredMethodInfos { get; }

    /// <summary>
    /// Gets the <see cref="TypeCache"/>.
    /// </summary>
    GlobalTypeCache TypeCache { get; }

    /// <summary>
    /// Returns the <see cref="CSharpName"/>.
    /// </summary>
    /// <returns>This type C# name.</returns>
    string ToString();
}
