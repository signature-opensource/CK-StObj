using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Captures nullability information about a Type.
    /// </summary>
    public enum NullabilityTypeKind : byte
    {
        /// <summary>
        /// Unknown type kind.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Expected nullability flag.
        /// </summary>
        IsNullable = 1,

        /// <summary>
        /// Actual nullability flag: challenging null is required.
        /// </summary>
        IsTechnicallyNullable = 2,

        /// <summary>
        /// Value type that may be a <see cref="Nullable{Type}"/>.
        /// </summary>
        IsValueType = 4,

        /// <summary>
        /// Reference types are <see cref="Type.IsClass"/> or <see cref="Type.IsInterface"/> but NOT <see cref="Type.IsArray"/>.
        /// </summary>
        IsReferenceType = 8,

        /// <summary>
        /// The type is a generic type. For <see cref="Nullable{T}"/>, this applies to the inner T type. 
        /// </summary>
        IsGenericType = 16,

        /// <summary>
        /// The type is an array type. Its item type is equivalent to a single generic parameter.
        /// </summary>
        IsArrayType = 32,

        /// <summary>
        /// Optional flag thar describes a Nullable Reference Type marked with NullableAttribute(2): the type
        /// is necessarily <see cref="IsReferenceType"/> and <see cref="IsNullable"/> and if <see cref="NullablityTypeKindExtension.HasTypeArguments()"/>
        /// is true, then all its subordinated types that are reference types are also nullables.
        /// <para>
        /// This flag can also be set simultaneously with the <see cref="NRTFullNonNullable"/>: when both are set it means that
        /// the type is marked with a complex NRT NullableAttribute.
        /// Use <see cref="NullablityTypeKindExtension.IsNRTFullNullable()"/> to test if this type is really NRT nullable.
        /// </para>
        /// </summary>
        NRTFullNullable = 64,

        /// <summary>
        /// Optional flag thar describes a Nullable Reference Type marked with NullableAttribute(1): the type
        /// is necessarily <see cref="IsReferenceType"/> and only <see cref="IsTechnicallyNullable"/> and
        /// if <see cref="NullablityTypeKindExtension.HasTypeArguments()"/> is true, then all its subordinated types that are reference types
        /// are also non nullables reference types.
        /// <para>
        /// This flag can also be set simultaneously with the <see cref="NRTFullNullable"/>: when both are set it means that
        /// the type is marked with a complex NRT NullableAttribute.
        /// Use <see cref="NullablityTypeKindExtension.IsNRTFullNonNullable()"/> to test if this type is really NRT nullable.
        /// </para>
        /// </summary>
        NRTFullNonNullable = 128,

        /// <summary>
        /// A nullable value type is <see cref="IsValueType"/>|<see cref="IsNullable"/>|<see cref="IsTechnicallyNullable"/>.
        /// It is wrapped in a <see cref="Nullable{T}"/>.
        /// </summary>
        NullableValueType = IsValueType | IsNullable | IsTechnicallyNullable,

        /// <summary>
        /// A generic value type wrapped in a <see cref="Nullable{T}"/>.
        /// </summary>
        NullableGenericValueType = IsValueType | IsNullable | IsTechnicallyNullable | IsGenericType,

        /// <summary>
        /// A non nullable value type is only <see cref="IsValueType"/>.
        /// </summary>
        NonNullableValueType = IsValueType,

        /// <summary>
        /// A non nullable generic value type is <see cref="IsValueType"/>|<see cref="IsGenericType"/>.
        /// </summary>
        NonNullableGenericValueType = IsValueType | IsGenericType,

        /// <summary>
        /// A nullable reference type is <see cref="IsReferenceType"/>|<see cref="IsNullable"/>|<see cref="IsTechnicallyNullable"/>.
        /// </summary>
        NullableReferenceType = IsReferenceType | IsNullable | IsTechnicallyNullable,

        /// <summary>
        /// A nullable generic reference type is <see cref="IsReferenceType"/>|<see cref="IsNullable"/>|<see cref="IsTechnicallyNullable"/>|<see cref="IsGenericType"/>.
        /// </summary>
        NullableGenericReferenceType = IsReferenceType | IsNullable | IsTechnicallyNullable | IsGenericType,

        /// <summary>
        /// A nullable array is a reference type: <see cref="IsReferenceType"/>|<see cref="IsNullable"/>|<see cref="IsTechnicallyNullable"/>|<see cref="IsArrayType"/>.
        /// </summary>
        NullableArrayType = IsReferenceType | IsNullable | IsTechnicallyNullable | IsArrayType,

        /// <summary>
        /// A non nullable reference type is <see cref="IsReferenceType"/>|<see cref="IsTechnicallyNullable"/>.
        /// </summary>
        NonNullableReferenceType = IsReferenceType | IsTechnicallyNullable,

        /// <summary>
        /// A non nullable generic reference type is <see cref="IsReferenceType"/>|<see cref="IsTechnicallyNullable"/>>|<see cref="IsGenericType"/>.
        /// </summary>
        NonNullableGenericReferenceType = IsReferenceType | IsTechnicallyNullable | IsGenericType,

        /// <summary>
        /// A non nullable array type is <see cref="IsReferenceType"/>|<see cref="IsTechnicallyNullable"/>>|<see cref="IsArrayType"/>.
        /// </summary>
        NonNullableArrayType = IsReferenceType | IsTechnicallyNullable | IsArrayType,

    }

    /// <summary>
    /// Extends <see cref="NullabilityTypeKind"/>.
    /// </summary>
    public static class NullablityTypeKindExtension
    {
        /// <summary>
        /// Gets whether this is <see cref="NullabilityTypeKind.IsArrayType"/> or <see cref="NullabilityTypeKind.IsReferenceType"/>.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for arrays or reference types.</returns>
        public static bool IsArrayOrReferenceType( this NullabilityTypeKind @this ) => (@this & (NullabilityTypeKind.IsArrayType | NullabilityTypeKind.IsReferenceType)) != 0;

        /// <summary>
        /// Gets whether this is <see cref="NullabilityTypeKind.IsArrayType"/> or <see cref="NullabilityTypeKind.IsGenericType"/>.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for arrays or generic types.</returns>
        public static bool HasTypeArguments( this NullabilityTypeKind @this ) => (@this & (NullabilityTypeKind.IsArrayType | NullabilityTypeKind.IsGenericType)) != 0;

        /// <summary>
        /// Gets whether this is a reference type that is used in a nullable aware context.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for Nullable Reference Type aware types.</returns>
        public static bool IsNRTAware( this NullabilityTypeKind @this ) => (@this & (NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) != 0;

        /// <summary>
        /// Gets whether this is a NRT that is fully nullable.
        /// See <see cref="NullabilityTypeKind.NRTFullNullable"/>.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for Nullable Reference Type fully null.</returns>
        public static bool IsNRTFullNullable( this NullabilityTypeKind @this ) => (@this & (NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) == NullabilityTypeKind.NRTFullNullable;

        /// <summary>
        /// Gets whether this is a NRT that is fully non nullable.
        /// See <see cref="NullabilityTypeKind.NRTFullNonNullable"/>.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for Nullable Reference Type fully null.</returns>
        public static bool IsNRTFullNonNullable( this NullabilityTypeKind @this ) => (@this & (NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) == NullabilityTypeKind.NRTFullNonNullable;

        /// <summary>
        /// Gets whether this is a nullable type.
        /// See <see cref="NullabilityTypeKind.IsNullable"/>.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for nullable type.</returns>
        public static bool IsNullable( this NullabilityTypeKind @this ) => (@this & NullabilityTypeKind.IsNullable) != 0;

        /// <summary>
        /// Gets whether this is a technically nullable type.
        /// See <see cref="NullabilityTypeKind.IsTechnicallyNullable"/>.
        /// </summary>
        /// <param name="this">This <see cref="NullabilityTypeKind"/>.</param>
        /// <returns>True for type that can be null (even if they shouldn't).</returns>
        public static bool IsTechnicallyNullable( this NullabilityTypeKind @this ) => (@this & NullabilityTypeKind.IsTechnicallyNullable) != 0;
    }
}
