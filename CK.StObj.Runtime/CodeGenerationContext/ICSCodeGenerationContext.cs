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
    /// This context is bound to one <see cref="IGeneratedBinPath"/> (the <see cref="CurrentRun"/>) that groups 0 or more equivalent <see cref="BinPathConfiguration"/>.
    /// </summary>
    public interface ICSCodeGenerationContext : ICodeGenerationContext
    {
        /// <summary>
        /// Gets the <see cref="IDynamicAssembly"/> to use to generate code of the <see cref="CurrentRun"/>.
        /// </summary>
        IDynamicAssembly Assembly { get; }

        /// <summary>
        /// Gets whether the source code must eventually be saved.
        /// See <see cref="CompileOption"/>.
        /// </summary>
        bool SaveSource { get; }

        /// <summary>
        /// Gets whether the generated source code must be parsed and or compiled.
        /// <see cref="SaveSource"/> can be false and this can be <see cref="CompileOption.None"/>
        /// when <see cref="IsUnifiedRun"/> is true and the unified bin path doesn't correspond to any of the
        /// existing <see cref="BinPathConfiguration"/>.
        /// </summary>
        CompileOption CompileOption { get; }
    }
}
