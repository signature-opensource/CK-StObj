using CK.Core;
using System;

namespace CK.Testing.StObjMap
{
    /// <summary>
    /// Event of <see cref="IStObjMapTestHelperCore.AutomaticServicesConfigured"/>.
    /// </summary>
    public class AutomaticServicesConfiguredEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new <see cref="AutomaticServicesConfiguredEventArgs"/>.
        /// </summary>
        /// <param name="map">The current StObjMap. Must not be null.</param>
        /// <param name="serviceRegister">The service register.</param>
        public AutomaticServicesConfiguredEventArgs( IStObjMap map, StObjContextRoot.ServiceRegister serviceRegister )
        {
            if( map == null ) throw new ArgumentNullException( nameof( map ) );
            StObjMap = map;
            ServiceRegister = serviceRegister;
        }

        /// <summary>
        /// Gets the curren StObjMap. Never null.
        /// </summary>
        public IStObjMap StObjMap { get; }

        /// <summary>
        /// Gets the service registerer.
        /// </summary>
        public StObjContextRoot.ServiceRegister ServiceRegister { get; }

    }
}
