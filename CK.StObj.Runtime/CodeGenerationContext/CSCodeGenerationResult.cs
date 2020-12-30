using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Defines the outcome of the <see cref="ICodeGenerator.Implement"/> and <see cref="IAutoImplementor{T}.Implement"/>
    /// methods: either the implementation has been successfuly done, must be done by a dedicated type that must
    /// be instantiated (with dependencies injection support), must be done by calling another method on the same object
    /// (with parameter dependencies injection support) or failed.
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
        /// </summary>
        public readonly Type? ImplementorType;

        /// <summary>
        /// Gets the name of a method (that can be private) of the initial implementor that will finalize the generation of the source code.
        /// </summary>
        public readonly string? MethodName;

        CSCodeGenerationResult( bool success )
        {
            _success = success;
            ImplementorType = null;
            MethodName = null;
        }

        /// <summary>
        /// Initializes a new result with a type that must be instanciated.
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
        /// <param name="unambiguousMethodName">The name of the metod to call.</param>
        public CSCodeGenerationResult( string unambiguousMethodName )
        {
            _success = false;
            ImplementorType = null;
            MethodName = unambiguousMethodName;
        }

    }
}
