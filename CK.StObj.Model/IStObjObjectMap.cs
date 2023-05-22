using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
// Ignore Spelling: Objs

namespace CK.Core
{
    /// <summary>
    /// Fundamental Types to <see cref="IStObj"/> mappings.
    /// This is exposed by <see cref="IStObjMap.StObjs"/> and is the result of the setup: its implementation
    /// is dynamically generated.
    /// </summary>
    public interface IStObjObjectMap
    {
        /// <summary>
        /// Gets the most specialized <see cref="IStObj"/> or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type.</param>
        /// <returns>Most specialized StObj or null if no mapping exists for this type.</returns>
        IStObj? ToLeaf( Type t );

        /// <summary>
        /// Gets the real object final implementation or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type (that must be a <see cref="IRealObject"/>).</param>
        /// <returns>Structured object instance or null if the type has not been mapped.</returns>
        object? Obtain( Type t );

        /// <summary>
        /// Gets all the real object final implementations that exist in this context.
        /// </summary>
        IReadOnlyList<IStObjFinalImplementation> FinalImplementations { get; }

        /// <summary>
        /// Gets all the <see cref="IStObj"/> and their final implementation that exist in this context.
        /// This contains only classes, not <see cref="IRealObject"/> interfaces. 
        /// </summary>
        IEnumerable<StObjMapping> StObjs { get; }

        /// <summary>
        /// Enables Real Objects to participate in the configuration of the <see cref="IServiceCollection"/>.
        /// If startup services are required, then the <see cref="StObjContextRoot.ServiceRegister.StartupServices"/> can be configured
        /// with these services that can configure the service configuration.
        /// <para>
        /// The first step calls all  <see cref="StObjContextRoot.RegisterStartupServicesMethodName"/> methods on all the <see cref="IStObj"/>, following
        /// the topological sort: during this step, startup services can be registered in the <see cref="ISimpleServiceContainer"/>) and/or used by
        /// dependent StObj (as a kind of "shared memory/state").
        /// <c>void RegisterStartupServices( IActivityMonitor, ISimpleServiceContainer );</c>
        /// </para>
        /// <para>
        /// Once all the RegisterStartupServices( IActivityMonitor, ISimpleServiceContainer ) methods have ran, then
        /// all the <see cref="StObjContextRoot.ConfigureServicesMethodName"/> StObj methods are called: 
        /// <c>void ConfigureServices( in StObjContextRoot.ServiceRegister, ... any services previously registered in the ISimpleServiceContainer ... );</c>
        /// </para>
        /// </summary>
        /// <param name="register">The service register.</param>
        void ConfigureServices( in StObjContextRoot.ServiceRegister register );
           
    }
}
