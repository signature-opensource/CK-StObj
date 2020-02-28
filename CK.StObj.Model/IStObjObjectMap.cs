using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Extends <see cref="IStObjTypeMap"/> to expose <see cref="IStObj"/> and Type to Object resolution.
    /// This is exposed by <see cref="IStObjMap.StObjs"/> and is the result of the setup: its implementation
    /// is dynamically generated.
    /// </summary>
    public interface IStObjObjectMap : IStObjTypeMap
    {
        /// <summary>
        /// Gets the most specialized <see cref="IStObj"/> or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type.</param>
        /// <returns>Most specialized StObj or null if no mapping exists for this type.</returns>
        IStObj ToLeaf( Type t );

        /// <summary>
        /// Gets the structured object final implementation or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type (that must be a <see cref="IRealObject"/>).</param>
        /// <returns>Structured object instance or null if the type has not been mapped.</returns>
        object Obtain( Type t );

        /// <summary>
        /// Gets all the structured object final implementations that exist in this context.
        /// </summary>
        IEnumerable<object> Implementations { get; }

        /// <summary>
        /// Gets all the <see cref="IStObj"/> and their final implementation that exist in this context.
        /// This contains only classes, not <see cref="IRealObject"/> interfaces. 
        /// Use <see cref="Mappings"/> to dump all the types to implementation mappings.
        /// </summary>
        IEnumerable<StObjImplementation> StObjs { get; }

        /// <summary>
        /// Gets all the <see cref="IRealObject"/> types to implementation objects that this
        /// context contains.
        /// The key types are interfaces (IRealObject) as well as classes.
        /// </summary>
        IEnumerable<KeyValuePair<Type, object>> Mappings { get; }

        /// <summary>
        /// Configures a <see cref="IServiceCollection"/> with the registered services.
        /// <para>
        /// The first services that are added are the real objets (as singletons) from <see cref="IStObjObjectMap.Mappings"/>.
        /// </para>
        /// <para>
        /// Once the real objects are registered, their <see cref="StObjContextRoot.RegisterStartupServicesMethodName"/> methods are called (so that startup services
        /// can be registered in the <see cref="ISimpleServiceContainer"/>):
        /// <c>void RegisterStartupServices( IActivityMonitor, ISimpleServiceContainer );</c>
        /// </para>
        /// <para>
        /// Once all the RegisterStartupServices( IActivityMonitor, ISimpleServiceContainer ) methods have ran, then
        /// all the <see cref="StObjContextRoot.ConfigureServicesMethodName"/> real objects' methods are called: 
        /// <c>void ConfigureServices( in StObjContextRoot.ServiceRegister, ... any services previously registered in the ISimpleServiceContainer ... );</c>
        /// </para>
        /// </summary>
        /// <param name="register">The service register.</param>
        void ConfigureServices( in StObjContextRoot.ServiceRegister register );
           
    }
}
