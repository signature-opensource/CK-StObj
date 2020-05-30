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
        /// Gets all the <see cref="IAutoService"/> types that are directly mapped to
        /// an already available Real Object.
        /// </summary>
        IReadOnlyDictionary<Type, IStObjFinalImplementation> ObjectMappings { get; }

        /// <summary>
        /// Gets all the <see cref="IAutoService"/> types to the final service class type
        /// that can be directly resolved by any DI container.
        /// <para>
        /// Use <see cref="SimpleMappingList"/> to have the final service classes list (without
        /// duplicates). 
        /// </para>
        /// </summary>
        IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> SimpleMappings { get; }

        /// <summary>
        /// Gets all the types (exposed by <see cref="IStObjServiceClassDescriptor.ClassType"/>)
        /// that can easily be resolved by any DI container.
        /// </summary>
        IReadOnlyList<IStObjServiceClassDescriptor> SimpleMappingList { get; }

        /// <summary>
        /// Gets all the <see cref="IAutoService"/> types to Service class mappings
        /// that cannot be directly resolved by a DI container and require either
        /// an adaptation based on the <see cref="IStObjServiceClassFactoryInfo"/> or
        /// to simply use the provided <see cref="IStObjServiceClassFactory.CreateInstance(IServiceProvider)"/>
        /// helper method.
        /// <para>
        /// Note that a <see cref="IStObjServiceClassFactory"/> is a <see cref="IStObjServiceClassDescriptor"/> (that
        /// is the descriptor used by <see cref="SimpleMappings"/>).
        /// </para>
        /// <para>
        /// Use <see cref="ManualMappingList"/> to have the final service factories list (without duplicates).
        /// </para>
        /// </summary>
        IReadOnlyDictionary<Type, IStObjServiceClassFactory> ManualMappings { get; }

        /// <summary>
        /// Gets all the not so simple registered types. See <see cref="ManualMappings"/>.
        /// duplicates). 
        /// </summary>
        IReadOnlyList<IStObjServiceClassFactory> ManualMappingList { get; }

    }
}
