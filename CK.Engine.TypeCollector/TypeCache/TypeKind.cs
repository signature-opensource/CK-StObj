using CK.Core;
using System;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Defines the "services" kind and life times and invalid combination of
/// <see cref="IAutoService"/> and <see cref="IRealObject"/>.
/// </summary>
[Flags]
public enum TypeKind
{
    /// <summary>
    /// Not a Poco, real object or service we handle. May be an external service
    /// for which no lifetime nor endpoint/backend adherence is known.
    /// </summary>
    None,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsScoped"/>
    IsScoped = Setup.ExternalServiceKind.IsScoped,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsSingleton"/>
    IsSingleton = Setup.ExternalServiceKind.IsSingleton,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsContainerConfiguredService"/>
    IsContainerConfiguredService = Setup.ExternalServiceKind.IsContainerConfiguredService,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsAmbientService"/>
    IsAmbientService = Setup.ExternalServiceKind.IsAmbientService,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsMultipleService"/>
    IsMultipleService = Setup.ExternalServiceKind.IsMultipleService,

    /// <summary>
    /// The type is "abstract": it transfers its kind to its specializations.
    /// </summary>
    IsDefiner = 1 << 6,

    /// <summary>
    /// The type is "super abstract": its specializations are Definers.
    /// </summary>
    IsSuperDefiner = 1 << 7,

    /// <summary>
    /// A IPoco marked interface.
    /// </summary>
    IsPoco = 1 << 8,

    /// <summary>
    /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
    /// </summary>
    IsAutoService = 1 << 9,

    /// <summary>
    /// A <see cref="IRealObject"/> is a true singleton.
    /// <list type="bullet">
    ///     <item><term>Implies</term><description><see cref="IsSingleton"/></description></item>
    ///     <item><term>Rejects</term><description><see cref="IsMultipleService"/> and <see cref="IsContainerConfiguredService"/></description></item>
    /// </list>
    /// </summary>
    IsRealObject = 1 << 10,

    /// <summary>
    /// Flags set when this type is excluded by [ExcludeCKType].
    /// This is also set when [StObjGen] attribute exists.
    /// </summary>
    IsIntrinsicExcluded = 1 << 11,

    /// <summary>
    /// Flags set when this type has at least one [RegisterCKType(..)].
    /// </summary>
    HasIntrinsicRegister = 1 << 12,

    /// <summary>
    /// The Type.FullName is null. This happens if the current instance represents a generic type parameter,
    /// an array type, pointer type, or byref type based on a type parameter, or a generic type
    /// that is not a generic type definition but contains unresolved type parameters.
    /// FullName is also null for (at least) classes nested into nested generic classes.
    /// </summary>
    IsNullFullName = 1 << 20,

    /// <summary>
    /// The type is implemented in a dynamic assembly.
    /// </summary>
    IsFromDynamicAssembly = 1 << 21,

    /// <summary>
    /// The type is not visible outside of its assembly.
    /// </summary>
    IsNotVisible = 1 << 22,

    /// <summary>
    /// The type is not a class, an enum, a value type or an interface.
    /// </summary>
    IsNotClassEnumValueTypeOrEnum = 1 << 23
}

/// <summary>
/// Extends <see cref="TypeKind"/>.
/// </summary>
public static class TypeKindExtension
{
    /// <summary>
    /// The <see cref="TypeKind.IsRealObject"/> and its implied flags IsSingleton.
    /// </summary>
    public const TypeKind RealObjectFlags = TypeKind.IsRealObject | TypeKind.IsSingleton;

    /// <summary>
    /// The <see cref="TypeKind.IsAmbientService"/> and its implied flags:
    /// IsContainerConfiguredService | IsScoped
    /// </summary>
    public const TypeKind AmbientServiceFlags = TypeKind.IsAmbientService | TypeKind.IsContainerConfiguredService | TypeKind.IsScoped;

    /// <summary>
    /// Gets this kind without <see cref="TypeKind.IsDefiner"/> and <see cref="TypeKind.IsSuperDefiner"/> flags.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <returns>Same as this without the two definer flags.</returns>
    public static TypeKind WithoutDefiners( this TypeKind @this ) => @this & ~(TypeKind.IsDefiner | TypeKind.IsSuperDefiner);

    /// <summary>
    /// Gets this kind without <see cref="TypeKind.HasIntrinsicRegister"/>.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <returns>Same as this without the two intrinsic register flags.</returns>
    public static TypeKind WithoutIntrinsicRegister( this TypeKind @this ) => @this & ~(TypeKind.HasIntrinsicRegister);

    internal static string? GetUnhandledMessage( this TypeKind type ) =>
            type switch
            {
                TypeKind.IsNullFullName => "has a null FullName",
                TypeKind.IsFromDynamicAssembly => "is defined by a dynamic assembly",
                TypeKind.IsNotVisible => "must be public (visible outside of its asssembly)",
                TypeKind.IsNotClassEnumValueTypeOrEnum => "must be an enum, a value type, a class or an interface",
                _ => null
            };
}
