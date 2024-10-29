
using CK.Core;

namespace CK.Setup;

/// <summary>
/// Non generic base class of <see cref="EngineAspect{T}"/>.
/// Aspect of the Engine are plugins configured by a <see cref="EngineAspectConfiguration"/>.
/// </summary>
public abstract class EngineAspect
{
    readonly EngineAspectConfiguration _aspectConfiguration;

    private protected EngineAspect( EngineAspectConfiguration aspectConfiguration )
    {
        Throw.DebugAssert( aspectConfiguration.Owner != null );
        _aspectConfiguration = aspectConfiguration;
    }

    /// <summary>
    /// Gets the engine configuration.
    /// </summary>
    public EngineConfiguration EngineConfiguration => _aspectConfiguration.Owner!;

    /// <summary>
    /// Gets this aspect configuration.
    /// </summary>
    public EngineAspectConfiguration AspectConfiguration => _aspectConfiguration;

    /// <summary>
    /// Called by the engine right after the aspect have been successfully created.
    /// This method can:
    /// <list type="bullet">
    ///     <item>
    ///     Alter the <see cref="EngineConfiguration"/> at its onw risk: the configuration has already been normalized and
    ///     no subsequent checks will be done on it.
    ///     </item>
    ///     <item>Register configure only services that will be available to following aspects.</item>
    ///     <item>Register engine services that will be available to attribute engines and other aspects.</item>
    ///     <item>Provides RealObject configuration capabilities by adding layers to the <see cref="StObjEngineConfigurator"/>.</item>
    /// </list>
    /// This default implementation does nothing and always returns true.
    /// </summary>
    /// <param name="monitor">Monitor to use.</param>
    /// <param name="context">Configuration context.</param>
    /// <returns>
    /// Must return true on success, false if any error occurred (errors must be logged).
    /// Returning false prevents any subsequent <see cref="RunPreCode"/> (the engine does
    /// not even handles types) but the remaining aspects are nevertheless initialized in order to
    /// detect potential other configuration errors.
    /// </returns>
    public virtual bool Initialize( IActivityMonitor monitor, IEngineAspectInitializationContext context )
    {
        return true;
    }

    /// <summary>
    /// Runs the aspect once the <see cref="IGeneratedBinPath"/> have been successfully built.
    /// <para>
    /// This default implementation does nothing and always returns true.
    /// </para>
    /// </summary>
    /// <param name="monitor">Monitor to use.</param>
    /// <param name="context">Run context.</param>
    /// <returns>
    /// Must return true on success, false if any error occurred (errors must be logged).
    /// </returns>
    public virtual bool RunPreCode( IActivityMonitor monitor, IEngineRunContext context )
    {
        return true;
    }

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
    public virtual bool RunPostCode( IActivityMonitor monitor, IStObjEnginePostCodeRunContext context )
    {
        return true;
    }

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
