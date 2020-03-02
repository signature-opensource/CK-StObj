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
        /// Front process service.
        /// This flag has to be set for <see cref="IsFrontEndPointService"/> and/or <see cref="IsMarshallableService"/> to be set.
        /// </summary>
        IsFrontProcessService = 1,

        /// <summary>
        /// Service is bound to the End Point. The service is necessarily bound to front
        /// process (<see cref="IsFrontProcessService"/> is also set).
        /// </summary>
        IsFrontEndPointService = 2,

        /// <summary>
        /// Marshallable service: this is set only if <see cref="IsFrontProcessService"/> is set.
        /// </summary>
        IsMarshallableService = 4,

        /// <summary>
        /// Singleton flag.
        /// External (Auto) services are flagged with this only (or with this and <see cref="IsMarshallableService"/> or <see cref="IsFrontEndPointService"/>).
        /// </summary>
        IsSingleton = 8,

        /// <summary>
        /// Scoped flag.
        /// External (Auto) services are flagged with this only (or with this and <see cref="IsMarshallableService"/> or <see cref="IsFrontEndPointService"/>).
        /// </summary>
        IsScoped = 16,

        /// <summary>
        /// Multiple registration flag (services must be registered with TryAddEnumerable instead of TryAdd).
        /// See <see cref="IsMultipleAttribute"/>. 
        /// External (Auto) services are flagged with this (without the <see cref="IsAutoService"/> bit).
        /// </summary>
        IsMultipleService = 32,

        /// <summary>
        /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
        /// </summary>
        IsAutoService = 64,

        /// <summary>
        /// A IPoco marked interface.
        /// </summary>
        IsPoco = 128,

        /// <summary>
        /// A real object is a singleton. 
        /// </summary>
        RealObject = IsSingleton | 256,

        /// <summary>
        /// Simple bit mask on <see cref="IsFrontEndPointService"/> | <see cref="IsFrontProcessService"/> | <see cref="IsMarshallableService"/>.
        /// </summary>
        FrontTypeMask = IsFrontEndPointService | IsFrontProcessService | IsMarshallableService,

        /// <summary>
        /// Simple bit mask on <see cref="IsScoped"/> | <see cref="IsSingleton"/>.
        /// </summary>
        LifetimeMask = IsScoped | IsSingleton
    }

    /// <summary>
    /// Extends <see cref="CKTypeKind"/>.
    /// </summary>
    public static class CKTypeKindExtension
    {
        /// <summary>
        /// Gets the <see cref="AutoServiceKind"/>.
        /// </summary>
        /// <param name="this">This type kind.</param>
        /// <returns>The Auto service kind.</returns>
        public static AutoServiceKind ToAutoServiceKind( this CKTypeKind @this )
        {
            if( (@this&(CKTypeKind.IsFrontEndPointService|CKTypeKind.IsMarshallableService)) != 0 && (@this & CKTypeKind.IsFrontProcessService) == 0 )
            {
                throw new ArgumentException( $"Invalid CKTypeKind: IsFrontEndPointService|IsMarshallableService must imply IsFrontProcessService." );
            }
            return (AutoServiceKind)((int)@this & 63);
        }

        /// <summary>
        /// Returns a string that correctly handles flags and results to <see cref="GetCKTypeKindCombinationError(CKTypeKind,bool)"/>
        /// if this kind is invalid.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <param name="realObjectCanBeSingletonService">True for Class type (not for interface).</param>
        /// <returns>A readable string.</returns>
        public static string ToStringClear( this CKTypeKind @this, bool realObjectCanBeSingletonService = false )
        {
            string error = GetCKTypeKindCombinationError( @this );
            return error == null ? ToStringFlags( @this ) : error;
        }

        /// <summary>
        /// Gets whether this <see cref="CKTypeKind"/> is <see cref="CKTypeKind.None"/> or
        /// is invalid (see <see cref="GetCKTypeKindCombinationError(CKTypeKind,bool)"/>).
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <returns>whether this is invalid.</returns>
        public static bool IsNoneOrInvalid( this CKTypeKind @this )
        {
            return @this == CKTypeKind.None || GetCKTypeKindCombinationError( @this ) != null;
        }

        /// <summary>
        /// Gets the conflicting duplicate status message or null if this CK type kind is valid.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <param name="realObjectCanBeSingletonService">True for Class type (not for interface).</param>
        /// <returns>An error message or null.</returns>
        public static string GetCKTypeKindCombinationError( this CKTypeKind @this, bool realObjectCanBeSingletonService = false )
        {
            if( @this < 0 || @this > CKTypeKindDetector.MaskPublicInfo )
            {
                throw new ArgumentOutOfRangeException( nameof(CKTypeKind), @this, "Undefined enum values appear." );
            }
            // Pure predicates: checks are made against them.
            bool isAuto = (@this & CKTypeKind.IsAutoService) != 0;
            bool isScoped = (@this & CKTypeKind.IsScoped) != 0;
            bool isSingleton = (@this & CKTypeKind.IsSingleton) != 0;
            bool isRealObject = (@this & (CKTypeKind.RealObject & ~CKTypeKind.IsSingleton)) != 0;
            bool isPoco = (@this & CKTypeKind.IsPoco) != 0;
            bool isFrontEndPoint = (@this & CKTypeKind.IsFrontEndPointService) != 0;
            bool isFrontProcess = (@this & CKTypeKind.IsFrontProcessService) != 0;
            bool isMarshallable = (@this & CKTypeKind.IsMarshallableService) != 0;
            bool isMultiple = (@this & CKTypeKind.IsMultipleService) != 0;

            if( (isFrontEndPoint || isMarshallable) && !isFrontProcess )
            {
                throw new ArgumentException( "CKTypeKind value error: missing IsFrontProcessService flag for IsFrontEndPointService|IsMarshallableService: " + @this.ToStringFlags() );
            }
            if( isRealObject && !isSingleton )
            {
                throw new Exception( "CKTypeKind value error: missing IsSingleton flag to RealObject mask: " + @this.ToStringFlags() );
            }

            string conflict = null;
            if( isPoco )
            {
                if( @this != CKTypeKind.IsPoco ) conflict = "Poco cannot be combined with any other aspect";
            }
            else if( isRealObject )
            {
                Debug.Assert( isSingleton, "Checked above." );
                if( @this != CKTypeKind.RealObject )
                {
                    // If IsMultiple, then this is an interface, not a class: a IRealObject interface cannot be IsMultiple.
                    if( isScoped ) conflict = "RealObject cannot have a Scoped lifetime";
                    else if( isMultiple ) conflict = "IRealObject interface cannot be marked as a Multiple service";
                    else if( isAuto && !realObjectCanBeSingletonService ) conflict = "IRealObject interface cannot be an IAutoService";
                    // Always handle Front service.
                    if( isFrontEndPoint | isFrontProcess | isMarshallable )
                    {
                        if( conflict != null ) conflict += ", ";
                        conflict += "RealObject cannot be a front service";
                    }
                }
            }
            else if( isScoped && isSingleton )
            {
                conflict = "An interface or an implementation cannot be both Scoped and Singleton";
            }
            return conflict == null ? null : $"Invalid CK type combination: {conflict} for '{@this.ToStringFlags()}'.";
        }


        /// <summary>
        /// Basic string projection where each bit is expressed, regardless of any checks.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <returns>A readable string.</returns>
        public static string ToStringFlags( this CKTypeKind @this )
        {
            string[] flags = new[] { "IsAutoService", "IsScopedService", "IsSingleton", "IsRealObject", "IsPoco", "IsFrontEndPointService", "IsFrontProcessService", "IsMarshallableService", "IsMultipleService" };
            if( @this == CKTypeKind.None ) return "None";
            var f = flags.Where( ( s, i ) => (i == 0 && (@this & CKTypeKind.IsAutoService) != 0)
                                             || (i == 1 && (@this & CKTypeKind.IsScoped) != 0)
                                             || (i == 2 && (@this & CKTypeKind.IsSingleton) != 0)
                                             || (i == 3 && (@this & (CKTypeKind.RealObject & ~CKTypeKind.IsSingleton)) != 0)
                                             || (i == 4 && (@this & CKTypeKind.IsPoco) != 0)
                                             || (i == 5 && (@this & CKTypeKind.IsFrontEndPointService) != 0)
                                             || (i == 6 && (@this & CKTypeKind.IsFrontProcessService) != 0)
                                             || (i == 7 && (@this & CKTypeKind.IsMarshallableService) != 0)
                                             || (i == 8 && (@this & CKTypeKind.IsMultipleService) != 0));
            return String.Join( "|", f );
        }
    }
}
