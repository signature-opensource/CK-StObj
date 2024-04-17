using CK.Core;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
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

        IsDefiner = 1 << 1,

        IsSuperDefiner = 1 << 2,

        /// <summary>
        /// A IPoco marked interface.
        /// </summary>
        IsPoco = 1 << 3,

        AutoServiceKindMask = 0b11_1111_1111 << 6,

        /// <inheritdoc cref="AutoServiceKind.IsAutoService"/>
        IsAutoService = AutoServiceKind.IsAutoService,

        /// <inheritdoc cref="AutoServiceKind.IsScoped"/>
        IsScoped = AutoServiceKind.IsScoped,

        /// <inheritdoc cref="AutoServiceKind.IsSingleton"/>
        IsSingleton = 1 << 8,

        /// <inheritdoc cref="AutoServiceKind.IsPerContextSingleton"/>
        IsPerContextSingleton = 1 << 9,

        /// <inheritdoc cref="AutoServiceKind.IsRealObject"/>
        IsRealObject = 1 << 10,

        /// <inheritdoc cref="AutoServiceKind.IsOptionalEndpointService"/>
        IsOptionalEndpointService = 1 << 11,

        /// <inheritdoc cref="AutoServiceKind.IsRequiredEndpointService"/>
        IsRequiredEndpointService = 1 << 12,

        /// <inheritdoc cref="AutoServiceKind.IsBackgroundService"/>
        IsBackgroundService = 1 << 13,

        /// <inheritdoc cref="AutoServiceKind.IsAmbientService"/>
        IsAmbientService = 1 << 14,

        /// <inheritdoc cref="AutoServiceKind.IsMultipleService"/>
        IsMultipleService = 1 << 15,

        /// <summary>
        /// Ubiquitous info is a scoped endpoint service (and optionally a auto service) that must be available in all
        /// containers. The instance must be directly marshallable (should be immutable or at least thread safe and
        /// be independent of any other service). See <see cref="EndpointScopedServiceAttribute"/>.
        /// </summary>
        AmbientService = IsAmbientService | IsBackgroundService | IsRequiredEndpointService | IsScoped,

        /// <summary>
        /// A real object is a singleton. 
        /// </summary>
        RealObject = IsRealObject | IsSingleton,

        /// <summary>
        /// Simple bit mask on <see cref="IsScoped"/> | <see cref="IsSingleton"/>.
        /// </summary>
        LifetimeMask = IsScoped | IsSingleton,

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
        /// Gets the <see cref="AutoServiceKind"/> (masks the unrelated bits).
        /// </summary>
        /// <param name="this">This type kind.</param>
        /// <returns>The Auto service kind.</returns>
        public static AutoServiceKind ToAutoServiceKind( this CKTypeKind @this )
        {
            return (AutoServiceKind)((int)(@this & CKTypeKind.AutoServiceKindMask));
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

        public static bool IsAmbientService( this CKTypeKind @this )
        {
            return true;
        }

        /// <summary>
        /// Gets the conflicting duplicate status message or null if this CK type kind is valid.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <param name="isClass">True for Class type (not for interface).</param>
        /// <returns>An error message or null.</returns>
        public static string? GetCombinationError( this CKTypeKind @this, bool isClass )
        {
            Throw.CheckArgument( @this >= 0 && @this <= CKTypeKindDetector.MaskPublicInfo );
            // Pure predicates: checks are made against them.
            bool isAuto = (@this & CKTypeKind.IsAutoService) != 0;
            bool isScoped = (@this & CKTypeKind.IsScoped) != 0;
            bool isSingleton = (@this & CKTypeKind.IsSingleton) != 0;
            bool isCtxSingleton = (@this & CKTypeKind.IsPerContextSingleton) != 0;
            bool isRealObject = (@this & CKTypeKind.IsRealObject) != 0;
            bool isPoco = (@this & CKTypeKind.IsPoco) != 0;
            bool isOptEndPoint = (@this & CKTypeKind.IsOptionalEndpointService) != 0;
            bool isReqEndPoint = (@this & CKTypeKind.IsRequiredEndpointService) != 0;
            bool isMultiple = (@this & CKTypeKind.IsMultipleService) != 0;
            bool isAmbient = (@this & CKTypeKind.IsAmbientService) != 0;


            string? conflict = null;
            void AddConflict( string c )
            {
                if( conflict != null ) conflict += ", " + c;
                else conflict = c;
            }

            if( isPoco )
            {
                if( @this != CKTypeKind.IsPoco ) AddConflict( "Poco cannot be combined with any other aspect" );
                if( isClass ) AddConflict( "a class cannot be a IPoco" );
            }
            else if( isRealObject )
            {
                if( @this != CKTypeKind.RealObject )
                {
                    if( !isSingleton ) AddConflict( "RealObject must be a Singleton" );
                    // If IsMultiple, then this is an interface, not a class: a IRealObject interface cannot be IsMultiple.
                    if( isScoped ) AddConflict( "RealObject cannot have a Scoped lifetime" );
                    if( isOptEndPoint ) AddConflict( "RealObject cannot be an optional Endpoint service" );
                    if( isMultiple ) AddConflict( "IRealObject interface cannot be marked as a Multiple service" );
                    // Allow a class to be RealObject that implements a service (usually the default service) but
                    // forbids defining an interface that is both.
                    if( isAuto && !isClass ) AddConflict( "IRealObject interface cannot be a IAutoService" );
                }
            }
            else if( isScoped && (isSingleton || isCtxSingleton) )
            {
                AddConflict( "an interface or an implementation cannot be both Scoped and Singleton" );
            }
            if( isAmbient )
            {
                if( @this != CKTypeKind.AmbientService && @this != (CKTypeKind.AmbientService|CKTypeKind.IsAutoService) )
                {
                    AddConflict( "an ambient service info can only be a required endpoint and background scoped service (and optionally a IScopedAutoService)." );
                }
            }
            else if( isOptEndPoint || isReqEndPoint )
            {
                if( !isScoped && !(isSingleton | isCtxSingleton) )
                {
                    AddConflict( "a Endpoint service must be known to be Scoped or Singleton" );
                }
            }
            if( isSingleton && isCtxSingleton )
            {
                AddConflict( "a Singleton cannot be both PerContext and proceswide" );
            }
            if( isOptEndPoint && isReqEndPoint )
            {
                AddConflict( "a Endpoint service cannot be both optional and required" );
            }
            if( isClass )
            {
                if( (@this & CKTypeKind.IsMultipleService) != 0 ) AddConflict( "a class cannot be marked as a Multiple service: only interfaces can be IsMultiple." );
            }
            return conflict == null ? null : $"Invalid CK type combination '{@this.ToStringFlags()}': {conflict} (type is a{(isClass ? " class" : "n interface")}).";
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
}
