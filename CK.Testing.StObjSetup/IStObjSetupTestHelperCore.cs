using CK.Setup;
using CKSetup;
using System;

namespace CK.Testing.StObjSetup
{
    /// <summary>
    /// This core helper enables CKSetup runs of the StObj engine.
    /// This helper heavily relies on <see cref="CKSetup.ICKSetupTestHelperCore"/>.
    /// </summary>
    [ResolveTarget( typeof( IStObjSetupTestHelper ) )]
    public interface IStObjSetupTestHelperCore
    {
        /// <summary>
        /// Gets or sets whether source files must be generated alongside the generated assembly.
        /// Defaults to "StObjSetup/StObjGenerateSourceFiles" configuration or true if the configuration does not exist.
        /// </summary>
        bool StObjGenerateSourceFiles { get; set; }

        /// <summary>
        /// Gets ors sets whether the ordering of StObj that share the same rank in the dependency graph must be inverted.
        /// Defaults to "StObjSetup/StObjRevertOrderingNames" configuration or false if the configuration does not exist.
        /// Note that this configuration can be reused by aspects that also use topology sort instead of introducing another similar option.
        /// </summary>
        bool StObjRevertOrderingNames { get; set; }

        /// <summary>
        /// Gets or sets whether the dependency graph (the set of IDependentItem) associated
        /// to the StObj objects must be send to the monitor before and after sorting.
        /// Defaults to "StObjSetup/StObjTraceGraphOrdering" configuration or false if the configuration does not exist.
        /// Note that this configuration can be reused by aspects that also use topology sort instead of introducing another similar option.
        /// </summary>
        bool StObjTraceGraphOrdering { get; set; }

        /// <summary>
        /// Fires before a Setup must be done.
        /// Aspect configurations must be added to the <see cref="StObjEngineConfiguration.Aspects"/>.
        /// </summary>
        event EventHandler<StObjSetupRunningEventArgs> StObjSetupRunning;

        /// <summary>
        /// Runs a StObj setup. This fires the <see cref="StObjSetupRunning"/> event and calls
        /// the actual CKSetup <see cref="CKSetup.ICKSetupDriver.Run"/> with the final configuration.
        /// This is automatically called by any access to the <see cref="StObjMap.IStObjMapTestHelperCore.StObjMapLoading"/> whenever
        /// the generated assembly name can not be found.
        /// </summary>
        /// <param name="configuration">Required StObj engine configuration.</param>
        /// <param name="forceSetup">True to force the setup (skipping any signature check).</param>
        /// <returns>The CKSetup run result.</returns>
        CKSetupRunResult RunStObjSetup( StObjEngineConfiguration configuration, bool forceSetup = false );

    }
}
