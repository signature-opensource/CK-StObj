using CK.CodeGen;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using CK.Core;

namespace CK.Setup
{
    static class EndpointSourceCodeGenerator
    {
        // Always injected.
        const string _localNamespaces =
            """
            using CK.Core;
            using Microsoft.Extensions.DependencyInjection;
            using System.Collections.Generic;
            using System;
            using Microsoft.Extensions.DependencyInjection.Extensions;
            using System.Linq;
            """;

        // Always injected.
        const string _endpointTypeInternal =
            """
            interface IEndpointTypeInternal : IEndpointType
            {
                bool ConfigureServices( IActivityMonitor monitor, IStObjMap stObjMap, IServiceCollection commonEndpointContainer );
            }            
            """;

        // Injected only if there's at least one EndType.
        const string _endpointType =
            """
            sealed class EndpointType<TScopeData> : IEndpointType<TScopeData>, IEndpointTypeInternal where TScopeData : notnull
            {
                internal EndpointServiceProvider<TScopeData>? _services;
            
                readonly EndpointDefinition<TScopeData> _definition;
                internal ServiceCollection? _configuration;
                readonly object _lock;
                bool _initializationSuccess;
            
                public EndpointType( EndpointDefinition<TScopeData> definition )
                {
                    _definition = definition;
                    _lock = new object();
                }
            
                public EndpointDefinition EndpointDefinition => _definition;
            
                public Type ScopeDataType => typeof( TScopeData );
            
                public string Name => _definition.Name;
            
                public EndpointServiceProvider<TScopeData> GetContainer() => _services ?? DoCreateContainer();
            
                EndpointServiceProvider<TScopeData> DoCreateContainer()
                {
                    lock( _lock )
                    {
                        if( _services == null )
                        {
                            if( !_initializationSuccess ) Throw.InvalidOperationException( "Endpoint initialization failed. It cannot be used." );
                            _services = new EndpointServiceProvider<TScopeData>( _configuration.BuildServiceProvider() );
                            // Release the configuration now that the endpoint container is built.
                            _configuration = null;
                        }
                        return _services;
                    }
                }
            
                public bool ConfigureServices( IActivityMonitor monitor, IStObjMap stObjMap, IServiceCollection commonEndpointContainer )
                {
                    var configuration = new ServiceCollection();
                    // Calls the ConfigureEndpointServices on the empty configuration.
                    _definition.ConfigureEndpointServices( configuration );
                    // Process the registrations to detect:
                    // - extra registrations: they must not be types mapped to IRealObject or IAutoService.
                    // - missing registrations from the definition.
                    if( CheckRegistrations( monitor, configuration, _definition, stObjMap ) )
                    {
                        configuration.AddRange( commonEndpointContainer );
                        // Add the scoped data holder.
                        var scopedDataType = typeof( EndpointScopeData<TScopeData> );
                        configuration.Add( new ServiceDescriptor( scopedDataType, scopedDataType, ServiceLifetime.Scoped ) );
                        // Waiting for .Net 8.
                        // configuration.MakeReadOnly();
                        _configuration = configuration;
                        return _initializationSuccess = true;
                    }
                    return false;
            
                    static bool CheckRegistrations( IActivityMonitor monitor, ServiceCollection configuration, EndpointDefinition definition, IStObjMap stObjMap )
                    {
                        var handledSingletons = new List<Type>( definition.SingletonServices );
                        var handledScoped = new List<Type>( definition.ScopedServices );
                        List<Type>? moreSingletons = null;
                        List<Type>? moreScoped = null;
                        foreach( var d in configuration )
                        {
                            if( d.Lifetime == ServiceLifetime.Singleton )
                            {
                                if( !handledSingletons.Remove( d.ServiceType ) )
                                {
                                    moreSingletons ??= new List<Type>();
                                    moreSingletons.Add( d.ServiceType );
                                }
                            }
                            else
                            {
                                if( !handledScoped.Remove( d.ServiceType ) )
                                {
                                    moreScoped ??= new List<Type>();
                                    moreScoped.Add( d.ServiceType );
                                }
                            }
                        }
                        bool success = ErrorUnhandledServices( monitor, definition, handledSingletons, ServiceLifetime.Singleton );
                        if( !ErrorUnhandledServices( monitor, definition, handledScoped, ServiceLifetime.Scoped ) ) success = false;
                        if( !ErrorNotEndpointAutoServices( monitor, definition, stObjMap, moreSingletons, ServiceLifetime.Singleton ) ) success = false;
                        if( !ErrorNotEndpointAutoServices( monitor, definition, stObjMap, moreScoped, ServiceLifetime.Scoped ) ) success = false;
                        return success;
            
                        static bool ErrorNotEndpointAutoServices( IActivityMonitor monitor, EndpointDefinition definition, IStObjMap stObjMap, List<Type>? extra, ServiceLifetime lt )
                        {
                            bool success = true;
                            if( extra != null )
                            {
                                foreach( var s in extra )
                                {
                                    var autoMap = stObjMap.ToLeaf( s );
                                    if( autoMap != null )
                                    {
                                        monitor.Error( $"Endpoint '{definition.Name}' configures the {lt} services: '{s:C}' that is automatically mapped to '{autoMap.ClassType:C}'. This is not allowed." );
                                        success = false;
                                    }
                                    else
                                    {
                                        monitor.Warn( $"Endpoint '{definition.Name}' supports the {lt} service '{s:C}' that is not declared as a endpoint service." );
                                    }
                                }
                            }
                            return success;
                        }
            
                        static bool ErrorUnhandledServices( IActivityMonitor monitor, EndpointDefinition definition, List<Type> unhandled, ServiceLifetime lt )
                        {
                            if( unhandled.Count > 0 )
                            {
                                monitor.Error( $"Endpoint '{definition.Name}' doesn't handles the declared {lt} services: '{unhandled.Select( s => s.ToCSharpName() ).Concatenate( "', '" )}'." );
                                return false;
                            }
                            return true;
                        }
                    }
                }
            }
            """;

        // Injected only if there's at least one EndType.
        const string _endpointHelper =
            """
            static class EndpointHelper
            {
                internal static IServiceCollection CreateCommonEndpointContainer( IActivityMonitor monitor,
                                                                                  IServiceCollection global,
                                                                                  Func<Type, bool> isEndpointService,
                                                                                  Dictionary<Type, object> externalMappings )
                {
                    ServiceCollection endpoint = new ServiceCollection();
                    foreach( var d in global )
                    {
                        var t = d.ServiceType;
                        if( t == typeof( EndpointTypeManager ) ) Throw.ArgumentException( "EndpointTypeManager must not be configured." );
                        // Skip any endpoint service and IHostedService.
                        if( isEndpointService( t ) || t == typeof( Microsoft.Extensions.Hosting.IHostedService ) )
                        {
                            // There's no need to have the IHostedService multiple service in the endpoint containers.
                            continue;
                        }
                        if( d.Lifetime == ServiceLifetime.Singleton )
                        {
                            // If it's a singleton, we must add the relay to the Global only once.
                            if( !TrackMultiple( externalMappings, t, d ) )
                            {
                                // Configure the relay to the last registered singleton.
                                endpoint.AddSingleton( t, sp => sp.GetRequiredService<EndpointTypeManager>().GlobalServiceProvider.GetService( t )! );
                            }
                        }
                        else
                        {
                            // For scope, this is simple: we reuse the service descriptor instance.
                            endpoint.Add( d );
                            // And we track duplicates to handle its IEnumerable<T> registration.
                            TrackMultiple( externalMappings, t, d );
                        }
                    }
                    return endpoint;

                    static bool TrackMultiple( Dictionary<Type, object> externalMultiple, Type t, ServiceDescriptor d )
                    {
                        if( externalMultiple.TryGetValue( t, out var exists ) )
                        {
                            // If multiple registrations exist, memorize the service descriptor.
                            if( exists is List<ServiceDescriptor> l ) l.Add( d );
                            else externalMultiple[t] = new List<ServiceDescriptor>() { (ServiceDescriptor)exists, d };
                            return true;
                        }
                        externalMultiple.Add( t, d );
                        return false;
                    }
                }

                internal static Type GetImplementationType( ServiceDescriptor d )
                {
                    if( d.ImplementationType != null )
                    {
                        return d.ImplementationType;
                    }
                    else if( d.ImplementationInstance != null )
                    {
                        return d.ImplementationInstance.GetType();
                    }
                    Type[]? typeArguments = d.ImplementationFactory.GetType().GenericTypeArguments;
                    return typeArguments[1];
                }
            }
            """;

        /// <summary>
        /// Always add the IEndpointTypeInternal (The EndpointTypeManager_CK needs it) but only
        /// add CK.StObj.EndpointType<TScopeData>, and the static EndpointHelper if at least one
        /// EndpointType exists.
        /// </summary>
        /// <param name="codeWorkspace">The code workspace.</param>
        /// <param name="hasEndpoint">Whether at least one endpoint exists.</param>
        internal static void GenerateSupportCode( ICodeWorkspace codeWorkspace, bool hasEndpoint )
        {
            var g = codeWorkspace.Global;
            g.Append( "namespace CK.StObj" )
             .OpenBlock()
             .Append( _localNamespaces )
             .Append( _endpointTypeInternal );
            if( hasEndpoint )
            {
                g.Append( _endpointType )
                 .Append( _endpointHelper );
            }
            g.CloseBlock();
        }

        public static void CreateFillMultipleEndpointMappingsMethod( ITypeScope rootType, IReadOnlyDictionary<Type, IMultipleInterfaceDescriptor> multipleMappings )
        {
            // Handling external mappings AND auto service mappings at the same time is hard...
            // For each auto service mapping, if one or more external registration exist, we merge their resolution.
            // There is one serious error case to consider: when the auto service IEnumerable is singleton and an
            // external scoped registration exists, we are stuck.
            // To avoid keeping the ServiceDecriptor in the closure and avoid resolving the implementation
            // type each time, we compute 2 arrays of implementation types, one for the singletons (to be
            // resolved by the global hook) and one for the scoped services.
            // Analyzing the external mappings is done by the ExternalMultipleHelper.

            rootType.GeneratedByComment().NewLine()
                    .Append( """
                             readonly struct ExternalMultipleHelper
                             {
                                 readonly List<Type> _singTypes;
                                 readonly List<Type> _scopTypes;
                                 readonly Dictionary<Type, object> _externalMappings;

                                 public ExternalMultipleHelper( Dictionary<Type, object> externalMappings )
                                 {
                                     _externalMappings = externalMappings;
                                     _singTypes = new List<Type>();
                                     _scopTypes = new List<Type>();
                                 }

                                 /// <summary>
                                 /// Processes a service type that is a [IsMultiple] interface with auto services or real objects mappings.
                                 /// If this abstract type is also mapped externally, this returns the singletons and scoped implementation
                                 /// types that must be included in the IEnumerable mapping.
                                 /// </summary>
                                 public (Type[]? ImplSing, Type[]? ImplScop, int ExtCount, string? ExceptionMessage) ProcessMultipleMappedType( IActivityMonitor monitor, Type t, bool isScoped )
                                 {
                                     if( !_externalMappings.Remove( t, out var ext ) ) return (null, null, 0, null);
                                     _singTypes.Clear();
                                     _scopTypes.Clear();
                                     string? exceptionMessage = null;
                                     if( ext is List<ServiceDescriptor> l )
                                     {
                                         foreach( var d in l ) Handle( monitor, t, d, ref exceptionMessage );
                                     }
                                     else Handle( monitor, t, (ServiceDescriptor)ext, ref exceptionMessage );
                                     if( exceptionMessage != null ) return (null, null, 0, exceptionMessage);
                                     Type[]? implSing = null;
                                     Type[]? implScop = null;
                                     int extCount = 0;
                                     if( _singTypes.Count > 0 )
                                     {
                                         if( isScoped )
                                         {
                                             monitor.Info( $"The IEnumerable<{t:C}> of [IsMultiple] is Scoped and contains Singletons: {_singTypes.Select( t => t.ToCSharpName() ).Concatenate()}." );
                                         }
                                         implSing = _singTypes.ToArray();
                                         extCount = implSing.Length;
                                     }
                                     if( _scopTypes.Count > 0 )
                                     {
                                         if( !isScoped )
                                         {
                                             // The [IsMultiple] has been resolved as a singleton. It cannot contain a scope: this is an
                                             // error that prevent the StObjMap to be registered, we use monitor.Error to signal this fatal
                                             // error.
                                             var msg = OnError( monitor, t, ref exceptionMessage );
                                             monitor.Error( $"The IEnumerable<{t:C}> of [IsMultiple] that is Singleton contains externally defined Scoped mappings: " +
                                                             $"{_scopTypes.Select( t => t.ToCSharpName() ).Concatenate()}. {msg}" );
                                         }
                                         implScop = _scopTypes.ToArray();
                                         extCount += implScop.Length;
                                     }
                                     return (implSing, implScop, extCount, exceptionMessage);
                                 }

                                 internal (List<Type> singTypes, List<Type> scopedTypes, string? errorMessage) ProcessRemainder( IActivityMonitor monitor, Type t, object o )
                                 {
                                     _singTypes.Clear();
                                     _scopTypes.Clear();
                                     string? exceptionMessage = null;
                                     if( o is List<ServiceDescriptor> l )
                                     {
                                         foreach( var d in l ) Handle( monitor, t, d, ref exceptionMessage );
                                     }
                                     else Handle( monitor, t, (ServiceDescriptor)o, ref exceptionMessage );
                                     return (_singTypes, _scopTypes, exceptionMessage);
                                 }

                                 // This only emits warnings: the StObjMap registration is not on error but using the IEnumerable will
                                 // raise an exception.
                                 void Handle( IActivityMonitor monitor, Type type, ServiceDescriptor ext, ref string? exceptionMessage )
                                 {
                                     var implType = GetImplementationType( ext );
                                     if( implType == type )
                                     {
                                         var msg = OnError( monitor, type, ref exceptionMessage );
                                         monitor.Warn( $"Unable to analyze a mapped type. {msg}" );
                                     }
                                     else if( ext.Lifetime == ServiceLifetime.Singleton )
                                     {
                                         if( _singTypes.Contains( implType ) )
                                         {
                                             var msg = OnError( monitor, type, ref exceptionMessage );
                                             monitor.Warn( $"Duplicate mapping to '{implType:C}' (singleton). {msg}" );
                                         }
                                         else
                                         {
                                             _singTypes.Add( implType );
                                         }
                                     }
                                     else
                                     {
                                         if( _scopTypes.Contains( implType ) )
                                         {
                                             var msg = OnError( monitor, type, ref exceptionMessage );
                                             monitor.Warn( $"Duplicate mapping to '{implType:C}' (scoped). {msg}" );
                                         }
                                         else
                                         {
                                             _scopTypes.Add( implType );
                                         }
                                     }
                                 }

                                 static string OnError( IActivityMonitor monitor, Type type, ref string? exceptionMessage )
                                 {
                                     exceptionMessage ??= $"This hybrid Scoped/Singleton 'IEnumerable<{type.ToCSharpName()}>' from endpoint containers is invalid.";
                                     return $"{exceptionMessage} Using it will throw an InvalidOperationException.";
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
                                     Type[]? typeArguments = d.ImplementationFactory.GetType().GenericTypeArguments;
                                     return typeArguments[1];
                                 }

                             }
                             """ );

            var fScope = rootType.GeneratedByComment().NewLine()
                                  .CreateFunction( """
                                                   void FillMultipleEndpointMappings( IActivityMonitor monitor,
                                                                                      Microsoft.Extensions.DependencyInjection.IServiceCollection commonEndpoint,
                                                                                      Dictionary<Type,object> externalMappings )
                                                   """ );
            // Shared variables reused for each mapping.
            fScope.Append( """
                           Type[]? extSingTypes = null;
                           Type[]? extScopedTypes = null;
                           int extCount;
                           string? errorMessage = null;
                           var multiHelper = new ExternalMultipleHelper( externalMappings );                          
                           """ );
            foreach( var mapping in multipleMappings.Values )
            {
                Debug.Assert( mapping.ImplementationCount > 0 );
                fScope.Append( "(extSingTypes, extScopedTypes, extCount, errorMessage) = multiHelper.ProcessMultipleMappedType( monitor, " )
                      .AppendTypeOf( mapping.ItemType ).Append( ", " ).Append( mapping.IsScoped ).Append( " );" ).NewLine()
                      .Append( "if( errorMessage != null )" )
                      .OpenBlock()
                          .Append( "commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(" )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( ", sp => Throw.InvalidOperationException<object>( errorMessage ), Microsoft.Extensions.DependencyInjection.ServiceLifetime." )
                          .Append( mapping.IsScoped ? "Scoped" : "Singleton" ).Append( " ) );" )
                      .CloseBlock()
                      .Append( "else" )
                      .OpenBlock();
                HandleOneMapping( fScope, mapping );
                fScope.CloseBlock();
            }
            // The [IsMultiple] mappings have been handled. We now process what is left in the externalMappings: the purely
            // external mappings.
            fScope.Append( """
                           foreach( var (t, o) in externalMappings )
                           {
                               var (singTypes, scopedTypes, error) = multiHelper.ProcessRemainder( monitor, t, o );
                               int count = singTypes.Count + scopedTypes.Count;
                               if( count > 1 )
                               {
                                   var tEnum = typeof( IEnumerable<> ).MakeGenericType( t );
                                   if( scopedTypes.Count > 0 )
                                   {
                                       var scopA = scopedTypes.ToArray();
                                       var singA = singTypes.Count > 0 ? singTypes.ToArray() : null;
                                       commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( tEnum,
                                           sp =>
                                           {
                                               var a = Array.CreateInstance( t, count );
                                               int i = 0;
                                               foreach( var scop in scopA )
                                               {
                                                   a.SetValue( sp.GetService( scop ), i++ );
                                               }
                                               if( singA != null )
                                               {
                                                   var g = ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider;
                                                   foreach( var sing in singA )
                                                   {
                                                       a.SetValue( g.GetService( sing ), i++ );
                                                   }
                                               }
                                               return a;
                                           }, ServiceLifetime.Scoped ) );
                                   }
                                   else
                                   {
                                       commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( tEnum,
                                           sp => ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider.GetService( tEnum),
                                           Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton ) );
                                   }
                               }
                           }
                           """ );

            // AddExternals helper.
            fScope.Append( """
                           static void AddExternals( IServiceProvider sp, Type[] singTypes, Type[] scopTypes, Array a, int i, IServiceProvider? g )
                           {
                               if( scopTypes != null )
                               {
                                   foreach( var t in scopTypes )
                                   {
                                       a.SetValue( sp.GetService( t ), i++ );
                                   }
                               }
                               if( singTypes != null )
                               {
                                   g ??= ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider;
                                   foreach( var t in singTypes )
                                   {
                                       a.SetValue( g.GetService( t ), i++ );
                                   }
                               }
                           }                           
                           """ );

            static void HandleOneMapping( IFunctionScope fScope, IMultipleInterfaceDescriptor mapping )
            {
                if( mapping.IsScoped )
                {
                    // If all items are scoped, we can let the ServiceProvider implementation do its job (providing that we register
                    // the sp => sp.GetService( mapped ) for each mapping) or we can take control and provide an explicit registration.
                    // Since we have to do it for the hybrid case, let's always do it: this will avoid some reflection code from the
                    // ServiceProvider implementation... Moreover, because we took the road of the "external service merge", we would
                    // have the choice to let the ServiceProvider implementation do its job only if we have NO [IsMultiple] to handle 
                    // (this is handled below) but here we have no choice.

                    // Closed variables by the registration lambda.
                    fScope.Append( "var singTypes = extSingTypes;" ).NewLine()
                          .Append( "var scopTypes = extScopedTypes;" ).NewLine()
                          .Append( "var count = extCount;" ).NewLine();
                    fScope.Append( "commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( ", sp => {" ).NewLine()
                          .Append( "var a = Array.CreateInstance( " )
                          .AppendTypeOf( mapping.ItemType )
                          .Append( ", count+" ).Append( mapping.ImplementationCount ).Append( ");" ).NewLine();
                    // Handle our mapping first. 
                    int i = 0;
                    bool atLeastOneSingleton = false;
                    foreach( var impl in mapping.Implementations )
                    {
                        if( impl.IsScoped )
                        {
                            fScope.Append( "a.SetValue( sp.GetService( " ).AppendTypeOf( impl.ClassType ).Append( " ), " ).Append( i++ ).Append( " );" ).NewLine();
                        }
                        else
                        {
                            if( !atLeastOneSingleton )
                            {
                                fScope.Append( "var g = ((EndpointTypeManager)sp.GetService(typeof(EndpointTypeManager))).GlobalServiceProvider;" ).NewLine();
                                atLeastOneSingleton = true;
                            }
                            fScope.Append( "a.SetValue( g.GetService( " ).AppendTypeOf( impl.ClassType ).Append( " ), i++ )," ).NewLine();
                        }
                    }
                    // Now handle the external registration.
                    fScope.Append( "AddExternals( sp, singTypes, scopTypes, a, " )
                          .Append( mapping.ImplementationCount ).Append( ", " ).Append( atLeastOneSingleton ? "g" : null ).Append( " );" ).NewLine();
                    fScope.Append( "return a;" ).NewLine()
                          .Append( "}, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ) );" );
                }
                else
                {
                    // For singletons, we register the resolution of its IEnumerable<T> through the hook otherwise
                    // we'll have a enumeration of n times the last singleton registration.
                    fScope.Append( "commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( ", sp => ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider.GetService( " )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( " ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton ) );" ).NewLine();
                }
            }
        }

    }
}
