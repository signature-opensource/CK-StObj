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
        /// Gets the Services map.
        /// This is for advanced use: <see cref="StObjServiceCollectionExtensions.AddStObjMap(IServiceCollection, IActivityMonitor, IStObjMap, SimpleServiceContainer)"/>
        /// handles everything that needs to be done before using all the services and objects.
        /// </summary>
        IStObjServiceMap Services { get; }

        /// <summary>
        /// Gets the name of this StObj map.
        /// Never null, defaults to the empty string.
        /// </summary>
        string MapName { get; }

        /// <summary>
        /// Gets the set of <see cref="VFeature"/> that is contained in this map.
        /// </summary>
        IReadOnlyCollection<VFeature> Features { get; }

    }
}
