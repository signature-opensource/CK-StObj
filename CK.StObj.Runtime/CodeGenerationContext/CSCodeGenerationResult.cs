using CK.Core;
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
    ///     <item>The same method must be retried during the next pass (use the singleton <see cref="CSCodeGenerationResult.Retry"/>).</item>
    ///     <item>Another method on the same object (with parameter dependencies injection support) must be called: use the <see cref="CSCodeGenerationResult(string)"/> constructor with the method name.</item>
    /// </list>
    /// This trampoline mechanism allows method parameter injection and avoids a direct lookup (service locator anti-pattern) into the <see cref="IGeneratedBinPath.ServiceContainer"/>
    /// of the <see cref="ICodeGenerationContext.CurrentRun"/>.
    /// </summary>
    public readonly struct CSCodeGenerationResult
    {
        readonly string? _methodName;
        readonly int _flag;

        /// <summary>
        /// Express a failed final result.
        /// </summary>
        public static readonly CSCodeGenerationResult Failed = new CSCodeGenerationResult();

        /// <summary>
        /// Express a successful final result.
        /// </summary>
        public static readonly CSCodeGenerationResult Success = new CSCodeGenerationResult( 1 );

        /// <summary>
        /// Express a failed final result.
        /// </summary>
        public static readonly CSCodeGenerationResult Retry = new CSCodeGenerationResult( 2 );

        /// <summary>
        /// Gets whether an error occurred. When true, there is nothing more to do.
        /// </summary>
        public bool HasError => _flag == 0;

        /// <summary>
        /// Gets whether the call succeed.
        /// </summary>
        public bool IsSuccess => _flag == 1;

        /// <summary>
        /// Gets whether the current call should be retried during the next pass.
        /// </summary>
        public bool IsRetry => _flag == 2;

        /// <summary>
        /// Gets the name of a method (that can be private) that will continue the generation of the source code.
        /// (Note that this method can redirect to yet another method.)
        /// <para>
        /// This method can have parameters that will be resolved from the available services
        /// in <see cref="ICodeGenerationContext.CurrentRun"/>'s <see cref="IGeneratedBinPath.ServiceContainer"/>.
        /// </para>
        /// </summary>
        public string? MethodName => _methodName;

        CSCodeGenerationResult( int f )
        {
            _methodName = null;
            _flag = f;
        }


        /// <summary>
        /// Initializes a new result with the name of a method that will be called.
        /// See <see cref="MethodName"/>.
        /// </summary>
        /// <param name="unambiguousMethodName">The name of the method to call.</param>
        public CSCodeGenerationResult( string unambiguousMethodName )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( unambiguousMethodName );
            _methodName = unambiguousMethodName;
            _flag = 4;
        }

    }
}
