using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Immutable encapsulation of nullability information.
    /// </summary>
    public interface IExtNullabilityInfo
    {
        /// <summary>
        /// The <see cref="System.Type" /> of the member or generic parameter
        /// to which this ExtNullabilityInfo belongs
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets whether this type is nullable.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// If the member type is an array, gives the <see cref="IExtNullabilityInfo" /> of the elements of the array, null otherwise
        /// </summary>
        IExtNullabilityInfo? ElementType { get; }

        /// <summary>
        /// If the member type is a generic type, gives the array of <see cref="IExtNullabilityInfo" /> for each type parameter
        /// </summary>
        IReadOnlyList<IExtNullabilityInfo> GenericTypeArguments { get; }

        /// <summary>
        /// Gets whether this nullability info reflects the read state of the member.
        /// </summary>
        bool ReflectsReadState { get; }

        /// <summary>
        /// Gets whether this nullability info reflects the write state of the member.
        /// </summary>
        bool ReflectsWriteState { get; }

        /// <summary>
        /// Gets whether this nullability info is the same for the read and write state.
        /// </summary>
        bool IsHomogeneous { get; }

        /// <summary>
        /// Limited mutator. <paramref name="typeDefinition"/> must not be <see cref="Type.IsValueType"/> and must
        /// be a <see cref="Type.IsGenericTypeDefinition"/> with the same number of arguments as this <see cref="GenericTypeArguments"/>.
        /// <para>
        /// The new type is built thanks to <see cref="Type.MakeGenericType(Type[])"/> bound to this GenericTypeArguments.
        /// </para>
        /// </summary>
        /// <param name="typeDefinition">Generic type definition to apply to substitute this <see cref="Type"/>.</param>
        /// <param name="nullable">
        /// True to return the non nullable, false for the nullable. By default this <see cref="IsNullable"/> is used.
        /// </param>
        /// <returns>A new <see cref="IExtNullabilityInfo"/>.</returns>
        IExtNullabilityInfo SetReferenceTypeDefinition( Type typeDefinition, bool? nullable = null );

        /// <summary>
        /// Returns either this or the non nullable corresponding nullability
        /// info if this is nullable.
        /// </summary>
        /// <returns>this or the non nullable nullability info for this type.</returns>
        IExtNullabilityInfo ToNonNullable();

        /// <summary>
        /// Returns either this or the nullable corresponding nullability
        /// info if this is non nullable.
        /// </summary>
        /// <returns>this or the non nullable nullability info for this type.</returns>
        IExtNullabilityInfo ToNullable();

        /// <summary>
        /// Gets quick description of this type with its nullabilities.
        /// This is intended for debug, it is not a C# or Type compliant format. 
        /// </summary>
        /// <returns>A readable string.</returns>
        string ToString();
    }
}
