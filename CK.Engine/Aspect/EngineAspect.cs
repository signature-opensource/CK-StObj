using CK.Core;
using System.Threading;

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
    /// <para>
    /// This is mutable but should only be altered (if needed) by <see cref="Initialize"/>. Modifying this configuration
    /// after the initialization is not supported and can have really bad effects.
    /// </para>
    /// </summary>
    public EngineConfiguration EngineConfiguration => _aspectConfiguration.Owner!;

    /// <summary>
    /// Gets this aspect configuration.
    /// <para>
    /// This is mutable but should only be altered (if needed) by <see cref="Initialize"/>. Modifying this configuration
    /// after the initialization is not supported and can have really bad effects.
    /// </para>
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
    /// <param name="context">The engine run context.</param>
    /// <returns>
    /// Must return true on success, false if any error occurred (errors must be logged).
    /// </returns>
    public virtual bool RunPreCode( IActivityMonitor monitor, IEngineRunContext context )
    {
        return true;
    }

    /// <summary>
    /// Runs the aspect once the code generation has been successfully done.
    /// <para>
    /// This default implementation does nothing and always returns true.
    /// </para>
    /// </summary>
    /// <param name="monitor">Monitor to use.</param>
    /// <param name="context">The engine run context.</param>
    /// <returns>
    /// Must return true on success, false if any error occurred (errors must be logged).
    /// </returns>
    public virtual bool RunPostCode( IActivityMonitor monitor, IEngineRunContext context )
    {
        return true;
    }

    /// <summary>
    /// Called by the engine in reverse order of the initialization.
    /// <para>
    /// This default implementation does nothing and always returns true.
    /// </para>
    /// </summary>
    /// <param name="monitor">Monitor to use.</param>
    /// <param name="context">Terminate context.</param>
    /// <returns>
    /// Must return true on success, false if any error occurred (errors must be logged).
    /// Returning false doesn't prevent other aspect to be terminated.
    /// </returns>
    public virtual bool Terminate( IActivityMonitor monitor, IEngineRunContext context )
    {
        return true;
    }

}
