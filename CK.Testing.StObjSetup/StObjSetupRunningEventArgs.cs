using CK.Setup;
using CKSetup;
using System;

namespace CK.Testing.StObjSetup
{
    /// <summary>
    /// Defines the event argument when starting a StObjSetup.
    /// </summary>
    public class StObjSetupRunningEventArgs : EventArgs
    {
        StObjEngineConfiguration _configuration;

        /// <summary>
        /// Initializes a new event with an existing configuration.
        /// </summary>
        /// <param name="conf">The configuration.</param>
        /// <param name="forceSetup">Initial <see cref="ForceSetup"/> configuration.</param>
        public StObjSetupRunningEventArgs( StObjEngineConfiguration conf, ForceSetupLevel forceSetup )
        {
            _configuration = conf;
            ForceSetup = forceSetup;
        }

        /// <summary>
        /// Gets the mutable <see cref="StObjEngineConfiguration"/> to configure.
        /// This object is pre configured with the different values of <see cref="IStObjSetupTestHelperCore"/>.
        /// Its <see cref="StObjEngineConfiguration.Aspects"/> is empty: some aspect configurations must be
        /// added.
        /// </summary>
        public StObjEngineConfiguration StObjEngineConfiguration => _configuration;

        /// <summary>
        /// Gets or sets the <see cref="CKSetup.ICKSetupDriver.Run"/> forceSetup parameter.
        /// </summary>
        public ForceSetupLevel ForceSetup { get; set; }
    }
}
