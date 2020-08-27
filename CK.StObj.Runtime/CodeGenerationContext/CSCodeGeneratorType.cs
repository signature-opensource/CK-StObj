using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Basic implementation of the <see cref="ICSCodeGeneratorType"/> interface that, by default, claims
    /// to take full responsibility for the implementation of every abstract methods and properties thanks to
    /// the "type based" method <see cref="Implement(IActivityMonitor, Type, ICSCodeGenerationContext, ITypeScope)"/>.
    /// </summary>
    public abstract class CSCodeGeneratorType : ICSCodeGeneratorType, IAutoImplementorMethod, IAutoImplementorProperty
    {
        /// <summary>
        /// See <see cref="ICSCodeGeneratorType.HandleMethod(IActivityMonitor, MethodInfo)"/>.
        /// This default implementation returns this object so that <see cref="Implement(IActivityMonitor, MethodInfo, ICSCodeGenerationContext, ITypeScope)"/>
        /// will be called.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The abstract method.</param>
        /// <returns>This implementation.</returns>
        public virtual IAutoImplementorMethod? HandleMethod( IActivityMonitor monitor, MethodInfo m ) => this;

        /// <summary>
        /// See <see cref="ICSCodeGeneratorType.HandleProperty(IActivityMonitor, PropertyInfo)"/>.
        /// This default implementation returns this object so that <see cref="Implement(IActivityMonitor, PropertyInfo, ICSCodeGenerationContext, ITypeScope)"/>
        /// will be called.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The abstract property.</param>
        /// <returns>This implementation.</returns>
        public virtual IAutoImplementorProperty? HandleProperty( IActivityMonitor monitor, PropertyInfo p ) => this;

        /// <summary>
        /// See <see cref="ICodeGenerator.Implement"/>.
        /// This default implementation returns true: the abstract <see cref="Implement(IActivityMonitor, Type, ICSCodeGenerationContext, ITypeScope)"/> "type
        /// based" method must do the job.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="m">The method to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>Always <see cref="CSCodeGenerationResult.Success"/> at this level.</returns>
        public virtual CSCodeGenerationResult Implement( IActivityMonitor monitor, MethodInfo m, ICSCodeGenerationContext c, ITypeScope typeBuilder ) => CSCodeGenerationResult.Success;

        /// <summary>
        /// See <see cref="ICodeGenerator.Implement"/>.
        /// This default implementation returns true: the abstract <see cref="Implement(IActivityMonitor, Type, ICSCodeGenerationContext, ITypeScope)"/> "type
        /// based" method must do the job.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="p">The property to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <returns>Always <see cref="CSCodeGenerationResult.Success"/> at this level.</returns>
        public virtual CSCodeGenerationResult Implement( IActivityMonitor monitor, PropertyInfo p, ICSCodeGenerationContext c, ITypeScope typeBuilder ) => CSCodeGenerationResult.Success;

        /// <summary>
        /// Must implement the full type.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="classType">The full base type to implement.</param>
        /// <param name="c">Code generation context with its Dynamic assembly being implemented.</param>
        /// <param name="scope">The type builder of the specialized class to implement.</param>
        /// <returns></returns>
        public abstract CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope );

    }
}
