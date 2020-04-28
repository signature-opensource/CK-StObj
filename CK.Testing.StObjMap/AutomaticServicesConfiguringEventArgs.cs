using CK.Core;
using System;

namespace CK.Testing.StObjMap
{
    /// <summary>
    /// Event of <see cref="IStObjMapTestHelperCore.AutomaticServicesConfiguring"/>.
    /// </summary>
    public class AutomaticServicesConfiguringEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new <see cref="AutomaticServicesConfiguringEventArgs"/>.
        /// </summary>
        /// <param name="map">The current StObjMap.</param>
        /// <param name="startupServices">The already created startup container.</param>
        public AutomaticServicesConfiguringEventArgs( IStObjMap map, SimpleServiceContainer startupServices )
        {
            StObjMap = map ?? throw new ArgumentNullException( nameof( map ) );
            StartupServices = startupServices;
        }

        /// <summary>
        /// Gets the curren StObjMap. Never null.
        /// </summary>
        public IStObjMap StObjMap { get; }

        /// <summary>
        /// Gets or sets the startup services. Never null.
        /// </summary>
        public SimpleServiceContainer StartupServices { get; set; }

    }
}
