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
        /// Configures a <see cref="IServiceCollection"/> by calling first all <see cref="StObjContextRoot.RegisterStartupServicesMethodName"/>
        /// an then all <see cref="StObjContextRoot.ConfigureServicesMethodName"/> on all the <see cref="Implementations"/> that expose
        /// such methods.
        /// </summary>
        /// <param name="register">The service register.</param>
        void ConfigureServices( in StObjContextRoot.ServiceRegister register );
           
    }
}
