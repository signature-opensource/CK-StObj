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

        public EndpointDefinitionImpl( IActivityMonitor monitor, Type decorated, EndpointDefinitionAttribute attr )
        {
            CKTypeEndpointServiceInfo.CheckEndPointDefinition( monitor, decorated );
            _attr = attr;
        }

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            if( c.ActualSourceCodeIsUseless ) return CSCodeGenerationResult.Success;

            scope.Definition.Modifiers |= Modifiers.Sealed;
            var endpointResult = c.CurrentRun.EngineMap.EndpointResult;
            var endpointContext = endpointResult.EndpointContexts.FirstOrDefault( c => c.EndpointDefinition.ClassType == classType );

            string name;
            if( endpointContext == null )
            {
                name = CKTypeEndpointServiceInfo.DefinitionName( classType ).ToString();
                monitor.Warn( $"Useless endpoint definition type '{classType:C}': no specific endpoint services are declared for it." );
            }
            else name = endpointContext.Name;

            scope.Append( "public override string Name =>" ).AppendSourceString( name ).Append( ";" ).NewLine();
            return CSCodeGenerationResult.Success;
        }

    }
}
