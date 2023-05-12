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
            scope.Append( "public static readonly CK.Core.DefaultEndpointType Default;" ).NewLine();
            scope.Append( "public static readonly CK.Core.EndpointType[] Endpoints;" ).NewLine();

            var ctor = scope.GeneratedByComment().NewLine()
                            .CreateFunction( "static EndpointTypeManager_CK()" );

            StaticConstructor( ctor, c.CurrentRun.EngineMap.StObjs );

            scope.Append( "public override DefaultEndpointType DefaultEndpointType => Default;" ).NewLine()
                 .Append( "public override IReadOnlyList<EndpointType> AllEndpointTypes => Endpoints;" ).NewLine();

            return CSCodeGenerationResult.Success;
        }

        static void StaticConstructor( IFunctionScope ctor, IStObjObjectEngineMap stObjMap )
        {
            var def = stObjMap.ToLeaf( typeof( DefaultEndpointType ) );
            Debug.Assert( def != null, "Systematically registered by StObjCollector.GetResult()." );

            ctor.Append( "Default = (DefaultEndpointType)" ).Append( def.CodeInstanceAccessor ).Append( ";" ).NewLine();

            var endpoints = stObjMap.FinalImplementations.Where( f => typeof( EndpointType ).IsAssignableFrom( f.ClassType ) ).ToList();
            ctor.Append( "Endpoints = new EndpointType[" ).Append( endpoints.Count ).Append( "];" ).NewLine()
                .Append( "Endpoints[0] = Default;" ).NewLine();
            int i = 1;
            foreach( var e in stObjMap.FinalImplementations.Where( f => typeof( EndpointType ).IsAssignableFrom( f.ClassType ) ) )
            {
                if( e != def )
                {
                    ctor.Append( "Endpoints[" ).Append( i++ ).Append( "] = (EndpointType)" ).Append( e.CodeInstanceAccessor ).Append( ";" ).NewLine();
                }
            }
        }
    }
}
