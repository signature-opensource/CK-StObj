using System.Reflection;
using CK.CodeGen.Abstractions;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface are able to implement a property.
    /// </summary>
    /// <remarks>
    /// This is not defined in the CK.StObj.Model since this is typically implemented by attributes, but
    /// not by the original ("Model") attributes but by their delegated implementations that depend on
    /// the runtimes/engines (<see cref="ContextBoundDelegationAttribute.ActualAttributeTypeAssemblyQualifiedName"/>). 
    /// </remarks>
    public interface IAutoImplementorProperty
    {
        /// <summary>
        /// Implements the given property on the given <see cref="ITypeScope"/>.
        /// Implementations can rely on the <paramref name="dynamicAssembly"/>.<see cref="IDynamicAssembly.Memory">Memory</see> to store shared information if needed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The property to implement.</param>
        /// <param name="dynamicAssembly">Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>
        /// True if the property is actually implemented, false if, for any reason, another implementation (empty for instance) must be generated 
        /// (for instance, whenever the property is not ready to be implemented).
        /// Any error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        bool Implement( IActivityMonitor monitor, PropertyInfo p, IDynamicAssembly dynamicAssembly, ITypeScope typeBuilder );
    }

}
