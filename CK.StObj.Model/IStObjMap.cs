using Microsoft.Extensions.DependencyInjection;
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
        /// Gets the names of this StObj map.
        /// Never empty, defaults to a single empty string.
        /// </summary>
        IReadOnlyList<string> Names { get; }

        /// <summary>
        /// Gets the set of <see cref="VFeature"/> that is contained in this map.
        /// </summary>
        IReadOnlyCollection<VFeature> Features { get; }

        /// <summary>
        /// Last step for service configuration. The <paramref name="serviceRegister"/> must contain the "Global" container
        /// configuration. 
        /// </summary>
        /// <param name="serviceRegister">The global container configuration.</param>
        void ConfigureEndpointServices( in StObjContextRoot.ServiceRegister serviceRegister );
    }
}
