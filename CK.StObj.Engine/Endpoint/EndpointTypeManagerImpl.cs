using CK.CodeGen;
using System;
using CK.Core;
using System.Diagnostics;
using System.Linq;

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
            scope.Append( "internal static readonly CK.Core.DefaultEndpointDefinition _default;" ).NewLine();
            scope.Append( "internal static readonly CK.Core.EndpointDefinition[] _endpoints;" ).NewLine();
            scope.Append( "internal static readonly IReadOnlySet<Type> _endpointServices;" ).NewLine();

            var cctor = scope.GeneratedByComment().NewLine()
                            .CreateFunction( "static EndpointTypeManager_CK()" );

            StaticConstructor( cctor, c.CurrentRun.EngineMap );

            scope.Append( "public override DefaultEndpointDefinition DefaultEndpointDefinition => _default;" ).NewLine()
                 .Append( "public override IReadOnlyList<EndpointDefinition> AllEndpointDefinitions => _endpoints;" ).NewLine()
                 .Append( "public override IReadOnlySet<Type> EndpointServices => _endpointServices;" ).NewLine();

            return CSCodeGenerationResult.Success;
        }

        static void StaticConstructor( IFunctionScope cctor, IStObjEngineMap engineMap )
        {
            var def = engineMap.StObjs.ToLeaf( typeof( DefaultEndpointDefinition ) );
            Debug.Assert( def != null, "Systematically registered by StObjCollector.GetResult()." );

            cctor.Append( "_default = (DefaultEndpointDefinition)" ).Append( def.CodeInstanceAccessor ).Append( ";" ).NewLine();

            var endpoints = engineMap.StObjs.FinalImplementations.Where( f => typeof( EndpointDefinition ).IsAssignableFrom( f.ClassType ) ).ToList();
            cctor.Append( "_endpoints = new EndpointDefinition[" ).Append( endpoints.Count ).Append( "];" ).NewLine()
                .Append( "_endpoints[0] = Default;" ).NewLine();
            int i = 1;
            foreach( var e in endpoints )
            {
                if( e != def )
                {
                    cctor.Append( "_endpoints[" ).Append( i++ ).Append( "] = (EndpointDefinition)" ).Append( e.CodeInstanceAccessor ).Append( ";" ).NewLine();
                }
            }
            cctor.Append( "_endpointServices = new HashSet<Type>( " ).AppendArray( engineMap.EndpointResult.EndpointServices ).Append( " );" ).NewLine();
        }
    }
}
