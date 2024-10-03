using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace CK.Setup;

/// <summary>
/// Supports a module for emit and source code generation helpers.
/// </summary>
public interface IDynamicAssembly
{
    private static class PurelyGenerated { }

    /// <summary>
    /// Gets a fake type that can be used to denote a purely generated type
    /// that has no dynamically emitted counterpart in a dynamic assembly.
    /// </summary>
    static readonly Type PurelyGeneratedType = typeof( PurelyGenerated );
    
    /// <summary>
    /// Provides a new unique number that can be used for generating unique names inside this dynamic assembly.
    /// </summary>
    /// <returns>A unique number.</returns>
    string NextUniqueNumber();

    /// <summary>
    /// Gets a shared dictionary associated to this dynamic assembly. 
    /// Methods that generate code can rely on this to store shared information as required by their generation process.
    /// If information has to be shared among different <see cref="IGeneratedBinPath"/> contexts, then the <see cref="IGeneratedBinPath.Memory"/>
    /// must be used.
    /// </summary>
    Dictionary<object, object?> Memory { get; }

    /// <summary>
    /// Gets the <see cref="ModuleBuilder"/> to use to emit stub types and other
    /// dynamic code.
    /// </summary>
    ModuleBuilder StubModuleBuilder { get; }

    /// <summary>
    /// Gets the <see cref="ICodeWorkspace"/> for this <see cref="IDynamicAssembly"/> into which
    /// source code should be generated.
    /// </summary>
    ICodeWorkspace Code { get; }
}
