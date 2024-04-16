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
        /// Not a service we handle or external service for which
        /// no lifetime nor front binding is known.
        /// </summary>
        None,

        /// <summary>
        /// Service can be overridden by an endpoint. Whether it is a <see cref="IsProcessService"/> or not is independent.
        /// </summary>
        IsEndpointService = 2,

        /// <summary>
        /// Singleton flag.
        /// External (Auto) services are flagged with this (without the <see cref="IsAutoService"/> bit).
        /// </summary>
        IsSingleton = 8,

        /// <summary>
        /// Scoped flag.
        /// External (Auto) services are flagged with this (without the <see cref="IsAutoService"/> bit).
        /// </summary>
        IsScoped = 16,

        /// <summary>
        /// Multiple registration flag. Applies only to interfaces. See <see cref="IsMultipleAttribute"/>. 
        /// External (Auto) services are flagged with this (without the <see cref="IsAutoService"/> bit).
        /// </summary>
        /// <remarks>
        /// Such "Multiple" services must be registered with TryAddEnumerable instead of TryAdd.
        /// </remarks>
        IsMultipleService = 32,

        /// <summary>
        /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
        /// </summary>
        IsAutoService = 64,

        /// <summary>
        /// Ubiquitous info is a scoped endpoint service (and optionally a auto service) that must be available in all
        /// containers. The instance must be directly marshallable (should be immutable or at least thread safe and
        /// be independent of any other service). See <see cref="EndpointScopedServiceAttribute"/>.
        /// </summary>
        UbiquitousInfo = 128 | IsEndpointService | IsScoped,

        /// <summary>
        /// A IPoco marked interface.
        /// </summary>
        IsPoco = 256,

        /// <summary>
        /// A real object is a singleton. 
        /// </summary>
        RealObject = IsSingleton | 512,

        /// <summary>
        /// Simple bit mask on <see cref="IsScoped"/> | <see cref="IsSingleton"/>.
        /// </summary>
        LifetimeMask = IsScoped | IsSingleton,

        /// <summary>
        /// Flags set when this type is excluded (by [ExcludeCKType] or type filtering function).
        /// This is also set when [StObjGen] attribute exists.
        /// </summary>
        IsExcludedType = 1024,

        /// <summary>
        /// Flags set whenever initial <see cref="CKTypeKindExtension.GetCombinationError(CKTypeKind, bool)"/>
        /// (that has been logged) returned an error or an error occurred in endpoint service handling.
        /// </summary>
        HasError = 2048
    }

    /// <summary>
    /// Extends <see cref="CKTypeKind"/>.
    /// </summary>
    public static class CKTypeKindExtension
    {
        /// <summary>
        /// Gets the <see cref="AutoServiceKind"/> (masks the internal bits).
        /// </summary>
        /// <param name="this">This type kind.</param>
        /// <returns>The Auto service kind.</returns>
        public static AutoServiceKind ToAutoServiceKind( this CKTypeKind @this ) => (AutoServiceKind)((int)@this & 255);

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
            bool isRealObject = (@this & (CKTypeKind.RealObject & ~CKTypeKind.IsSingleton)) != 0;
            bool isPoco = (@this & CKTypeKind.IsPoco) != 0;
            bool isEndPoint = (@this & CKTypeKind.IsEndpointService) != 0;
            bool isMultiple = (@this & CKTypeKind.IsMultipleService) != 0;
            bool isUbiquitous = (@this & (CKTypeKind.UbiquitousInfo & ~CKTypeKind.IsEndpointService & ~CKTypeKind.IsScoped)) != 0;


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
                    if( isMultiple ) AddConflict( "IRealObject interface cannot be marked as a Multiple service" );
                    if( isAuto && !isClass ) AddConflict( "IRealObject interface cannot be a IAutoService" );
                }
            }
            else if( isScoped && isSingleton )
            {
                AddConflict( "an interface or an implementation cannot be both Scoped and Singleton" );
            }
            if( isUbiquitous )
            {
                if( @this != CKTypeKind.UbiquitousInfo && @this != (CKTypeKind.UbiquitousInfo|CKTypeKind.IsAutoService) )
                {
                    AddConflict( "an ubiquitous info can only be a endpoint scoped service (and optionally a IScopedAutoService)." );
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
                if( (@this & CKTypeKind.IsMultipleService) != 0 ) AddConflict( "a class cannot be marked as a Multiple service: only interfaces can be IsMultiple." );
            }
            return conflict == null ? null : $"Invalid CK type combination '{@this.ToStringFlags()}': {conflict} (type is a{(isClass ? " class" : "n interface")}).";
        }


        static readonly string[] flags = new[] { "IsAutoService",
                                                 "IsScopedService",
                                                 "IsSingleton",
                                                 "IsRealObject",
                                                 "IsPoco",
                                                 "IsUbiquitous",
                                                 "IsEndpointService",
                                                 "IsMultipleService",
                                                 "IsExcludedType",
                                                 "HasError"};

        /// <summary>
        /// Basic string projection where each bit is expressed, regardless of any checks.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <returns>A readable string.</returns>
        public static string ToStringFlags( this CKTypeKind @this )
        {
            if( @this == CKTypeKind.None ) return "None";
            bool isUbiquitous = (@this & (CKTypeKind.UbiquitousInfo & ~CKTypeKind.IsEndpointService & ~CKTypeKind.IsScoped)) != 0;
            var f = flags.Where( ( s, i ) => (i == 0 && (@this & CKTypeKind.IsAutoService) != 0)
                                             || (i == 1 && (@this & CKTypeKind.IsScoped) != 0)
                                             || (i == 2 && (@this & CKTypeKind.IsSingleton) != 0)
                                             || (i == 3 && (@this & (CKTypeKind.RealObject & ~CKTypeKind.IsSingleton)) != 0)
                                             || (i == 4 && (@this & CKTypeKind.IsPoco) != 0)
                                             || (i == 5 && isUbiquitous)
                                             || (i == 6 && (@this & CKTypeKind.IsEndpointService) != 0)
                                             || (i == 9 && (@this & CKTypeKind.IsMultipleService) != 0)
                                             || (i == 10 && (@this & CKTypeKind.IsExcludedType) != 0)
                                             || (i == 11 && (@this & CKTypeKind.HasError) != 0) );
            return String.Join( "|", f );
        }
    }
}
