using CK.CodeGen;
using System;
using CK.Core;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{

    /// <summary>
    /// Implements a EndpointDefinition instance.
    /// There is only the Name to generate here...
    /// </summary>
    public sealed class EndpointDefinitionImpl : CSCodeGeneratorType, IAttributeContextBound
    {
        public EndpointDefinitionImpl( IActivityMonitor monitor, Type decorated, EndpointDefinitionAttribute attr )
        {
            EndpointContext.CheckEndPointDefinition( monitor, decorated );
        }

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            if( c.ActualSourceCodeIsUseless ) return CSCodeGenerationResult.Success;

            scope.Definition.Modifiers |= Modifiers.Sealed;

            var endpointResult = c.CurrentRun.EngineMap.EndpointResult;
            var e = endpointResult.EndpointContexts.First( c => c.EndpointDefinition.ClassType == classType );

            scope.Append( "public override string Name => " ).AppendSourceString( e.Name ).Append( ";" ).NewLine();

            return CSCodeGenerationResult.Success;
        }
    }
}
