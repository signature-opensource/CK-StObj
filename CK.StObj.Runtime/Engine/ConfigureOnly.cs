using System;

namespace CK.Setup
{
    /// <summary>
    /// Wrapper around any service used to comunicate the fact that the registered service
    /// should only be used by the other following aspects only from
    /// their <see cref="IStObjEngineAspect.Configure"/> method.
    /// </summary>
    /// <typeparam name="T">Actual service type.</typeparam>
    public readonly struct ConfigureOnly<T>
    {
        /// <summary>
        /// The wrapped service instance.
        /// </summary>
        public readonly T Service;

        /// <summary>
        /// Initializes a new ConfigureOnly wrapper.
        /// </summary>
        /// <param name="service">Actual instance. Must not be null.</param>
        public ConfigureOnly( T service )
        {
            if( service == null ) throw new ArgumentNullException( nameof( service ) );
            Service = service;
        }
    }
}
