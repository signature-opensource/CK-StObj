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

    /// <summary>
    /// Flags set when this type is excluded by [ExcludeCKType].
    /// This is also set when [StObjGen] attribute exists.
    /// </summary>
    IsExcludedType = 1 << 0,

    /// <summary>
    /// The type is "abstract": it transfers its kind to its specializations.
    /// </summary>
    IsDefiner = 1 << 1,

    /// <summary>
    /// The type is "super abstract": its specializations are Definers.
    /// </summary>
    IsSuperDefiner = 1 << 2,

    /// <summary>
    /// A IPoco marked interface.
    /// </summary>
    IsPoco = 1 << 3,

    /// <summary>
    /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
    /// </summary>
    IsAutoService = 1 << 6,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsScoped"/>
    IsScoped = Setup.ExternalServiceKind.IsScoped,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsSingleton"/>
    IsSingleton = Setup.ExternalServiceKind.IsSingleton,

    /// <summary>
    /// A <see cref="IRealObject"/> is a true singleton.
    /// <list type="bullet">
    ///     <item><term>Implies</term><description><see cref="IsSingleton"/></description></item>
    ///     <item><term>Rejects</term><description><see cref="IsMultipleService"/> and <see cref="IsContainerConfiguredService"/></description></item>
    /// </list>
    /// </summary>
    IsRealObject = 1 << 10,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsContainerConfiguredService"/>
    IsContainerConfiguredService = Setup.ExternalServiceKind.IsContainerConfiguredService,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsAmbientService"/>
    IsAmbientService = Setup.ExternalServiceKind.IsAmbientService,

    /// <inheritdoc cref="Setup.ExternalServiceKind.IsMultipleService"/>
    IsMultipleService = Setup.ExternalServiceKind.IsMultipleService,

    /// <summary>
    /// The Type.FullName is null. This happens if the current instance represents a generic type parameter,
    /// an array type, pointer type, or byref type based on a type parameter, or a generic type
    /// that is not a generic type definition but contains unresolved type parameters.
    /// FullName is also null for (at least) classes nested into nested generic classes.
    /// </summary>
    IsNullFullName = 1 << 16,

    /// <summary>
    /// The type is implemented in a dynamic assembly.
    /// </summary>
    IsFromDynamicAssembly = 1 << 17,

    /// <summary>
    /// The type is not visible outside of its assembly.
    /// </summary>
    IsNotVisible = 1 << 18,

    /// <summary>
    /// The type is not a class, an enum, a value type or an interface.
    /// </summary>
    IsNotClassEnumValueTypeOrEnum = 1 << 19
}

/// <summary>
/// Extends <see cref="TypeKind"/>.
/// </summary>
public static class TypeKindExtension
{
    /// <summary>
    /// The <see cref="TypeKind.IsSingleton"/>.
    /// </summary>
    public const TypeKind SingletonFlags = TypeKind.IsSingleton;

    /// <summary>
    /// The <see cref="TypeKind.IsRealObject"/> and its implied flags:
    /// IsSingleton
    /// </summary>
    public const TypeKind RealObjectFlags = TypeKind.IsRealObject | SingletonFlags;

    /// <summary>
    /// The <see cref="TypeKind.IsAmbientService"/> and its implied flags:
    /// IsContainerConfiguredService | IsScoped
    /// </summary>
    public const TypeKind AmbientServiceFlags = TypeKind.IsAmbientService | TypeKind.IsContainerConfiguredService | TypeKind.IsScoped;

    /// <summary>
    /// Simple bit mask on <see cref="TypeKind.IsScoped"/> | <see cref="TypeKind.IsSingleton"/>.
    /// </summary>
    public const TypeKind LifetimeMask = TypeKind.IsScoped | TypeKind.IsSingleton;

    /// <summary>
    /// Gets whether this <see cref="TypeKind"/> is <see cref="TypeKind.None"/> or
    /// is invalid (see <see cref="GetCombinationError(TypeKind,bool)"/>).
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>whether this is invalid.</returns>
    public static bool IsNoneOrInvalid( this TypeKind @this, bool isClass )
    {
        return @this == TypeKind.None || GetCombinationError( @this, isClass ) != null;
    }

    /// <summary>
    /// Gets this kind without <see cref="TypeKind.IsDefiner"/> and <see cref="TypeKind.IsSuperDefiner"/> flags.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <returns>Same as this without the two definer flags.</returns>
    public static TypeKind WithoutDefiners( this TypeKind @this ) => @this & ~(TypeKind.IsDefiner | TypeKind.IsSuperDefiner);


    /// <summary>
    /// Gets the conflicting duplicate status message or null if this CK type kind is valid.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>An error message or null.</returns>
    public static string? GetCombinationError( this TypeKind @this, bool isClass )
    {
        var k = @this.WithoutDefiners();
        // Pure predicates: checks are made against them.
        bool isAuto = (k & TypeKind.IsAutoService) != 0;
        bool isScoped = (k & TypeKind.IsScoped) != 0;
        bool isSingleton = (k & TypeKind.IsSingleton) != 0;
        bool isRealObject = (k & TypeKind.IsRealObject) != 0;
        bool isPoco = (k & TypeKind.IsPoco) != 0;
        bool isEndPoint = (k & TypeKind.IsContainerConfiguredService) != 0;
        bool isMultiple = (k & TypeKind.IsMultipleService) != 0;
        bool isAmbient = (k & TypeKind.IsAmbientService) != 0;


        string? conflict = null;
        void AddConflict( string c )
        {
            if( conflict != null ) conflict += ", " + c;
            else conflict = c;
        }

        if( isPoco )
        {
            if( k != TypeKind.IsPoco ) AddConflict( "Poco cannot be combined with any other aspect" );
            if( isClass ) AddConflict( "a class cannot be a IPoco" );
        }
        else if( isRealObject )
        {
            if( k != RealObjectFlags )
            {
                if( !isSingleton ) AddConflict( "RealObject must be a Singleton" );
                // If IsMultiple, then this is an interface, not a class: a IRealObject interface cannot be IsMultiple.
                if( isScoped ) AddConflict( "RealObject cannot have a Scoped lifetime" );
                if( isEndPoint ) AddConflict( "RealObject cannot be an optional Endpoint service" );
                if( isMultiple ) AddConflict( "IRealObject interface cannot be marked as a Multiple service" );
                // Allow a class to be RealObject that implements a service (usually the default service) but
                // forbids defining an interface that is both.
                if( isAuto && !isClass ) AddConflict( "IRealObject interface cannot be a IAutoService" );
            }
        }
        else if( isScoped && isSingleton )
        {
            AddConflict( "an interface or an implementation cannot be both Scoped and Singleton" );
        }
        if( isAmbient )
        {
            if( k != AmbientServiceFlags && k != (AmbientServiceFlags | TypeKind.IsAutoService) )
            {
                AddConflict( "an ambient service info can only be a required endpoint and background scoped service (and optionally a IScopedAutoService)." );
            }
        }
        else if( isEndPoint )
        {
            if( !isScoped && !isSingleton )
            {
                AddConflict( "a Endpoint service must be known to be Scoped or Singleton" );
            }
        }
        if( isClass )
        {
            if( (k & TypeKind.IsMultipleService) != 0 ) AddConflict( "a class cannot be marked as a Multiple service: only interfaces can be IsMultiple." );
        }
        return conflict == null ? null : $"Invalid CK type combination '{k}': {conflict} (type is a{(isClass ? " class" : "n interface")}).";
    }

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
