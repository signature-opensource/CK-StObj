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
            if( c.ActualSourceCodeIsUseless ) return CSCodeGenerationResult.Success;

            scope.Definition.Modifiers |= Modifiers.Sealed;

            var def = c.CurrentRun.EngineMap.EndpointResult.DefaultEndpointContext;

            EndpointDefinitionImpl.WriteScopedAndSingletonServices( scope, def );

            return CSCodeGenerationResult.Success;
        }
    }
}
