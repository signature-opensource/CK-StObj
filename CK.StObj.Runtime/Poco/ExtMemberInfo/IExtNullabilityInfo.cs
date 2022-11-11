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
        /// If the member type is an array, gives the <see cref="NullabilityInfo" /> of the elements of the array, null otherwise
        /// </summary>
        IExtNullabilityInfo? ElementType { get; }

        /// <summary>
        /// If the member type is a generic type, gives the array of <see cref="NullabilityInfo" /> for each type parameter
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
    }
}
