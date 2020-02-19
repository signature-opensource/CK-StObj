using CK.Core;
using System;

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
        /// Auto service flag. 
        /// </summary>
        IsAutoService = 1,

        /// <summary>
        /// Singleton flag.
        /// External services are flagged with this only.
        /// </summary>
        IsSingleton = 2,

        /// <summary>
        /// Scoped flag.
        /// External services are flagged with this only.
        /// </summary>
        IsScoped = 4,

        /// <summary>
        /// A singleton auto service: <see cref="IsAutoService"/> | <see cref="IsSingleton"/>. 
        /// </summary>
        AutoSingleton = IsAutoService | IsSingleton,

        /// <summary>
        /// A real object is a singleton. 
        /// </summary>
        RealObject = IsSingleton | 8,

        /// <summary>
        /// A scoped auto service: <see cref="IsAutoService"/> | <see cref="IsScoped"/>.
        /// </summary>
        AutoScoped = IsAutoService | IsScoped,

        /// <summary>
        /// A IPoco marked interface.
        /// </summary>
        IsPoco = 16,

        ///// <summary>
        ///// 
        ///// </summary>
        //IsFront = 32
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
                case CKTypeKind.AutoScoped: return "ScopedAutoService";
                case CKTypeKind.IsScoped: return "ScopedService";
                case CKTypeKind.IsSingleton: return "SingletonService";
                case CKTypeKind.IsAutoService: return "AutoService";
                case CKTypeKind.IsPoco: return "Poco";
                default:
                    {
                        if( realObjectCanBeSingletonService && @this == (CKTypeKind.RealObject|CKTypeKind.AutoSingleton) )
                        {
                            return "RealObject and AutoSingleton";
                        }
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
            return conflict == null ? null : $"Invalid CK type combination: {conflict} cannot be defined simultaneously."; 
        }
    }
}
