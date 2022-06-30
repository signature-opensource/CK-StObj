using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Global context that is provided to code generators.
    /// This context is bound to one <see cref="IGeneratedBinPath"/> (the <see cref="CurrentRun"/>) that groups 0 or more equivalent <see cref="BinPathConfiguration"/>.
    /// The <see cref="ICSCodeGenerationContext"/> specializes this code generation context to expose properties specific to CSharp code generation.
    /// </summary>
    public interface ICodeGenerationContext
    {
        /// <summary>
        /// Gets the currently generated bin path among <see cref="AllBinPaths"/>.
        /// </summary>
        IGeneratedBinPath CurrentRun { get; }

        /// <summary>
        /// Gets all the <see cref="IGeneratedBinPath"/> that are processed in order: the first one (the <see cref="IStObjEngineRunContext.PrimaryBinPath"/>)
        /// is guaranteed to "cover" all of them.
        /// </summary>
        IReadOnlyList<IGeneratedBinPath> AllBinPaths { get; }

        /// <summary>
        /// Gets whether this is the initial run (on the <see cref="IStObjEngineRunContext.PrimaryBinPath"/>) or a secondary run.
        /// </summary>
        bool IsPrimaryRun { get; }

        /// <summary>
        /// Sets an object during the first run: this must be called only when <see cref="IsPrimaryRun"/> is true.
        /// This should be used to store objects from the "reality", object that unify all required aspects across
        /// the <see cref="AllBinPaths"/>.
        /// </summary>
        /// <param name="key">Key of the object to cache.</param>
        /// <param name="o">The object to cache.</param>
        /// <param name="addOrUpdate">True to add or update. False (the default) throws an exception if the key already exists.</param>
        void SetPrimaryRunResult( string key, object o, bool addOrUpdate = false );

        /// <summary>
        /// Gets an object created by the first run: this must be called only when <see cref="IsPrimaryRun"/> is false.
        /// The key must exist otherwise a <see cref="KeyNotFoundException"/> is throw.
        /// </summary>
        /// <param name="key">Key of the cached result.</param>
        object GetPrimaryRunResult( string key );

        /// <summary>
        /// Gets a shared dictionary associated to the whole code generation context. 
        /// Note that use of such shared memory should be avoided as much as possible, and if required should be properly
        /// encapsulated, typically by extension methods on this context.
        /// </summary>
        IDictionary<object, object?> GlobalMemory { get; }

        /// <summary>
        /// Gets the global <see cref="IStObjEngineRunContext.ServiceContainer"/>.
        /// <see cref="IStObjEngineAspect"/> classes typically registers services
        /// inside this container so that code generator can use them.
        /// <para>
        /// When a code generator supports a trampoline or any other means to defer a subsequent execution,
        /// a code generation step can also registers any number of services that will be available to subsequent
        /// code generator. A <see cref="ICSCodeGenerator.Implement"/> for instance can register services and 
        /// deferred implementors (see <see cref="CSCodeGenerationResult.ImplementorType"/>
        /// or <see cref="CSCodeGenerationResult.MethodName"/>) can use them.
        /// </para>
        /// </summary>
        ISimpleServiceContainer GlobalServiceContainer { get; }

    }
}
