using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Main interface that offers access to type mapping, Real Object instances and
    /// simple feature model.
    /// </summary>
    public interface IStObjMap
    {
        /// <summary>
        /// Gets the StObjs map.
        /// This is for advanced use: <see cref="StObjServiceCollectionExtensions.AddStObjMap(IServiceCollection, IActivityMonitor, IStObjMap, SimpleServiceContainer)"/>
        /// handles everything that needs to be done before using all the services and objects.
        /// </summary>
        IStObjObjectMap StObjs { get; }

        /// <summary>
        /// Gets a <see cref="SHA1Value"/> that uniquely identifies this map.
        /// </summary>
        SHA1Value GeneratedSignature { get; }

        /// <summary>
        /// Gets the Services map.
        /// This is for advanced use: <see cref="StObjServiceCollectionExtensions.AddStObjMap(IServiceCollection, IActivityMonitor, IStObjMap, SimpleServiceContainer)"/>
        /// handles everything that needs to be done before using all the services and objects.
        /// </summary>
        IStObjServiceMap Services { get; }

        /// <summary>
        /// Gets the <see cref="IStObjFinalClass"/> that can be a <see cref="IStObjFinalImplementation"/> or
        /// a <see cref="IStObjServiceClassDescriptor"/> or null if no mapping exists.
        /// </summary>
        /// <param name="t">IAutoService or IRealObject type.</param>
        /// <returns>The implementation or null if no mapping exists for this type.</returns>
        IStObjFinalClass? ToLeaf( Type t );

        /// <summary>
        /// Gets the names of this StObj map.
        /// Never empty, defaults to a single empty string.
        /// </summary>
        IReadOnlyList<string> Names { get; }

        /// <summary>
        /// Gets the set of <see cref="VFeature"/> that is contained in this map.
        /// </summary>
        IReadOnlyCollection<VFeature> Features { get; }

        /// <summary>
        /// Gets the [IsMultiple] interfaces IEnumerable mappings.
        /// The key is the [IsMultiple] interface type.
        /// </summary>
        IReadOnlyDictionary<Type, IStObjMultipleInterface> MultipleMappings { get; }

        /// <summary>
        /// Configures the global <see cref="StObjContextRoot.ServiceRegister.Services"/> services collection.
        /// <list type="number">
        ///     <item>
        ///     Real Objects can participate in the configuration of the global <see cref="IServiceCollection"/> thanks to the
        ///     <see cref="StObjContextRoot.ServiceRegister.StartupServices"/> that is a "shared memory/state" provided to 
        ///     all the StObj <c>RegisterStartupServices</c> and <c>ConfigureServices</c> methods.
        ///     It can initially be configured with any service that can help configuring the service configuration.
        ///     <list type="bullet">
        ///         <item>
        ///         The first step calls all <c>RegisterStartupServices</c> methods on all the <see cref="IStObj"/>, following
        ///         the topological dependency order: during this step, startup services can be registered in the <see cref="ISimpleServiceContainer"/>)
        ///         and/or used by dependent StObj (as a kind of "shared memory/state").
        ///         <para>
        ///         <c>void RegisterStartupServices( IActivityMonitor, ISimpleServiceContainer );</c>
        ///         </para>
        ///         </item>
        ///         <item>
        ///         Once all the <c>RegisterStartupServices</c> methods have ran, then all the <c>ConfigureServices</c> StObj methods are called:
        ///         <para>
        ///         <c>void ConfigureServices( in StObjContextRoot.ServiceRegister, ... any services previously registered in the ISimpleServiceContainer ... );</c>
        ///         </para>
        ///         </item>
        ///     </list>
        ///     </item>
        ///     <item>
        ///     If any <see cref="EndpointDefinition"/> exist, a common <see cref="IServiceProvider"/> blueprint for endpoint containers is created.
        ///     </item>
        ///     <item>
        ///     The global and the common endpoint service collections are then configured with the real objects and auto services.
        ///     </item>
        ///     <item>
        ///     Existing endpoints are initialized and build their own container, configured by their <see cref="EndpointDefinition{TScopeData}.ConfigureEndpointServices(IServiceCollection, Func{IServiceProvider, TScopeData}, IServiceProviderIsService)"/>.
        ///     </item>
        /// </list>
        /// </summary>
        /// <param name="serviceRegister">The global container configuration.</param>
        /// <returns>True on success, false on error. Errors have been logged to <see cref="StObjContextRoot.ServiceRegister.Monitor"/>.</returns>
        bool ConfigureServices( in StObjContextRoot.ServiceRegister serviceRegister );
    }
}
