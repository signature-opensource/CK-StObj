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
            var e = endpointResult.EndpointContexts.First( c => c.EndpointDefinition.ClassType == classType );

            scope.Append( "public override string Name => " ).AppendSourceString( e.Name ).Append( ";" ).NewLine();

            WriteScopedAndSingletonServices( scope, e );

            return CSCodeGenerationResult.Success;
        }

        internal static void WriteScopedAndSingletonServices( ITypeScope scope, IEndpointContext endpointContext )
        {
            scope.Append( "public override IReadOnlyList<Type> ScopedServices => " ).AppendArray( endpointContext.ScopedServices ).Append( ";" ).NewLine();
            scope.Append( "public override IReadOnlyList<Type> SingletonServices => " ).AppendArray( endpointContext.SingletonServices ).Append( ";" ).NewLine();
        }
    }
}
