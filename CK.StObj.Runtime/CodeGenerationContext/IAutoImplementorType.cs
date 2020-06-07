using CK.CodeGen.Abstractions;
using CK.Core;
using System;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Classes that implement this interface are able to provide any implementation they want to an abstract class.
    /// </summary>
    /// <remarks>
    /// This is not defined in the CK.StObj.Model since this is typically implemented by attributes, but
    /// not by the original ("Model") attributes but by their delegated implementations that depend on
    /// the runtimes/engines (<see cref="ContextBoundDelegationAttribute.ActualAttributeTypeAssemblyQualifiedName"/>). 
    /// </remarks>
    public interface IAutoImplementorType
    {
        /// <summary>
        /// Must check whether the given abstract method is handled by this implementor.
        /// When null is returned, the method must be handled by another <see cref="IAutoImplementorType"/> or
        /// a <see cref="IAutoImplementorMethod"/>.
        /// A typical implementation returns this type implementor that also implements <see cref="IAutoImplementorMethod"/> and <see cref="IAutoImplementorProperty"/>
        /// with a simple return true implementation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The abstract method.</param>
        /// <returns>A non null <see cref="IAutoImplementorMethod"/> if the method is handled by this implementor, false otherwise.</returns>
        IAutoImplementorMethod? HandleMethod( IActivityMonitor monitor, MethodInfo m );

        /// <summary>
        /// Must check whether the given abstract method is handled by this implementor.
        /// When null is returned, the method must be handled by another <see cref="IAutoImplementorType"/> or
        /// a <see cref="IAutoImplementorProperty"/>.
        /// A typical implementation returns this type implementor that also implements <see cref="IAutoImplementorMethod"/> and <see cref="IAutoImplementorProperty"/>
        /// with a simple return true implementation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The abstract property.</param>
        /// <returns>A non null <see cref="IAutoImplementorProperty"/> if the property is handled by this implementor, false otherwise.</returns>
        IAutoImplementorProperty? HandleProperty( IActivityMonitor monitor, PropertyInfo p );

        /// <summary>
        /// Generates any required code for the given abstract class in the given <see cref="ITypeScope"/>.
        /// Implementations can rely on the <paramref name="dynamicAssembly"/> to store shared information if needed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="classType">The abstract class type to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="scope">The type scope into which the implementation should be generated.</param>
        /// <returns>
        /// True on success, false on error. 
        /// Any error must be logged into the <paramref name="monitor"/>.
        /// </returns>
        bool Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope );
    }

}
