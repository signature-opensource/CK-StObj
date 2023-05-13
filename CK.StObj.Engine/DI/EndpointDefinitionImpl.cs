using CK.CodeGen;
using System;
using CK.Core;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Implements the EndpointTypeManager.
    /// </summary>
    public sealed class EndpointDefinitionImpl : CSCodeGeneratorType, IAttributeContextBound
    {
        readonly EndpointDefinitionAttribute _attr;

        public EndpointDefinitionImpl( Type owner, EndpointDefinitionAttribute attr )
        {
            if( owner.BaseType != typeof( EndpointDefinition ) ) Throw.InvalidOperationException( $"Endpoint definition '{owner}' cannot specialize another EndpointDefinition." );
            _attr = attr;
        }

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            scope.Definition.Modifiers |= Modifiers.Sealed;
            return CSCodeGenerationResult.Success;
        }

    }
}
