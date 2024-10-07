using CK.Core;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Defines the "services" kind and life times and invalid combination of
/// <see cref="IAutoService"/> and <see cref="IRealObject"/>.
/// </summary>
[Flags]
public enum CKTypeKind
{
    /// <summary>
    /// Not a Poco, real object or service we handle. May be an external service
    /// for which no lifetime nor endpoint/backend adherence is known.
    /// </summary>
    None,

    /// <summary>
    /// Flags set when this type is excluded (by [ExcludeCKType] or type filtering function).
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

    /// <inheritdoc cref="AutoServiceKind.IsAutoService"/>
    IsAutoService = AutoServiceKind.IsAutoService,

    /// <inheritdoc cref="AutoServiceKind.IsScoped"/>
    IsScoped = AutoServiceKind.IsScoped,

    /// <inheritdoc cref="AutoServiceKind.IsSingleton"/>
    IsSingleton = AutoServiceKind.IsSingleton,

    /// <inheritdoc cref="AutoServiceKind.IsRealObject"/>
    IsRealObject = AutoServiceKind.IsRealObject,

    /// <inheritdoc cref="AutoServiceKind.IsContainerConfiguredService"/>
    IsContainerConfiguredService = AutoServiceKind.IsContainerConfiguredService,

    /// <inheritdoc cref="AutoServiceKind.IsAmbientService"/>
    IsAmbientService = AutoServiceKind.IsAmbientService,

    /// <inheritdoc cref="AutoServiceKind.IsMultipleService"/>
    IsMultipleService = AutoServiceKind.IsMultipleService,

    /// <summary>
    /// Flags set whenever initial <see cref="CKTypeKindExtension.GetCombinationError(CKTypeKind, bool)"/>
    /// (that has been logged) returned an error or an error occurred in endpoint service handling.
    /// </summary>
    HasError = 1 << 16
}

/// <summary>
/// Extends <see cref="CKTypeKind"/>.
/// </summary>
public static class CKTypeKindExtension
{
    /// <summary>
    /// The <see cref="CKTypeKind.IsSingleton"/>.
    /// </summary>
    public const CKTypeKind SingletonFlags = CKTypeKind.IsSingleton;

    /// <summary>
    /// The <see cref="CKTypeKind.IsRealObject"/> and its implied flags:
    /// IsSingleton
    /// </summary>
    public const CKTypeKind RealObjectFlags = CKTypeKind.IsRealObject | SingletonFlags;

    /// <summary>
    /// The <see cref="CKTypeKind.IsAmbientService"/> and its implied flags:
    /// IsContainerConfiguredService | IsScoped
    /// </summary>
    public const CKTypeKind AmbientServiceFlags = CKTypeKind.IsAmbientService | CKTypeKind.IsContainerConfiguredService | CKTypeKind.IsScoped;

    /// <summary>
    /// Simple bit mask on <see cref="CKTypeKind.IsScoped"/> | <see cref="CKTypeKind.IsSingleton"/>.
    /// </summary>
    public const CKTypeKind LifetimeMask = CKTypeKind.IsScoped | CKTypeKind.IsSingleton;

    /// <summary>
    /// Covers the <see cref="AutoServiceKind"/> subset.
    /// </summary>
    public const CKTypeKind AutoServiceKindMask = (CKTypeKind)(0b11_1111_1111 << 6);

    /// <summary>
    /// Gets the <see cref="AutoServiceKind"/> (masks the unrelated bits).
    /// </summary>
    /// <param name="this">This type kind.</param>
    /// <returns>The Auto service kind.</returns>
    public static AutoServiceKind ToAutoServiceKind( this CKTypeKind @this )
    {
        return (AutoServiceKind)((int)(@this & AutoServiceKindMask));
    }

    /// <summary>
    /// Returns a string that correctly handles flags and results to <see cref="GetCombinationError(CKTypeKind,bool)"/>
    /// if this kind is invalid.
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>A readable string.</returns>
    public static string ToStringClear( this CKTypeKind @this, bool isClass )
    {
        string? error = GetCombinationError( @this, isClass );
        return error == null ? ToStringFlags( @this ) : error;
    }

    /// <summary>
    /// Gets whether this <see cref="CKTypeKind"/> is <see cref="CKTypeKind.None"/> or
    /// is invalid (see <see cref="GetCombinationError(CKTypeKind,bool)"/>).
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>whether this is invalid.</returns>
    public static bool IsNoneOrInvalid( this CKTypeKind @this, bool isClass )
    {
        return @this == CKTypeKind.None || GetCombinationError( @this, isClass ) != null;
    }

    /// <summary>
    /// Gets this kind without <see cref="CKTypeKind.IsDefiner"/> and <see cref="CKTypeKind.IsSuperDefiner"/> flags.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <returns>Same as this without the two definer flags.</returns>
    public static CKTypeKind WithoutDefiners( this CKTypeKind @this ) => @this & ~(CKTypeKind.IsDefiner | CKTypeKind.IsSuperDefiner);


    /// <summary>
    /// Gets the conflicting duplicate status message or null if this CK type kind is valid.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>An error message or null.</returns>
    public static string? GetCombinationError( this CKTypeKind @this, bool isClass )
    {
        Throw.CheckArgument( @this >= 0 && @this <= CKTypeKindDetector.MaskPublicInfo );
        var k = @this.WithoutDefiners();
        // Pure predicates: checks are made against them.
        bool isAuto = (k & CKTypeKind.IsAutoService) != 0;
        bool isScoped = (k & CKTypeKind.IsScoped) != 0;
        bool isSingleton = (k & CKTypeKind.IsSingleton) != 0;
        bool isRealObject = (k & CKTypeKind.IsRealObject) != 0;
        bool isPoco = (k & CKTypeKind.IsPoco) != 0;
        bool isEndPoint = (k & CKTypeKind.IsContainerConfiguredService) != 0;
        bool isMultiple = (k & CKTypeKind.IsMultipleService) != 0;
        bool isAmbient = (k & CKTypeKind.IsAmbientService) != 0;


        string? conflict = null;
        void AddConflict( string c )
        {
            if( conflict != null ) conflict += ", " + c;
            else conflict = c;
        }

        if( isPoco )
        {
            if( k != CKTypeKind.IsPoco ) AddConflict( "Poco cannot be combined with any other aspect" );
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
            if( k != AmbientServiceFlags && k != (AmbientServiceFlags | CKTypeKind.IsAutoService) )
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
            if( (k & CKTypeKind.IsMultipleService) != 0 ) AddConflict( "a class cannot be marked as a Multiple service: only interfaces can be IsMultiple." );
        }
        return conflict == null ? null : $"Invalid CK type combination '{k.ToStringFlags()}': {conflict} (type is a{(isClass ? " class" : "n interface")}).";
    }

    /// <summary>
    /// Basic string projection where each bit is expressed, regardless of any checks.
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <returns>A readable string.</returns>
    public static string ToStringFlags( this CKTypeKind @this )
    {
        if( @this == CKTypeKind.None ) return "None";
        var s = (@this & CKTypeKind.IsPoco) != 0
                    ? "IsPoco"
                    : ToAutoServiceKind( @this ).ToString().Replace( ", ", "|" );
        if( (@this & CKTypeKind.HasError) != 0 ) s += "|HasError";
        return s;
    }
}
