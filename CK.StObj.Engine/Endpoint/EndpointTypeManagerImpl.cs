using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
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
            // static (Real Objects)
            scope.Append( "internal static readonly EndpointDefinition[] _endpoints;" ).NewLine();
            scope.Append( "internal static readonly IReadOnlyDictionary<Type,AutoServiceKind> _endpointServices;" ).NewLine();
            // instance (bound to the DI world). 
            scope.Append( "internal readonly CK.StObj.IEndpointTypeInternal[] _endpointTypes;" ).NewLine();

            var endpointResult = c.CurrentRun.EngineMap.EndpointResult;

            StaticConstructor( scope, endpointResult );
            InstanceConstructor( scope, endpointResult );
            CreateTrueSingletons( scope, endpointResult );
            GetInitialEndpointUbiquitousInfo( scope, endpointResult );

            scope.Append( "public override IReadOnlyList<EndpointDefinition> EndpointDefinitions => _endpoints;" ).NewLine()
                 .Append( "public override IReadOnlyDictionary<Type,AutoServiceKind> EndpointServices => _endpointServices;" ).NewLine()
                 .Append( "public override IReadOnlyList<IEndpointType> EndpointTypes => _endpointTypes;" ).NewLine()
                 .Append( "internal void SetGlobalContainer( IServiceProvider g ) => _global = g;" ).NewLine();
            
            return CSCodeGenerationResult.Success;
        }

        static void StaticConstructor( ITypeScope scope, IEndpointResult endpointResult )
        {
            scope.Append( "static EndpointTypeManager_CK()" )
                 .OpenBlock();
            scope.Append( "_endpointServices = new Dictionary<Type,AutoServiceKind>() {" );
            foreach( var kv in endpointResult.EndpointServices )
            {
                scope.Append( "{" ).Append( kv.Key ).Append( "," ).Append( kv.Value ).Append( "}," ).NewLine();
            }
            scope.Append( " };" ).NewLine();

            var endpoints = endpointResult.EndpointContexts;
            scope.Append( "_endpoints = new EndpointDefinition[] {" ).NewLine();
            foreach( var e in endpoints )
            {
                scope.Append( "(EndpointDefinition)" ).Append( e.EndpointDefinition.CodeInstanceAccessor ).Append( "," ).NewLine();
            }
            scope.Append("};")
                 .CloseBlock();
        }

        static void InstanceConstructor( ITypeScope scope, IEndpointResult endpointResult )
        {
            scope.Append( "internal EndpointTypeManager_CK()" )
                 .OpenBlock();

            scope.Append( "_endpointTypes = new CK.StObj.IEndpointTypeInternal[] {" ).NewLine();
            int i = 0;
            foreach( var e in endpointResult.EndpointContexts )
            {
                // The EndpointTypeManager_CK directly and immediately instantiates the EndpointType<> objects.
                // These are singletons just like this EndpointTypeManager. They are registered
                // as singleton instances in the global container (and, as global singleton instances,
                // also in endpoint containers). The IEnumerable<IEndpointType> is also explicitly
                // registered: that is the _endpointTypes array.
                var scopeDataTypeName = e.ScopeDataType.ToGlobalTypeName();
                scope.Append( "new CK.StObj.EndpointType<" )
                    .Append( scopeDataTypeName )
                    .Append( ">( (EndpointDefinition<" ).Append( scopeDataTypeName ).Append( ">)_endpoints[" ).Append( i++ ).Append( "] )," ).NewLine();
            }
            scope.Append( "};" ).NewLine();

            scope.CloseBlock();
        }

        static void CreateTrueSingletons( ITypeScope scope, IEndpointResult endpointResult )
        {
            scope.Append( "internal Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] CreateTrueSingletons( IStObjMap stObjMap )" )
                 .OpenBlock()
                 .Append( "return new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {" ).NewLine()
                 .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( EndpointTypeManager ), this )," ).NewLine()
                 .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IStObjMap ), stObjMap )," ).NewLine()
                 .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IEnumerable<IEndpointType> ), _endpointTypes )," ).NewLine();
            int i = 0;
            foreach( var e in endpointResult.EndpointContexts )
            {
                scope.Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IEndpointType<" )
                     .Append( e.ScopeDataType.ToCSharpName() )
                     .Append( "> ), _endpointTypes[" ).Append( i++ ).Append( "] )," ).NewLine();
            }
            scope.Append( "};" )
                 .CloseBlock();
        }

        static void GetInitialEndpointUbiquitousInfo( ITypeScope scope, IEndpointResult endpointResult )
        {
            scope.Append( "protected override object GetInitialEndpointUbiquitousInfo( IServiceProvider services )" )
                 .OpenBlock()
                 .Append( "return new Dictionary<Type, object> {" ).NewLine();
            foreach( var t in endpointResult.UbiquitousInfoServices )
            {
                scope.Append( "{ " ).AppendTypeOf( t )
                     .Append( ", (" ).AppendTypeOf( t ).Append( ")Required( services, " ).AppendTypeOf( t ).Append( " ) }," ).NewLine();
            }
            scope.Append( "};").NewLine()
                 .Append( """
                          static object Required( IServiceProvider services, Type type )
                          {
                              var o = services.GetService( type );
                              if( o != null ) return o;
                              return Throw.InvalidOperationException<object>( $"Ubiquitous service '{type}' not registered! This type must always be resolvable." );
                          }
                          """ )
                 .CloseBlock();
        }

    }
}
