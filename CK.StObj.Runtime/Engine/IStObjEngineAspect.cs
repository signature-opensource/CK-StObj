using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an aspect of the SetupEngine.
    /// <para>
    /// Concrete Aspect classes must implement this interface and have a public constructor
    /// that takes the configuration object instance.
    /// </para>
    /// <para>
    /// The configuration object is a <see cref="EngineAspectConfiguration"/> that has been 
    /// added to the <see cref="EngineConfiguration.Aspects"/> list and 
    /// whose <see cref="EngineAspectConfiguration.AspectType"/> is the assembly qualified name
    /// of the Aspect they configure.
    /// </para>
    /// <para>
    /// Aspects can implement <see cref="ICSCodeGenerator"/> if they need to directly participate to
    /// code generation. When implemented <see cref="ICSCodeGenerator.Implement"/>
    /// is called (for each <see cref="ICodeGenerationContext.CurrentRun"/>) after <see cref="RunPreCode(IActivityMonitor, IStObjEngineRunContext)"/>
    /// and before <see cref="RunPostCode(IActivityMonitor, IStObjEnginePostCodeRunContext)"/>.
    /// </para>
    /// </summary>
    public interface IStObjEngineAspect
    {
        /// <summary>
        /// Called by the engine right after the aspect have been successfully created.
        /// This method typically registers services that will be available to following aspects.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Configuration context.</param>
        /// <returns>
        /// Must return true on success, false if any error occurred (errors must be logged).
        /// Returning false prevents any subsequent <see cref="RunPreCode"/> (the engine does not even build
        /// the StObj graph) but the remaining aspects are nevertheless configured in order to
        /// detect potential other configuration errors.
        /// </returns>
        bool Configure( IActivityMonitor monitor, IStObjEngineConfigureContext context );

        /// <summary>
        /// Called when <see cref="IStObjEngineConfigureContext.CanSkipRun"/> is true after all aspects
        /// have been configured: the setup is skipped, the next call will be for <see cref="Terminate(IActivityMonitor, IStObjEngineTerminateContext)"/>.
        /// <para>
        /// This enables aspects to update any resources (like <see cref="IGeneratedArtifact"/>) is needed.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if an error occurred.</returns>
        bool OnSkippedRun( IActivityMonitor monitor );

        /// <summary>
        /// Runs the aspect once the StObjs graphs have been successfully build.
        /// When this method is called, <see cref="IStObjEngineStatus.Success"/> may be false: it is
        /// up to the implementation to decide to skip its own process in this case.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Run context.</param>
        /// <returns>
        /// Must return true on success, false if any error occurred (errors must be logged).
        /// Returning false does not stop the engine: <see cref="IStObjEngineStatus.Success"/> is set to false
        /// and following aspects are run, the final assembly is not generated and <see cref="Terminate"/> is
        /// called on all the aspects in reverse order.
        /// </returns>
        bool RunPreCode( IActivityMonitor monitor, IStObjEngineRunContext context );

        /// <summary>
        /// Runs the aspect once the Code generation has been successfully done.
        /// When this method is called, <see cref="IStObjEngineStatus.Success"/> may be false: it is
        /// up to the implementation to decide to skip its own process in this case.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Run context.</param>
        /// <returns>
        /// Must return true on success, false if any error occurred (errors must be logged).
        /// Returning false does not stop the engine: <see cref="IStObjEngineStatus.Success"/> is set to false
        /// and following aspects are run, the final assembly is not generated and <see cref="Terminate"/> is
        /// called on all the aspects in reverse order.
        /// </returns>
        bool RunPostCode( IActivityMonitor monitor, IStObjEnginePostCodeRunContext context );

        /// <summary>
        /// Called by the engine in reverse order after all aspects have <see cref="RunPreCode"/>.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Terminate context.</param>
        /// <returns>
        /// Must return true on success, false if any error occurred (errors must be logged).
        /// Returning false sets <see cref="IStObjEngineStatus.Success"/> to false but preceding
        /// aspects are terminated.
        /// </returns>
        bool Terminate( IActivityMonitor monitor, IStObjEngineTerminateContext context );

    }
}
