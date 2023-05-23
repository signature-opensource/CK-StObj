using CK.CodeGen;
using System.Diagnostics;

namespace CK.Setup
{
    static class EndpointSourceCodeGenerator
    {
        // The whole code of the CK.StObj.GeneratedEndpointContainerBuilder is inside
        // its CK.StObj namespace declaration so that code from test can be copied as-is.
        const string _containerBuilder =
"""
namespace CK.StObj
{
    using CK.Core;
    using Microsoft.Extensions.DependencyInjection;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics;
    using System;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    static class GeneratedEndpointContainerBuilder
    {
        sealed class GlobalHook
        {
            [AllowNull]
            public IServiceProvider Global { get; set; }
        }

        enum SKind
        {
            AtLeastOneSingleton = 1,
            MultiSingleton = 2,
            AtLeastOneScoped = 4
        }

        internal static ServiceCollection CreateEndpointBaseServiceCollection( IActivityMonitor monitor,
                                                                               ServiceCollection global,
                                                                               Func<Type, bool> isEndpointService )
        {
            var kindMap = new Dictionary<Type, SKind>();
            ServiceCollection endpoint = new ServiceCollection();
            endpoint.AddSingleton( new GlobalHook() );
            foreach( var d in global )
            {
                // Skip any endpoint service.
                if( isEndpointService( d.ServiceType ) ) continue;

                var t = d.ServiceType;
                if( d.Lifetime == ServiceLifetime.Singleton )
                {
                    // If it's a singleton, we must only add the relay to the Global once.
                    if( kindMap.TryGetValue( t, out var kind ) )
                    {
                        // If multiple registrations exist, memorize it.
                        kindMap[t] = kind | SKind.MultiSingleton;
                    }
                    else
                    {
                        // Configure the relay to the last registered singleton.
                        endpoint.AddSingleton( t, sp => sp.GetRequiredService<GlobalHook>().Global.GetService( t )! );
                        kindMap.Add( t, SKind.AtLeastOneSingleton );
                    }
                }
                else
                {
                    // For scope, this is simple: we reuse the service descriptor instance.
                    endpoint.Add( d );
                    // But we flag this type as (at least) one scoped. If this type
                    // is also a singleton we'll have to handle its IEnumerable<T> registration
                    // as a scoped one instead of a singleton one.
                    if( kindMap.TryGetValue( t, out var kind ) )
                    {
                        kindMap[t] = kind | SKind.AtLeastOneScoped;
                    }
                    else
                    {
                        kindMap.Add( t, SKind.AtLeastOneScoped );
                    }
                }
            }
            foreach( var (type, kind) in kindMap )
            {
                if( kind == (SKind.AtLeastOneSingleton | SKind.MultiSingleton) )
                {
                    // If the type is registered as multiple singletons, we register
                    // the resolution of its IEnumerable<T> through the hook otherwise
                    // we'll have a enumeration of n times the last singleton registration.
                    var eType = typeof( IEnumerable<> ).MakeGenericType( type );
                    endpoint.AddSingleton( eType, sp => sp.GetRequiredService<GlobalHook>().Global.GetService( eType )! );
                }
                else if( (kind & ~SKind.MultiSingleton) == (SKind.AtLeastOneSingleton | SKind.AtLeastOneScoped) )
                {
                    // If the type is registered as a mix of scoped and singletons, this is the worst case.
                    // We cannot use the IEnumearble<T> resolution from the global hook since this
                    // would register scoped services in the root provider.
                    // We have no other choice than to fully resolve this here :(.
                    // This forces us to mimic the ServiceType to ImplementationType resolution of
                    // the Microsoft ServiceProvider implementation.
                    //
                    // This should barely happen, so, currently, we choose to not impact the regular case
                    // with a pre-registration of such hybrid cases: we process the collection again and
                    // emit a warning. If it happens that too many warnings occur in practice, this can be
                    // rewritten by storing the descriptor list in the kindMap (that would not be a "kind"
                    // map anymore but a reversed map of registrations).
                    using( monitor.OpenWarn( $"Type '{type:C}' is registered more than once with Scoped and Singleton lifetime. " +
                                             $"Handling its 'IEnumerable<{type:C}>' registration is unfortunately required." ) )
                    {
                        RegisterResolvedEnumerable( monitor, global, endpoint, type );
                    }
                }
                // Else:
                //  - if the type is registered only with AtLeastOneSingleton, we have nothing to do, the
                //    enumerable will be resolved by whatever container with the single last registration.
                //  - if the type is registered as scoped only, we let the container do its job as usual.
            }
            return endpoint;

            static void RegisterResolvedEnumerable( IActivityMonitor monitor, ServiceCollection global, ServiceCollection endpoint, Type type )
            {
                string exceptionMessage = $"This hybrid Scoped/Singleton 'IEnumerable<{type.ToCSharpName()}>' from endpoint containers is invalid.";
                string warnMessage = $"{exceptionMessage} Using it will throw an InvalidOperationException.";

                // To avoid keeping the ServiceDecriptor in the closure and avoid resolving the implementation
                // type each time, we compute 2 arrays of implementation types, one for the singletons (to be
                // resolved by the global hook) and one for the scoped services.
                var singTypes = new List<Type>();
                var scopedTypes = new List<Type>();
                bool hasError = false;
                foreach( var d in global )
                {
                    if( d.ServiceType == type )
                    {
                        var implType = GetImplementationType( d );
                        if( implType == type )
                        {
                            hasError = true;
                            monitor.Warn( $"Unable to analyze a mapped type. {warnMessage}" );
                        }
                        else if( d.Lifetime == ServiceLifetime.Singleton )
                        {
                            if( singTypes.Contains( implType ) )
                            {
                                hasError = true;
                                monitor.Warn( $"Duplicate mapping to '{implType:C}' (singleton). {warnMessage}" );
                            }
                            else
                            {
                                monitor.Info( $"=> '{implType:C}' (Singleton)" );
                                singTypes.Add( implType );
                            }
                        }
                        else
                        {
                            if( scopedTypes.Contains( implType ) )
                            {
                                hasError = true;
                                monitor.Warn( $"Duplicate mapping to '{implType:C}' (scoped). {warnMessage}" );
                            }
                            else
                            {
                                monitor.Info( $"=> '{implType:C}' (Scoped)" );
                                scopedTypes.Add( implType );
                            }
                        }
                    }
                }
                var aSing = singTypes.ToArray();
                var aScop = scopedTypes.ToArray();

                // Register the IEnumerable<> with its resolution.
                var eType = typeof( IEnumerable<> ).MakeGenericType( type );
                if( hasError )
                {
                    endpoint.AddScoped( eType, sp => Throw.InvalidOperationException<object>( exceptionMessage ) );
                }
                else
                {
                    endpoint.AddScoped( eType, sp =>
                    {
                        var a = Array.CreateInstance( type, aSing.Length + aScop.Length );
                        int i = 0;
                        var g = sp.GetRequiredService<GlobalHook>().Global;
                        foreach( var t in aSing )
                        {
                            a.SetValue( g.GetService( t ), i++ );
                        }
                        foreach( var t in aScop )
                        {
                            a.SetValue( sp.GetService( t ), i++ );
                        }
                        return a;
                    } );
                }
            }

            static Type GetImplementationType( ServiceDescriptor d )
            {
                if( d.ImplementationType != null )
                {
                    return d.ImplementationType;
                }
                else if( d.ImplementationInstance != null )
                {
                    return d.ImplementationInstance.GetType();
                }
                Debug.Assert( d.ImplementationFactory != null );
                Type[]? typeArguments = d.ImplementationFactory.GetType().GenericTypeArguments;
                Debug.Assert( typeArguments.Length == 2 );
                return typeArguments[1];
            }
        }

        internal static ServiceProvider BuildEndpointServiceProvider( ServiceCollection endpoint, ServiceProvider global )
        {
            var e = endpoint.BuildServiceProvider();
            e.GetRequiredService<GlobalHook>().Global = global;
            return e;
        }

    }
}

""";

        internal static void GenerateEndpointCode( ITypeScope rootType, IEndpointResult endpointResult )
        {
            Debug.Assert( endpointResult.EndpointContexts.Count > 1 );
            // Adds the CK.StObj.GeneratedEndpointContainerBuilder static helper.
            rootType.Workspace.Global.Append( _containerBuilder );
            rootType.Append( "public void ConfigureEndpointServices( in StObjContextRoot.ServiceRegister reg )" )
                    .OpenBlock()
                    .Append( "(reg.Services as ServiceCollection)?.MakeReadOnly();" ).NewLine()
                    .Append( "var common = GeneratedEndpointContainerBuilder.CreateEndpointBaseServiceCollection( reg.Monitor, reg.Services, CK.Core.EndpointTypeManager_CK._endpointServices.Contains );" ).NewLine()
                    .Append( "foreach( var e in CK.Core.EndpointTypeManager_CK._endpoints.Skip(1) )" )
                    .OpenBlock()
                    .CloseBlock()
                    .CloseBlock();
        }
    }
}
