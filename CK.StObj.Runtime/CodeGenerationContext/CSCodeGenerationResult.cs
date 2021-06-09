using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Defines the outcome of the <see cref="ICSCodeGenerator.Implement"/> and <see cref="IAutoImplementor{T}.Implement"/>
    /// methods:
    /// <list type="bullet">
    ///     <item>The implementation has been successfully done (use the singleton <see cref="CSCodeGenerationResult.Success"/>).</item>
    ///     <item>The implementation failed (use the singleton <see cref="CSCodeGenerationResult.Failed"/>).</item>
    ///     <item>A dedicated type must be instantiated (with dependencies injection support): use the <see cref="CSCodeGenerationResult(Type)"/> constructor. See <see cref="ImplementorType"/>.</item>
    ///     <item>Another method on the same object (with parameter dependencies injection support) must be called: use the <see cref="CSCodeGenerationResult(string)"/> constructor with the method name.</item>
    /// </list>
    /// This trampoline mechanism allows constructor or method parameter injection and avoids a direct lookup (service locator anti-pattern) into the <see cref="ICodeGenerationContext.GlobalServiceContainer"/>
    /// or the <see cref="IGeneratedBinPath.ServiceContainer"/> of th current run.
    /// </summary>
    public readonly struct CSCodeGenerationResult
    {
        readonly bool _success;

        /// <summary>
        /// Express a successful, final, result.
        /// </summary>
        public static readonly CSCodeGenerationResult Success = new CSCodeGenerationResult( true );

        /// <summary>
        /// Express a failed, final, result.
        /// </summary>
        public static readonly CSCodeGenerationResult Failed = new CSCodeGenerationResult( false );

        /// <summary>
        /// Gets whether an error occurred. When true, there is nothing more to do.
        /// </summary>
        public bool HasError => !_success && ImplementorType == null && MethodName == null;

        /// <summary>
        /// Gets the type that must be instantiated and that will finalize the generation of the source code.
        /// This type must be a <see cref="IAutoImplementorMethod"/>, <see cref="IAutoImplementorProperty"/> or <see cref="ICSCodeGeneratorType"/>
        /// that must be the same as the initial implementor.
        /// <para>
        /// This type can have constructor parameters that will be resolved from the available services: the current run <see cref="IGeneratedBinPath.ServiceContainer"/>
        /// and <see cref="ICodeGenerationContext.GlobalServiceContainer"/>.
        /// </para>
        /// </summary>
        public readonly Type? ImplementorType;

        /// <summary>
        /// Gets the name of a method (that can be private) of the initial implementor that will continue the generation of the source code.
        /// (Note that this method can redirect to yet another method.)
        /// <para>
        /// This method can have parameters that will be resolved from the available services: the current run <see cref="IGeneratedBinPath.ServiceContainer"/>
        /// and <see cref="ICodeGenerationContext.GlobalServiceContainer"/>.
        /// </para>
        /// </summary>
        public readonly string? MethodName;

        CSCodeGenerationResult( bool success )
        {
            _success = success;
            ImplementorType = null;
            MethodName = null;
        }

        /// <summary>
        /// Initializes a new result with a type that must be instantiated.
        /// See <see cref="ImplementorType"/>.
        /// </summary>
        /// <param name="implementor">The type to implement.</param>
        public CSCodeGenerationResult( Type implementor )
        {
            _success = false;
            ImplementorType = implementor;
            MethodName = null;
        }

        /// <summary>
        /// Initializes a new result with the name of a method that will be called.
        /// See <see cref="MethodName"/>.
        /// </summary>
        /// <param name="unambiguousMethodName">The name of the method to call.</param>
        public CSCodeGenerationResult( string unambiguousMethodName )
        {
            _success = false;
            ImplementorType = null;
            MethodName = unambiguousMethodName;
        }

    }
}
