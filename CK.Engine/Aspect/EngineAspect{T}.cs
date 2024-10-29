namespace CK.Setup;

/// <summary>
/// Aspect of the Engine are plugins configured by their associated <see cref="EngineAspectConfiguration"/>.
/// <para>
/// Concrete Aspect classes must specialize this class and have a public constructor
/// that takes at least the configuration object instance.
/// </para>
/// <para>
/// The configuration object is a <see cref="EngineAspectConfiguration"/> that has been 
/// added to the <see cref="EngineConfiguration.Aspects"/> list and 
/// whose <see cref="EngineAspectConfiguration.AspectType"/> is the assembly qualified name
/// of the concrete aspect class.
/// </para>
/// <para>
/// Aspects can implement <see cref="ICSCodeGenerator"/> if they need to directly participate to
/// code generation. When implemented <see cref="ICSCodeGenerator.Implement"/>
/// is called (for each <see cref="ICodeGenerationContext.CurrentRun"/>) after <see cref="EngineAspect.RunPreCode(IActivityMonitor, IStObjEngineRunContext)"/>
/// and before <see cref="EngineAspect.RunPostCode(IActivityMonitor, IStObjEnginePostCodeRunContext)"/>.
/// </para>
/// </summary>
public abstract class EngineAspect<T> : EngineAspect where T : EngineAspectConfiguration
{
    protected EngineAspect( T aspectConfiguration )
        : base( aspectConfiguration )
    {
    }

    /// <inheritdoc />
    public new T AspectConfiguration => (T)base.AspectConfiguration;

}
