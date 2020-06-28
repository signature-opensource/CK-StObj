using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Global context that is provided to <see cref="IAutoImplementorType"/>, <see cref="IAutoImplementorMethod"/> and <see cref="IAutoImplementorProperty"/>
    /// implement methods.
    /// </summary>
    public interface ICodeGenerationContext
    {
        /// <summary>
        /// Gets the unified bin path.
        /// This is the first to be processed.
        /// </summary>
        IGeneratedBinPath UnifiedBinPath { get; }

        /// <summary>
        /// Gets the currently generated bin path among <see cref="AllBinPaths"/>.
        /// </summary>
        IGeneratedBinPath CurrentRun { get; }

        /// <summary>
        /// Gets all the <see cref="IGeneratedBinPath"/> including the <see cref="UnifiedBinPath"/>.
        /// </summary>
        IReadOnlyList<IGeneratedBinPath> AllBinPaths { get; }

        /// <summary>
        /// Gets the <see cref="IDynamicAssembly"/> to use to generate code of the <see cref="CurrentRun"/>.
        /// </summary>
        IDynamicAssembly Assembly { get; }

        /// <summary>
        /// Gets whether this is the initial run (on the <see cref="UnifiedBinPath"/>) or a secondary run.
        /// </summary>
        bool IsUnifiedRun { get; }

        /// <summary>
        /// Sets an object during the first run: this must be called only when <see cref="IsUnifiedRun"/> is true.
        /// This should be used to store objects from the "reality", object that unify all required aspects accross
        /// the <see cref="AllBinPaths"/>.
        /// </summary>
        /// <param name="key">Key of the object to cache.</param>
        /// <param name="o">The object to cache.</param>
        /// <param name="addOrUpdate">True to add or update. False (the default) throws an exception if the key already exists.</param>
        void SetUnifiedRunResult( string key, object o, bool addOrUpdate = false );

        /// <summary>
        /// Gets an object created by the first run: this must be called only when <see cref="IsUnifiedRun"/> is false.
        /// The key must exist otherwise a <see cref="KeyNotFoundException"/> is throw.
        /// </summary>
        /// <param name="key">Key of the cached result.</param>
        object GetUnifiedRunResult( string key );

        /// <summary>
        /// Gets a shared dictionary associated to the whole code generation context. 
        /// Note that use of such shared memory should be avoided as much as possible, and if required should be properly
        /// encapsulated, typically by extension methods on this context.
        /// </summary>
        IDictionary<object, object?> GlobalMemory { get; }

        /// <summary>
        /// Gets the global <see cref="IStObjEngineRunContext.ServiceContainer"/>.
        /// <see cref="ICodeGenerator.Implement"/> typically registers services inside this container so that
        /// deferred implementators (<see cref="AutoImplementationResult.ImplementorType"/>) can depend on them.
        /// </summary>
        ISimpleServiceContainer GlobalServiceContainer { get; }

        /// <summary>
        /// Gets whether the source code must eventually be saved.
        /// See <see cref="CompileSource"/>.
        /// </summary>
        bool SaveSource { get; }

        /// <summary>
        /// Gets whether the generated source code must be compiled.
        /// Both <see cref="SaveSource"/> and <see cref="CompileSource"/> can be false when <see cref="IsUnifiedRun"/> is true and
        /// the unified bin path doesn't correspond to any of the different <see cref="BinPathConfiguration"/>.
        /// </summary>
        bool CompileSource { get; }

    }
}
