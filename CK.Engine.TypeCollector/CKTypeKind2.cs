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
public enum CKTypeKind2
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
    IsAutoService = AutoServiceKind2.IsAutoService,

    /// <inheritdoc cref="AutoServiceKind.IsScoped"/>
    IsScoped = AutoServiceKind2.IsScoped,

    /// <inheritdoc cref="AutoServiceKind.IsSingleton"/>
    IsSingleton = AutoServiceKind2.IsSingleton,

    /// <inheritdoc cref="AutoServiceKind.IsRealObject"/>
    IsRealObject = AutoServiceKind2.IsRealObject,

    /// <inheritdoc cref="AutoServiceKind.IsContainerConfiguredService"/>
    IsContainerConfiguredService = AutoServiceKind2.IsContainerConfiguredService,

    /// <inheritdoc cref="AutoServiceKind.IsAmbientService"/>
    IsAmbientService = AutoServiceKind2.IsAmbientService,

    /// <inheritdoc cref="AutoServiceKind.IsMultipleService"/>
    IsMultipleService = AutoServiceKind2.IsMultipleService,

    /// <summary>
    /// Flags set whenever initial <see cref="CKTypeKindExtension.GetCombinationError(CKTypeKind, bool)"/>
    /// (that has been logged) returned an error or an error occurred in endpoint service handling.
    /// </summary>
    HasError = 1 << 16
}

/// <summary>
/// Extends <see cref="CKTypeKind2"/>.
/// </summary>
public static class CKTypeKind2Extension
{
    /// <summary>
    /// The <see cref="CKTypeKind2.IsSingleton"/>.
    /// </summary>
    public const CKTypeKind2 SingletonFlags = CKTypeKind2.IsSingleton;

    /// <summary>
    /// The <see cref="CKTypeKind.IsRealObject"/> and its implied flags:
    /// IsSingleton
    /// </summary>
    public const CKTypeKind2 RealObjectFlags = CKTypeKind2.IsRealObject | SingletonFlags;

    /// <summary>
    /// The <see cref="CKTypeKind.IsAmbientService"/> and its implied flags:
    /// IsContainerConfiguredService | IsScoped
    /// </summary>
    public const CKTypeKind2 AmbientServiceFlags = CKTypeKind2.IsAmbientService | CKTypeKind2.IsContainerConfiguredService | CKTypeKind2.IsScoped;

    /// <summary>
    /// Simple bit mask on <see cref="CKTypeKind2.IsScoped"/> | <see cref="CKTypeKind2.IsSingleton"/>.
    /// </summary>
    public const CKTypeKind2 LifetimeMask = CKTypeKind2.IsScoped | CKTypeKind2.IsSingleton;

    /// <summary>
    /// Covers the <see cref="AutoServiceKind"/> subset.
    /// </summary>
    public const CKTypeKind2 AutoServiceKindMask = (CKTypeKind2)(0b11_1111_1111 << 6);

    /// <summary>
    /// Gets the <see cref="AutoServiceKind"/> (masks the unrelated bits).
    /// </summary>
    /// <param name="this">This type kind.</param>
    /// <returns>The Auto service kind.</returns>
    public static AutoServiceKind2 ToAutoServiceKind( this CKTypeKind2 @this )
    {
        return (AutoServiceKind2)((int)(@this & AutoServiceKindMask));
    }

    /// <summary>
    /// Returns a string that correctly handles flags and results to <see cref="GetCombinationError(CKTypeKind,bool)"/>
    /// if this kind is invalid.
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>A readable string.</returns>
    public static string ToStringClear( this CKTypeKind2 @this, bool isClass )
    {
        string? error = GetCombinationError( @this, isClass );
        return error == null ? ToStringFlags( @this ) : error;
    }

    /// <summary>
    /// Gets whether this <see cref="CKTypeKind2"/> is <see cref="CKTypeKind2.None"/> or
    /// is invalid (see <see cref="GetCombinationError(CKTypeKind2,bool)"/>).
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>whether this is invalid.</returns>
    public static bool IsNoneOrInvalid( this CKTypeKind2 @this, bool isClass )
    {
        return @this == CKTypeKind2.None || GetCombinationError( @this, isClass ) != null;
    }

    /// <summary>
    /// Gets this kind without <see cref="CKTypeKind.IsDefiner"/> and <see cref="CKTypeKind.IsSuperDefiner"/> flags.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <returns>Same as this without the two definer flags.</returns>
    public static CKTypeKind2 WithoutDefiners( this CKTypeKind2 @this ) => @this & ~(CKTypeKind2.IsDefiner | CKTypeKind2.IsSuperDefiner);


    /// <summary>
    /// Gets the conflicting duplicate status message or null if this CK type kind is valid.
    /// </summary>
    /// <param name="this">This kind.</param>
    /// <param name="isClass">True for Class type (not for interface).</param>
    /// <returns>An error message or null.</returns>
    public static string? GetCombinationError( this CKTypeKind2 @this, bool isClass )
    {
        var k = @this.WithoutDefiners();
        // Pure predicates: checks are made against them.
        bool isAuto = (k & CKTypeKind2.IsAutoService) != 0;
        bool isScoped = (k & CKTypeKind2.IsScoped) != 0;
        bool isSingleton = (k & CKTypeKind2.IsSingleton) != 0;
        bool isRealObject = (k & CKTypeKind2.IsRealObject) != 0;
        bool isPoco = (k & CKTypeKind2.IsPoco) != 0;
        bool isEndPoint = (k & CKTypeKind2.IsContainerConfiguredService) != 0;
        bool isMultiple = (k & CKTypeKind2.IsMultipleService) != 0;
        bool isAmbient = (k & CKTypeKind2.IsAmbientService) != 0;


        string? conflict = null;
        void AddConflict( string c )
        {
            if( conflict != null ) conflict += ", " + c;
            else conflict = c;
        }

        if( isPoco )
        {
            if( k != CKTypeKind2.IsPoco ) AddConflict( "Poco cannot be combined with any other aspect" );
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
            if( k != AmbientServiceFlags && k != (AmbientServiceFlags | CKTypeKind2.IsAutoService) )
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
            if( (k & CKTypeKind2.IsMultipleService) != 0 ) AddConflict( "a class cannot be marked as a Multiple service: only interfaces can be IsMultiple." );
        }
        return conflict == null ? null : $"Invalid CK type combination '{k.ToStringFlags()}': {conflict} (type is a{(isClass ? " class" : "n interface")}).";
    }

    /// <summary>
    /// Basic string projection where each bit is expressed, regardless of any checks.
    /// </summary>
    /// <param name="this">This CK type kind.</param>
    /// <returns>A readable string.</returns>
    public static string ToStringFlags( this CKTypeKind2 @this )
    {
        if( @this == CKTypeKind2.None ) return "None";
        var s = (@this & CKTypeKind2.IsPoco) != 0
                    ? "IsPoco"
                    : ToAutoServiceKind( @this ).ToString().Replace( ", ", "|" );
        if( (@this & CKTypeKind2.HasError) != 0 ) s += "|HasError";
        return s;
    }
}
