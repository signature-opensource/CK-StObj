using CK.CodeGen.Abstractions;
using CK.Core;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface are able to implement a method.
    /// </summary>
    /// <remarks>
    /// This is not defined in the CK.StObj.Model since this is typically implemented by attributes, but
    /// not by the original ("Model") attributes but by their delegated implementations that depend on
    /// the runtimes/engines (<see cref="ContextBoundDelegationAttribute.ActualAttributeTypeAssemblyQualifiedName"/>). 
    /// </remarks>
    public interface IAutoImplementorMethod
    {
        /// <summary>
        /// Implements the given method on the given <see cref="ITypeScope"/>.
        /// Implementations can rely on the <paramref name="dynamicAssembly"/> to store shared information if needed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The method to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>
        /// True on success, false on error. 
        /// Any error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        bool Implement( IActivityMonitor monitor, MethodInfo m, ICodeGenerationContext c, ITypeScope typeBuilder );
    }

}
