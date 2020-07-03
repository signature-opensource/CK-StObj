
using CK.CodeGen;
using CK.Core;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface are able to implement a method, a property or a type.
    /// </summary>
    /// <remarks>
    /// This is not defined in the CK.StObj.Model since this is typically implemented by attributes, but
    /// not by the original ("Model") attributes but by their delegated implementations that depend on
    /// the runtimes/engines (<see cref="ContextBoundDelegationAttribute.ActualAttributeTypeAssemblyQualifiedName"/>). 
    /// </remarks>
    public interface IAutoImplementor<T> where T : MemberInfo
    {
        /// <summary>
        /// Implements the given method, property or type on the given <see cref="ITypeScope"/> in the <paramref name="codeGenContext"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The member to implement.</param>
        /// <param name="codeGenContext">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>
        /// The <see cref="AutoImplementationResult"/>. If a <see cref="AutoImplementationResult.ImplementorType"/> is specified,
        /// it must implement this <see cref="IAutoImplementorMethod"/> interface.
        /// On error, the error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        AutoImplementationResult Implement( IActivityMonitor monitor, T m, ICodeGenerationContext codeGenContext, ITypeScope typeBuilder );
    }

}
