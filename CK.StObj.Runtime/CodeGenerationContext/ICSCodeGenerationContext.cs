using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Global context that is provided to <see cref="ICSCodeGeneratorType"/>, <see cref="IAutoImplementorMethod"/> and <see cref="IAutoImplementorProperty"/>
    /// implement methods.
    /// This context is bound to one <see cref="IGeneratedBinPath"/> (the <see cref="ICodeGenerationContext.CurrentRun"/>) that groups 0 or more
    /// equivalent <see cref="BinPathConfiguration"/>.
    /// </summary>
    public interface ICSCodeGenerationContext : ICodeGenerationContext
    {
        /// <summary>
        /// Gets the <see cref="IDynamicAssembly"/> to use to generate code of the <see cref="ICodeGenerationContext.CurrentRun"/>.
        /// </summary>
        IDynamicAssembly Assembly { get; }

        /// <summary>
        /// Gets whether the source code must eventually be saved.
        /// See <see cref="CompileOption"/>.
        /// </summary>
        bool SaveSource { get; }

        /// <summary>
        /// Gets whether the generated source code must be parsed and or compiled.
        /// </summary>
        CompileOption CompileOption { get; }

        /// <summary>
        /// Gets whether really generating source code is useless since <see cref="SaveSource"/> is false
        /// and <see cref="CompileOption"/> is <see cref="CompileOption.None"/>.
        /// <para>
        /// This does not mean that the whole code generation process should be skipped: when generating code,
        /// side effects (from a bin path) are possible that may be useful to others.
        /// </para>
        /// <para>
        /// This applies to the <see cref="ICodeGenerationContext.IsPrimaryRun"/> if the unified bin path doesn't correspond to any of the
        /// existing <see cref="BinPathConfiguration"/> (<see cref="IStObjEngineRunContext.IsUnifiedPure"/>).
        /// </para>
        /// </summary>
        bool ActualSourceCodeIsUseless => SaveSource == false && CompileOption == CompileOption.None;
    }
}
