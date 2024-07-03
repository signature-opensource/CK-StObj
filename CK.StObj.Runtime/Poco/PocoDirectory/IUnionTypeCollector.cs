using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates a list of <see cref="IExtPropertyInfo"/> that are ValueTuples.
    /// Each value defines a possible type of the union type.
    /// No check is done at the PocoDirectory level except the fact
    /// that all [UnionType] attribute must CanBeExtended or not, it is the PocoTypeSystem
    /// that checks the types and nullabilities.
    /// </summary>
    public interface IUnionTypeCollector
    {
        /// <summary>
        /// Gets whether this union type can be extended.
        /// When false, the types must not be related to each other.
        /// When true related types are "widened". 
        /// </summary>
        bool CanBeExtended { get; }

        /// <summary>
        /// Gets the different types that compose this union.
        /// </summary>
        IReadOnlyList<IExtMemberInfo> Types { get; }

        /// <summary>
        /// Returns a readable comma separated list of type names.
        /// </summary>
        /// <returns>A readable string.</returns>
        string ToString();
    }
}
