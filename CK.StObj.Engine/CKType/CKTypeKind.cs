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
        /// Process service flag indicates a service that has a data/configuration adherence to the
        /// current process: it requires some sort of marshalling/configuration to be able to do its job
        /// remotely (out of this process).
        /// (A typical example is the IOptions&lt;&gt; implementations for instance.) 
        /// </summary>
        IsProcessService = 1,

        /// <summary>
        /// Service can be overridden by an endpoint. Whether it is a <see cref="IsProcessService"/> or not is independent.
        /// </summary>
        IsEndpointService = 2,

        /// <summary>
        /// Marshallable service.
        /// This is independent of <see cref="IsProcessService"/> (but a process service must eventually be marshallable).
        /// </summary>
        IsMarshallable = 4,

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
        /// A IPoco marked interface.
        /// </summary>
        IsPoco = 128,

        /// <summary>
        /// A [PocoClass] class.
        /// </summary>
        IsPocoClass = 256,

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
        public static AutoServiceKind ToAutoServiceKind( this CKTypeKind @this ) => (AutoServiceKind)((int)@this & 127);

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
            bool isPocoClass = (@this & CKTypeKind.IsPocoClass) != 0;
            bool isEndPoint = (@this & CKTypeKind.IsEndpointService) != 0;
            bool isProcess = (@this & CKTypeKind.IsProcessService) != 0;
            bool isMarshallable = (@this & CKTypeKind.IsMarshallable) != 0;
            bool isMultiple = (@this & CKTypeKind.IsMultipleService) != 0;

            if( isRealObject && !isSingleton )
            {
                Throw.Exception( "CKTypeKind value error: missing IsSingleton flag to RealObject mask: " + @this.ToStringFlags() );
            }

            string? conflict = null;
            if( isPoco )
            {
                if( @this != CKTypeKind.IsPoco ) conflict = "Poco cannot be combined with any other aspect";
                if( isClass )
                {
                    if( conflict != null ) conflict += ", ";
                    conflict += "A class cannot be a IPoco";
                }
            }
            else if( isPocoClass )
            {
                if( @this != CKTypeKind.IsPocoClass ) conflict = "[PocoClass] class cannot be combined with any other aspect";
            }
            else if( isRealObject )
            {
                Debug.Assert( isSingleton, "Checked above." );
                if( @this != CKTypeKind.RealObject )
                {
                    // If IsMultiple, then this is an interface, not a class: a IRealObject interface cannot be IsMultiple.
                    if( isScoped ) conflict = "RealObject cannot have a Scoped lifetime";
                    else if( isMultiple ) conflict = "IRealObject interface cannot be marked as a Multiple service";
                    else if( isAuto && !isClass ) conflict = "IRealObject interface cannot be a IAutoService";
                    if( isMarshallable )
                    {
                        if( conflict != null ) conflict += ", ";
                        conflict += "RealObject cannot be marked as Marshallable";
                    }
                    if( isEndPoint | isProcess )
                    {
                        if( conflict != null ) conflict += ", ";
                        conflict += "RealObject cannot be a Endpoint or Process service";
                    }
                }
            }
            else if( isScoped && isSingleton )
            {
                conflict = "An interface or an implementation cannot be both Scoped and Singleton";
            }
            else if( isEndPoint )
            {
                if( !isScoped && !isSingleton )
                {
                    conflict = "A Endpoint service must be known to be Scoped or Singleton";
                }
            }
            if( isClass )
            {
                if( (@this & CKTypeKind.IsMultipleService) != 0 ) conflict = "A class cannot be marked as a Multiple service: only interfaces can be IsMultiple.";
            }
            return conflict == null ? null : $"Invalid CK type combination: {conflict} for {(isClass ? "class" : "interface")} '{@this.ToStringFlags()}'.";
        }


        /// <summary>
        /// Basic string projection where each bit is expressed, regardless of any checks.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <returns>A readable string.</returns>
        public static string ToStringFlags( this CKTypeKind @this )
        {
            string[] flags = new[] { "IsAutoService",
                                     "IsScopedService",
                                     "IsSingleton",
                                     "IsRealObject",
                                     "IsPoco",
                                     "IsPocoClass",
                                     "IsEndpointService",
                                     "IsProcessService",
                                     "IsMarshallable",
                                     "IsMultipleService",
                                     "IsExcludedType",
                                     "HasError"};
            if( @this == CKTypeKind.None ) return "None";
            var f = flags.Where( ( s, i ) => (i == 0 && (@this & CKTypeKind.IsAutoService) != 0)
                                             || (i == 1 && (@this & CKTypeKind.IsScoped) != 0)
                                             || (i == 2 && (@this & CKTypeKind.IsSingleton) != 0)
                                             || (i == 3 && (@this & (CKTypeKind.RealObject & ~CKTypeKind.IsSingleton)) != 0)
                                             || (i == 4 && (@this & CKTypeKind.IsPoco) != 0)
                                             || (i == 5 && (@this & CKTypeKind.IsPocoClass) != 0)
                                             || (i == 6 && (@this & CKTypeKind.IsEndpointService) != 0)
                                             || (i == 7 && (@this & CKTypeKind.IsProcessService) != 0)
                                             || (i == 8 && (@this & CKTypeKind.IsMarshallable) != 0)
                                             || (i == 9 && (@this & CKTypeKind.IsMultipleService) != 0)
                                             || (i == 10 && (@this & CKTypeKind.IsExcludedType) != 0)
                                             || (i == 11 && (@this & CKTypeKind.HasError) != 0) );
            return String.Join( "|", f );
        }
    }
}
