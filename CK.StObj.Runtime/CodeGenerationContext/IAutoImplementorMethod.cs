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
    public interface IAutoImplementorMethod : IAutoImplementor<MethodInfo>
    {
    }

}
