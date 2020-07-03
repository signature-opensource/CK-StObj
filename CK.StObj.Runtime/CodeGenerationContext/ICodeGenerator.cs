
using CK.Core;


namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface can participate in code generation.
    /// </summary>
    /// <remarks>
    /// This is not defined in the CK.StObj.Model since this is typically implemented by attributes, but
    /// not by the original ("Model") attributes but by their delegated implementations that depend on
    /// the runtimes/engines (<see cref="ContextBoundDelegationAttribute.ActualAttributeTypeAssemblyQualifiedName"/>). 
    /// </remarks>
    public interface ICodeGenerator
    {
        /// <summary>
        /// Implementations can interact with the given <see cref="ICodeGenerationContext"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="codeGenContext">Code generation context with its Dynamic assembly being implemented.</param>
        /// <returns>
        /// The <see cref="AutoImplementationResult"/>. If a <see cref="AutoImplementationResult.ImplementorType"/> is specified,
        /// it must implement this <see cref="ICodeGenerator"/> interface.
        /// On error, the error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        AutoImplementationResult Implement( IActivityMonitor monitor, ICodeGenerationContext codeGenContext );
    }

}
