using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Associates the final, most specialized, implementation and its multiple and unique mappings.
    /// </summary>
    public interface IStObjFinalImplementation
    {
        /// <summary>
        /// Gets the final implementation instance.
        /// </summary>
        object Implementation { get; }

        /// <summary>
        /// Gets the interfaces that are marked with <see cref="IsMultipleAttribute"/> and must be mapped to this <see cref="Implementation"/>
        /// regadless of their other mappings.
        /// </summary>
        IReadOnlyCollection<Type> MultipleMappings { get; }

        /// <summary>
        /// Gets the types that are mapped to this <see cref="Implementation"/> and only to this implementation.
        /// </summary>
        IReadOnlyCollection<Type> UniqueMappings { get; }

    }

}
