using CK.CodeGen.Abstractions;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Basic implementation of the <see cref="IAutoImplementorType"/> interface that, by default, claims
    /// to take full responsibility for the implementation of every abstract methods and properties thanks to
    /// the "type based" method <see cref="Implement(IActivityMonitor, Type, ICodeGenerationContext, ITypeScope)"/>.
    /// </summary>
    public abstract class AutoImplementorType : IAutoImplementorType, IAutoImplementorMethod, IAutoImplementorProperty
    {
        /// <summary>
        /// See <see cref="IAutoImplementorType.HandleMethod(IActivityMonitor, MethodInfo)"/>.
        /// This default implementation returns this object so that <see cref="Implement(IActivityMonitor, MethodInfo, ICodeGenerationContext, ITypeScope)"/>
        /// will be called.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The abstract method.</param>
        /// <returns>This implementation.</returns>
        public virtual IAutoImplementorMethod? HandleMethod( IActivityMonitor monitor, MethodInfo m ) => this;

        /// <summary>
        /// See <see cref="IAutoImplementorType.HandleProperty(IActivityMonitor, PropertyInfo)"/>.
        /// This default implementation returns this object so that <see cref="Implement(IActivityMonitor, PropertyInfo, ICodeGenerationContext, ITypeScope)"/>
        /// will be called.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The abstract property.</param>
        /// <returns>This implementation.</returns>
        public virtual IAutoImplementorProperty? HandleProperty( IActivityMonitor monitor, PropertyInfo p ) => this;

        /// <summary>
        /// See <see cref="ICodeGenerator.Implement"/>.
        /// This default implementation returns true: the abstract <see cref="Implement(IActivityMonitor, Type, ICodeGenerationContext, ITypeScope)"/> "type
        /// based" method must do the job.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The method to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>Always <see cref="AutoImplementationResult.Success"/> at this level.</returns>
        public virtual AutoImplementationResult Implement( IActivityMonitor monitor, MethodInfo m, ICodeGenerationContext c, ITypeScope typeBuilder ) => AutoImplementationResult.Success;

        /// <summary>
        /// See <see cref="ICodeGenerator.Implement"/>.
        /// This default implementation returns true: the abstract <see cref="Implement(IActivityMonitor, Type, ICodeGenerationContext, ITypeScope)"/> "type
        /// based" method must do the job.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The property to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>Always <see cref="AutoImplementationResult.Success"/> at this level.</returns>
        public virtual AutoImplementationResult Implement( IActivityMonitor monitor, PropertyInfo p, ICodeGenerationContext c, ITypeScope typeBuilder ) => AutoImplementationResult.Success;

        /// <inheritdoc />
        public abstract AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope );

    }
}
