using CK.CodeGen;
using CK.Core;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CK.Setup
{
    /// <summary>
    /// Supports a module for emit and source code generation helpers.
    /// </summary>
    public interface IDynamicAssembly
    {
        /// <summary>
        /// Provides a new unique number that can be used for generating unique names inside this dynamic assembly.
        /// </summary>
        /// <returns>A unique number.</returns>
        string NextUniqueNumber();

        /// <summary>
        /// Gets a shared dictionary associated to this dynamic assembly. 
        /// Methods that generate code can rely on this to store shared information as required by their generation process.
        /// If information has to be shared among different <see cref="IGeneratedBinPath"/> contexts, then the <see cref="ICodeGenerationContext.GlobalMemory"/>
        /// must be used.
        /// </summary>
        IDictionary<object, object?> Memory { get; }

        /// <summary>
        /// Gets the <see cref="ModuleBuilder"/> to use to emit stub types and other
        /// dynamic code.
        /// </summary>
        ModuleBuilder StubModuleBuilder { get; }

        /// <summary>
        /// Gets the <see cref="ICodeWorkspace"/> for this <see cref="IDynamicAssembly"/> into which
        /// code should be generated.
        /// </summary>
        ICodeWorkspace Code { get; }
    }

}
