using CK.Setup;
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
        public StObjSetupRunningEventArgs( StObjEngineConfiguration conf, bool forceSetup )
        {
            _configuration = conf;
            ForceSetup = forceSetup;
        }

        /// <summary>
        /// Gets or sets a mutable <see cref="StObjEngineConfiguration"/> (it must never be null).
        /// This object is configured with the different values of <see cref="IStObjSetupTestHelperCore"/>.
        /// Its <see cref="StObjEngineConfiguration.Aspects"/> is empty: some aspect configurations must be
        /// added.
        /// </summary>
        public StObjEngineConfiguration StObjEngineConfiguration
        {
            get => _configuration;
            set => _configuration = value ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// Gets or sets the <see cref="CKSetup.ICKSetupDriver.Run"/> forceSetup parameter.
        /// </summary>
        public bool ForceSetup { get; set; }
    }
}
