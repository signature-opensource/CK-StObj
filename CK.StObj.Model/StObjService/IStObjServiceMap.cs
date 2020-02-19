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
        IReadOnlyDictionary<Type, object> ObjectMappings { get; }

        /// <summary>
        /// Gets all the <see cref="IAutoService"/> types to the final service class type
        /// that can be directly resolved by any DI container.
        /// </summary>
        IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> SimpleMappings { get; }

        /// <summary>
        /// Gets all the <see cref="IAutoService"/> types to Service class mappings
        /// that cannot be directly resolved by a DI container and require either
        /// an adaptation based on the <see cref="IStObjServiceClassFactoryInfo"/> or
        /// to simply use the existing <see cref="IStObjServiceClassFactory.CreateInstance(IServiceProvider)"/>
        /// helper method.
        /// Note that a <see cref="IStObjServiceClassFactory"/> is a <see cref="IStObjServiceClassDescriptor"/> (that
        /// is the descriptor used by <see cref="SimpleMappings"/>).
        /// </summary>
        IReadOnlyDictionary<Type, IStObjServiceClassFactory> ManualMappings { get; }

    }
}
