using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an aspect of the SetupEngine.
    /// <para>
    /// Concrete Aspect classes must implement this interface and have a public constructor
    /// that takes the configuration object instance.
    /// <see cref="Configure"/> will be called once all aspects have been instanciated.
    /// </para>
    /// <para>
    /// The configuration object is a <see cref="IStObjEngineAspectConfiguration"/> that has been 
    /// added to the <see cref="StObjEngineConfiguration.Aspects"/> list and 
    /// whose <see cref="IStObjEngineAspectConfiguration.AspectType"/> is the assembly qualified name
    /// of the Aspect they configure.
    /// </para>
    /// </summary>
    public interface IStObjEngineAspect
    {
        /// <summary>
        /// Called by the engine right after the aspect have been successfuly created.
        /// This method typically registers services that will be available to following aspects.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Configuration context.</param>
        /// <returns>
        /// Must return true on success, false if any error occured (errors must be logged).
        /// Returning false prevents any subsequent <see cref="Run"/> (the engine does not even build
        /// the StObj graph) but the remaining aspects are nevertheless configured in order to
        /// detect potential other configuration errors.
        /// </returns>
        bool Configure( IActivityMonitor monitor, IStObjEngineConfigureContext context );

        /// <summary>
        /// Runs the aspect once the StObjs graphs have been successfully build.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Run context.</param>
        /// <returns>
        /// Must return true on succes, false if any error occured (errors must be logged).
        /// Returning false does not stop the engine: <see cref="IStObjEngineStatus.Success"/> is set to false
        /// and following aspects are run, the final assembly is not generated and <see cref="Terminate"/> is
        /// called on all the aspects in reverse order.
        /// </returns>
        bool Run( IActivityMonitor monitor, IStObjEngineRunContext context );

        /// <summary>
        /// Called by the engine in reverse order after all aspects have <see cref="Run"/>.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="context">Terminate context.</param>
        /// <returns>
        /// Must return true on succes, false if any error occured (errors must be logged).
        /// Returning false sets <see cref="IStObjEngineStatus.Success"/> to false but preceeding
        /// aspects are terminated.
        /// </returns>
        bool Terminate( IActivityMonitor monitor, IStObjEngineTerminateContext context );

    }
}
