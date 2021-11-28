
using CK.Core;


namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface can participate in CSharp code generation or any other kind of generation.
    /// It is independent of any type and can use the <see cref="CSCodeGenerationResult"/> to redirect
    /// with a method that expects one or more specific service (like <see cref="Json.JsonSerializationCodeGen"/>
    /// for instance).
    /// <para>
    /// See <see cref="CSCodeGeneratorType"/> to implement a code generator for a Type. 
    /// </para>
    /// </summary>
    /// <remarks>
    /// This is not defined in the CK.StObj.Model since this is typically implemented by attributes, but
    /// not by the original ("Model") attributes but by their delegated implementations that depend on
    /// the runtimes/engines (<see cref="ContextBoundDelegationAttribute.ActualAttributeTypeAssemblyQualifiedName"/>). 
    /// </remarks>
    public interface ICSCodeGenerator
    {
        /// <summary>
        /// Implementations can interact with the given <see cref="ICSCodeGenerationContext"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="codeGenContext">Code generation context with its Dynamic assembly being implemented.</param>
        /// <returns>
        /// The <see cref="CSCodeGenerationResult"/>. If a <see cref="CSCodeGenerationResult.ImplementorType"/> is specified,
        /// it must implement this <see cref="ICSCodeGenerator"/> interface.
        /// On error, the error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext );
    }

}
