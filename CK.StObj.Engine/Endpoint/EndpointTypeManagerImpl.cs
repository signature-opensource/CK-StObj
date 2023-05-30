using CK.CodeGen;
using System;
using CK.Core;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using static System.Formats.Asn1.AsnWriter;

namespace CK.Setup
{
    /// <summary>
    /// Implements the EndpointTypeManager.
    /// </summary>
    public sealed class EndpointTypeManagerImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Debug.Assert( scope.FullName == "CK.Core.EndpointTypeManager_CK" );
            scope.Definition.Modifiers |= Modifiers.Sealed;

            // This CK.Core.EndpointTypeManager_CK statically exposes the default and all endpoint definitions.
            // static (Real Objects)
            scope.Append( "internal static readonly DefaultEndpointDefinition _default;" ).NewLine();
            scope.Append( "internal static readonly EndpointDefinition[] _endpoints;" ).NewLine();
            scope.Append( "internal static readonly IReadOnlySet<Type> _endpointServices;" ).NewLine();
            // instance (bound to the DI world). 
            scope.Append( "internal readonly CK.StObj.IEndpointTypeInternal[] _endpointTypes;" ).NewLine();

            var endpointResult = c.CurrentRun.EngineMap.EndpointResult;

            StaticConstructor( scope, endpointResult );
            InstanceConstructor( scope, endpointResult );

            scope.Append( "public override DefaultEndpointDefinition DefaultEndpointDefinition => _default;" ).NewLine()
                 .Append( "public override IReadOnlyList<EndpointDefinition> AllEndpointDefinitions => _endpoints;" ).NewLine()
                 .Append( "public override IReadOnlySet<Type> EndpointServices => _endpointServices;" ).NewLine()
                 .Append( "public override IReadOnlyList<IEndpointType> EndpointTypes => _endpointTypes;" ).NewLine()
                 .Append( "internal void SetGlobalContainer( IServiceProvider g ) => _global = g;" ).NewLine();
            
            return CSCodeGenerationResult.Success;
        }

        static void InstanceConstructor( ITypeScope scope, IEndpointResult endpointResult )
        {
            scope.Append( "internal EndpointTypeManager_CK()" )
                 .OpenBlock();

            scope.Append( "_endpointTypes = new CK.StObj.IEndpointTypeInternal[] {" ).NewLine();
            int i = 0;
            foreach( var e in endpointResult.EndpointContexts.Skip( 1 ) )
            {
                var instanceTypeName = e.ScopeDataType.ToCSharpName();
                scope.Append( "new CK.StObj.EndpointType<" )
                    .Append( instanceTypeName )
                    .Append( ">( (EndpointDefinition<" ).Append( instanceTypeName ).Append( ">)_endpoints[" ).Append( ++i ).Append( "] )," ).NewLine();
            }
            scope.Append( "};" ).NewLine();

            scope.CloseBlock();
        }

        static void StaticConstructor( ITypeScope scope, IEndpointResult endpointResult )
        {
            scope.Append( "static EndpointTypeManager_CK()" )
                 .OpenBlock();
            // Exposes all the endpoint service types as a set.
            scope.Append( "_endpointServices = new HashSet<Type>( " ).AppendArray( endpointResult.EndpointServices ).Append( " );" ).NewLine();

            scope.Append( "_default = (DefaultEndpointDefinition)" ).Append( endpointResult.DefaultEndpointContext.EndpointDefinition.CodeInstanceAccessor ).Append( ";" ).NewLine();
            var endpoints = endpointResult.EndpointContexts;
            scope.Append( "_endpoints = new EndpointDefinition[" ).Append( endpoints.Count ).Append( "];" ).NewLine();
            int i = 0;
            foreach( var e in endpoints )
            {
                scope.Append( "_endpoints[" ).Append( i++ ).Append( "] = (EndpointDefinition)" ).Append( e.EndpointDefinition.CodeInstanceAccessor ).Append( ";" ).NewLine();
            }
            scope.CloseBlock();
        }
    }
}
