using CK.Core;
using System;
using System.Diagnostics;

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
        /// no lifetime is known.
        /// </summary>
        None,

        /// <summary>
        /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
        /// </summary>
        IsAutoService = 1,

        /// <summary>
        /// Singleton flag.
        /// External services are flagged with this only (or with this and <see cref="IsMarshallableService"/> or <see cref="IsFrontOnlyService"/>).
        /// </summary>
        IsSingleton = 2,

        /// <summary>
        /// Scoped flag.
        /// External services are flagged with this only (or with this and <see cref="IsMarshallableService"/> or <see cref="IsFrontOnlyService"/>).
        /// </summary>
        IsScoped = 4,

        /// <summary>
        /// A singleton auto service: <see cref="IsAutoService"/> | <see cref="IsSingleton"/>. 
        /// </summary>
        AutoSingleton = IsAutoService | IsSingleton,

        /// <summary>
        /// A singleton front only auto service: <see cref="IsAutoService"/> | <see cref="IsSingleton"/> | <see cref="IsFrontOnlyService"/>. 
        /// </summary>
        AutoFrontOnlySingleton = AutoSingleton | IsFrontOnlyService,

        /// <summary>
        /// A singleton marshallable auto service: <see cref="IsAutoService"/> | <see cref="IsSingleton"/> | <see cref="IsMarshallableService"/>. 
        /// </summary>
        AutoMarshallableSingleton = AutoSingleton | IsMarshallableService,

        /// <summary>
        /// A singleton, front only, externally declared service: <see cref="IsSingleton"/> | <see cref="IsFrontOnlyService"/>. 
        /// </summary>
        FrontOnlySingleton = IsSingleton | IsFrontOnlyService,

        /// <summary>
        /// A singleton, marshallable, externally declared service: <see cref="IsSingleton"/> | <see cref="IsMarshallableService"/>. 
        /// </summary>
        MarshallableSingleton = IsSingleton | IsMarshallableService,

        /// <summary>
        /// A real object is a singleton. 
        /// </summary>
        RealObject = IsSingleton | 8,

        /// <summary>
        /// A scoped auto service: <see cref="IsAutoService"/> | <see cref="IsScoped"/>.
        /// </summary>
        AutoScoped = IsAutoService | IsScoped,

        /// <summary>
        /// A singleton front only auto service: <see cref="IsAutoService"/> | <see cref="IsScoped"/> | <see cref="IsFrontOnlyService"/>. 
        /// </summary>
        AutoFrontOnlyScoped = AutoScoped | IsFrontOnlyService,

        /// <summary>
        /// A singleton marshallable auto service: <see cref="IsAutoService"/> | <see cref="IsScoped"/> | <see cref="IsMarshallableService"/>. 
        /// </summary>
        AutoMarshallableScoped = AutoScoped | IsMarshallableService,

        /// <summary>
        /// A singleton, front only, externally declared service: <see cref="IsScoped"/> | <see cref="IsFrontOnlyService"/>. 
        /// </summary>
        FrontOnlyScoped = IsScoped | IsFrontOnlyService,

        /// <summary>
        /// A singleton, marshallable, externally declared service: <see cref="IsScoped"/> | <see cref="IsMarshallableService"/>. 
        /// </summary>
        MarshallableScoped = IsScoped | IsMarshallableService,

        /// <summary>
        /// A IPoco marked interface.
        /// </summary>
        IsPoco = 16,

        /// <summary>
        /// Front only service. This excludes <see cref="IsMarshallableService"/>.
        /// </summary>
        IsFrontOnlyService = 32,

        /// <summary>
        /// Marshallable service. This excludes <see cref="IsFrontOnlyService"/>.
        /// </summary>
        IsMarshallableService = 64,

        /// <summary>
        /// Simple bit mask on <see cref="IsFrontOnlyService"/> | <see cref="IsMarshallableService"/>.
        /// </summary>
        FrontTypeMask = IsFrontOnlyService | IsMarshallableService,

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
        /// Returns a string that correctly handles flags and results to <see cref="GetCKTypeKindCombinationError(CKTypeKind,bool)"/>
        /// if this kind is invalid.
        /// </summary>
        /// <param name="this">This CK type kind.</param>
        /// <param name="realObjectCanBeSingletonService">True for Class type (not for interface).</param>
        /// <returns>A readable string.</returns>
        public static string ToStringClear( this CKTypeKind @this, bool realObjectCanBeSingletonService = false )
        {
            switch( @this )
            {
                case CKTypeKind.None: return "None";
                case CKTypeKind.RealObject: return "RealObject";
                case CKTypeKind.AutoSingleton: return "SingletonAutoService";
                case CKTypeKind.AutoFrontOnlySingleton: return "SingletonFrontOnlyAutoService";
                case CKTypeKind.AutoMarshallableSingleton: return "SingletonMarshallableAutoService";
                case CKTypeKind.AutoScoped: return "ScopedAutoService";
                case CKTypeKind.AutoFrontOnlyScoped: return "ScopedFrontOnlyAutoService";
                case CKTypeKind.AutoMarshallableScoped: return "ScopedMarshallableAutoService";
                case CKTypeKind.IsAutoService: return "AutoService";
                case CKTypeKind.IsScoped: return "ExternallyDefinedScopedService";
                case CKTypeKind.MarshallableScoped: return "ExternallyDefinedMarshallableScopedService";
                case CKTypeKind.FrontOnlyScoped: return "ExternallyDefinedFrontOnlyScopedService";
                case CKTypeKind.IsSingleton: return "ExternallyDefinedSingletonService";
                case CKTypeKind.MarshallableSingleton: return "ExternallyDefinedMarshallableSingletonService";
                case CKTypeKind.FrontOnlySingleton: return "ExternallyDefinedFrontOnlySingletonService";
                case CKTypeKind.IsPoco: return "Poco";
                default:
                    {
                        if( realObjectCanBeSingletonService && @this == (CKTypeKind.RealObject|CKTypeKind.AutoSingleton) )
                        {
                            return "RealObject and AutoSingleton";
                        }
                        Debug.Assert( GetCKTypeKindCombinationError( @this ) != null );
                        return GetCKTypeKindCombinationError( @this );
                    }
            }
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
            bool isAuto = (@this & CKTypeKind.IsAutoService) != 0;
            bool isAutoScoped = (@this & CKTypeKind.AutoScoped) == CKTypeKind.AutoScoped;
            bool isAutoSingleton = (@this & CKTypeKind.AutoSingleton) == CKTypeKind.AutoSingleton;
            bool isRealObject = (@this & CKTypeKind.RealObject) == CKTypeKind.RealObject;
            bool isPoco = (@this & CKTypeKind.IsPoco) == CKTypeKind.IsPoco;

            bool isMarshallable = (@this & CKTypeKind.IsMarshallableService) != 0;
            bool isFrontOnly = (@this & CKTypeKind.IsFrontOnlyService) != 0;

            string conflict = null;
            if( isAutoScoped && isAutoSingleton )
            {
                if( isRealObject ) conflict = "AutoScoped, AutoSingleton and RealObject";
                else conflict = "AutoScoped and AutoSingleton";
            }
            else if( isAutoScoped && isRealObject )
            {
                conflict = "AutoScoped and RealObject";
            }
            else if( isAutoSingleton && isRealObject && !realObjectCanBeSingletonService )
            {
                conflict = "AutoSingleton and RealObject";
            }
            else if( isPoco && isRealObject )
            {
                conflict = "Poco and RealObject";
            }
            else if( isPoco && isAuto )
            {
                conflict = "Poco and AutoService";
            }
            // This should not happen unless the enum value is externally manipulated.
            if( isMarshallable && isFrontOnly )
            {
                if( conflict != null ) conflict += " and ";
                conflict += "both Marshallable and Front service";
            }
            return conflict == null ? null : $"Invalid CK type combination: {conflict} cannot be defined simultaneously."; 
        }
    }
}
