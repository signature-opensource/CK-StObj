using CK.CodeGen;
using System;
using CK.Core;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements the DefaultEndpointDefinitionImpl.
    /// </summary>
    public sealed class DefaultEndpointDefinitionImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            scope.Definition.Modifiers |= Modifiers.Sealed;
            return CSCodeGenerationResult.Success;
        }
    }
}
