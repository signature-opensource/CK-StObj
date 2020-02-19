using CK.CodeGen.Abstractions;
using CK.Core;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface are able to implement a method.
    /// </summary>
    public interface IAutoImplementorMethod
    {
        /// <summary>
        /// Implements the given method on the given <see cref="ITypeScope"/>.
        /// Implementations can rely on the <paramref name="dynamicAssembly"/> to store shared information if needed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The method to implement.</param>
        /// <param name="dynamicAssembly">Dynamic assembly being implemented.</param>
        /// <param name="b">The type builder to use.</param>
        /// <returns>
        /// True on success, false on error. 
        /// Any error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        bool Implement( IActivityMonitor monitor, MethodInfo m, IDynamicAssembly dynamicAssembly, ITypeScope b );
    }

}
