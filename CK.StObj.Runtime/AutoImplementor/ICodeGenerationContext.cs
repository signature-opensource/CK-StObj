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
        /// Gets all the <see cref="IGeneratedBinPath"/> including the <see cref="UnifiedBinPath"/>.
        /// </summary>
        IReadOnlyList<IGeneratedBinPath> AllBinPaths { get; }

        /// <summary>
        /// Gets the currently generated bin path.
        /// </summary>
        IGeneratedBinPath CurrentRun { get; }

        /// <summary>
        /// Gets the <see cref="IDynamicAssembly"/> to use to generate code of the <see cref="CurrentRun"/>.
        /// </summary>
        IDynamicAssembly RunAssembly { get; }

        /// <summary>
        /// Gets whether this is the primary run on the <see cref="UnifiedBinPath"/> or a secondary run.
        /// </summary>
        bool IsUnifiedRun => CurrentRun == UnifiedBinPath;

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
        IDictionary GlobalMemory { get; }
    }
}
