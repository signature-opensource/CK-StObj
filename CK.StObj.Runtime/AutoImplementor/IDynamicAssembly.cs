using CK.CodeGen;
using CK.CodeGen.Abstractions;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CK.Setup
{
    /// <summary>
    /// Supports assembly generation.
    /// Actual support is not required by the model layer: runtime and engine are in charge of
    /// extending this abstraction in any required way.
    /// </summary>
    public interface IDynamicAssembly
    {
        /// <summary>
        /// Gets whether this is the primary, main, run or a secondary run.
        /// </summary>
        bool IsSecondaryRun { get; }

        /// <summary>
        /// Gets an object created by the first run: this must be called only when <see cref="IsSecondaryRun"/> is true.
        /// The key must exist otherwise a <see cref="KeyNotFoundException"/> is throw.
        /// </summary>
        /// <param name="key">Key of the cached result.</param>
        object GetPrimaryRunResult( string key );

        /// <summary>
        /// Sets an object during the first run: this must be called only when <see cref="IsSecondaryRun"/> is false.
        /// </summary>
        /// <param name="key">Key of the object to cache.</param>
        /// <param name="o">The object to cache.</param>
        /// <param name="addOrUpdate">True to add or update, false to throw an exception if the key already exists.</param>
        void SetPrimaryRunResult( string key, object o, bool addOrUpdate );

        /// <summary>
        /// Provides a new unique number that can be used for generating unique names inside this dynamic assembly.
        /// </summary>
        /// <returns>A unique number.</returns>
        string NextUniqueNumber();

        /// <summary>
        /// Gets a shared dictionary associated to the dynamic assembly. 
        /// Methods that generate code can rely on this to store shared information as required by their generation process.
        /// </summary>
        IDictionary Memory { get; }

        /// <summary>
        /// Gets the <see cref="ModuleBuilder"/> to use to emit stub types and other
        /// dynamic code.
        /// </summary>
        ModuleBuilder StubModuleBuilder { get; }

        /// <summary>
        /// Gets the default name space for this <see cref="IDynamicAssembly"/>
        /// into which code should be generated.
        /// Note that nothing prevents the <see cref="INamedScope.Workspace"/> to be used and other
        /// namespaces to be created.
        /// </summary>
        INamespaceScope DefaultGenerationNamespace { get; }

        /// <summary>
        /// Gets a mutable list of source code generator modules for this <see cref="IDynamicAssembly"/>.
        /// </summary>
        IList<ICodeGeneratorModule> SourceModules { get; }

    }

}
