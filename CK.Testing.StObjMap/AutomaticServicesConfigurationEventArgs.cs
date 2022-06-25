using CK.Core;
using System;

namespace CK.Testing.StObjMap
{
    /// <summary>
    /// Event of <see cref="IStObjMapTestHelperCore.AutomaticServicesConfiguring"/> and <see cref="IStObjMapTestHelperCore.AutomaticServicesConfigured"/>.
    /// </summary>
    public class AutomaticServicesConfigurationEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new <see cref="AutomaticServicesConfigurationEventArgs"/>.
        /// </summary>
        /// <param name="map">The current StObjMap. Must not be null.</param>
        /// <param name="serviceRegister">The service register.</param>
        public AutomaticServicesConfigurationEventArgs( IStObjMap map, StObjContextRoot.ServiceRegister serviceRegister )
        {
            Throw.CheckNotNullArgument( map );
            StObjMap = map;
            ServiceRegister = serviceRegister;
        }

        /// <summary>
        /// Gets the current StObjMap. Never null.
        /// </summary>
        public IStObjMap StObjMap { get; }

        /// <summary>
        /// Gets the service register.
        /// </summary>
        public StObjContextRoot.ServiceRegister ServiceRegister { get; }

    }
}
