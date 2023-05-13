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

            // This CK.Core.EndpointTypeManager_CK statically exposes the default and all endpoint types.
            scope.Append( "public static readonly CK.Core.DefaultEndpointDefinition Default;" ).NewLine();
            scope.Append( "public static readonly CK.Core.EndpointDefinition[] Endpoints;" ).NewLine();

            var ctor = scope.GeneratedByComment().NewLine()
                            .CreateFunction( "static EndpointTypeManager_CK()" );

            StaticConstructor( ctor, c.CurrentRun.EngineMap.StObjs );

            scope.Append( "public override DefaultEndpointDefinition DefaultEndpointDefinition => Default;" ).NewLine()
                 .Append( "public override IReadOnlyList<EndpointDefinition> AllEndpointDefinitions => Endpoints;" ).NewLine();

            return CSCodeGenerationResult.Success;
        }

        static void StaticConstructor( IFunctionScope ctor, IStObjObjectEngineMap stObjMap )
        {
            var def = stObjMap.ToLeaf( typeof( DefaultEndpointDefinition ) );
            Debug.Assert( def != null, "Systematically registered by StObjCollector.GetResult()." );

            ctor.Append( "Default = (DefaultEndpointDefinition)" ).Append( def.CodeInstanceAccessor ).Append( ";" ).NewLine();

            var endpoints = stObjMap.FinalImplementations.Where( f => typeof( EndpointDefinition ).IsAssignableFrom( f.ClassType ) ).ToList();
            ctor.Append( "Endpoints = new EndpointDefinition[" ).Append( endpoints.Count ).Append( "];" ).NewLine()
                .Append( "Endpoints[0] = Default;" ).NewLine();
            int i = 1;
            foreach( var e in stObjMap.FinalImplementations.Where( f => typeof( EndpointDefinition ).IsAssignableFrom( f.ClassType ) ) )
            {
                if( e != def )
                {
                    ctor.Append( "Endpoints[" ).Append( i++ ).Append( "] = (EndpointDefinition)" ).Append( e.CodeInstanceAccessor ).Append( ";" ).NewLine();
                }
            }
        }
    }
}
