using System;
using System.Collections.Generic;

namespace CK.Core
{

    /// <summary>
    /// Exposes Service Types (interfaces and classes) to Service class mappings.
    /// This is exposed by <see cref="IStObjMap.Services"/> and is the result of the setup: its implementation
    /// is dynamically generated.
    /// </summary>
    public interface IStObjServiceMap
    {
        /// <summary>
        /// Gets the <see cref="IStObjFinalClass"/> that can be a <see cref="IStObjFinalImplementation"/> (if the service
        /// is implemented by a real object) or a <see cref="IStObjServiceClassDescriptor"/> or null if no mapping exists.
        /// </summary>
        /// <param name="t">IAutoService type.</param>
        /// <returns>The implementation or null if no mapping exists for this type.</returns>
        IStObjFinalClass? ToLeaf( Type t );

        /// <summary>
        /// Gets all the <see cref="IAutoService"/> types that are directly mapped to
        /// a Real Object.
        /// </summary>
        /// <para>
        /// Use <see cref="ObjectMappingList"/> to have the final real object classes list (without
        /// duplicates). 
        /// </para>
        IReadOnlyDictionary<Type, IStObjFinalImplementation> ObjectMappings { get; }

        /// <summary>
        /// Gets all the real objects that implement one or more <see cref="IAutoService"/> without duplicates.
        /// </summary>
        IReadOnlyList<IStObjFinalImplementation> ObjectMappingList { get; }

        /// <summary>
        /// Gets all the <see cref="IAutoService"/> types to the final service class type.
        /// <para>
        /// Use <see cref="MappingList"/> to have the final service classes list (without
        /// duplicates). 
        /// </para>
        /// </summary>
        IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> Mappings { get; }

        /// <summary>
        /// Gets all the Auto service implementations.
        /// </summary>
        IReadOnlyList<IStObjServiceClassDescriptor> MappingList { get; }
    }
}
