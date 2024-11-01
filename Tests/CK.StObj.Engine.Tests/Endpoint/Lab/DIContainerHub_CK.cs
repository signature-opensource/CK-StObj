using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static CK.Core.ActivityMonitorSimpleCollector;
using static CK.Core.DIContainerHub;
using static System.Formats.Asn1.AsnWriter;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant;

// The DIContainerHub is code generated.
sealed class DIContainerHub_CK : DIContainerHub
{
    // This is static: there is only one global container per application.
    // More precisely, there is only one service configuration per loaded StObjMap
    // (there may be multiple StObjMap loaded in an application domain).
    static IServiceProvider? _globalServices;
    internal static IServiceProvider GlobalServices => _globalServices!;
    // This is called by the code generated HostedServiceLifetimeTrigger constructor. 
    internal static void SetGlobalServices( IServiceProvider serviceProvider ) => _globalServices = serviceProvider;

    // DIContainerDefinition are IRealObject: they are static and resolved from
    // the GeneratedRootContext.GenStObj.
    static readonly DIContainerDefinition[] _containerDefinitions;
    internal static readonly ImmutableArray<AmbientServiceMapping> _ambientMappings;
    internal static Dictionary<Type, AutoServiceKind> _endpointServices;
    internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _ambientServiceEndpointDescriptors;
    internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _ambientServiceBackendDescriptors;

    internal readonly IDIContainerInternal[] _containers;

    static DIContainerHub_CK()
    {
        _endpointServices = new Dictionary<Type, AutoServiceKind>()
        {
            { typeof(IActivityMonitor), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsScoped },
            { typeof(IParallelLogger), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsScoped },
            { typeof(IExternalAuthenticationInfo), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsScoped },
            { typeof(ExternalAuthenticationInfo), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsScoped },
            { typeof(IFakeTenantInfo), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped },
            { typeof(FakeTenantInfo), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped },
            { typeof(ExternalCultureInfo), AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsScoped },
        };
        _containerDefinitions = [new FakeBackDIContainerDefinition_CK()];
        _ambientMappings = ImmutableArray.Create(
            // IFakeTenantInfo is an auto service: the inheritance chain is analyzed, they share the same index.
            new AmbientServiceMapping( typeof( IFakeTenantInfo ), 0 ),
            new AmbientServiceMapping( typeof( FakeTenantInfo ), 0 ),
            // Not an auto service: autonmous entries.
            new AmbientServiceMapping( typeof( IExternalAuthenticationInfo ), 1 ),
            new AmbientServiceMapping( typeof( ExternalAuthenticationInfo ), 2 ),
            // Single entry.
            new AmbientServiceMapping( typeof( ExternalCultureInfo ), 3 )
        );
        Func<IServiceProvider, object> back0 = sp => ScopeDataHolder.GetAmbientService( sp, 0 );
        Func<IServiceProvider, object> back1 = sp => ScopeDataHolder.GetAmbientService( sp, 1 );
        Func<IServiceProvider, object> back2 = sp => ScopeDataHolder.GetAmbientService( sp, 2 );
        Func<IServiceProvider, object> back3 = sp => ScopeDataHolder.GetAmbientService( sp, 3 );
        _ambientServiceBackendDescriptors = [
                new ServiceDescriptor( typeof( IFakeTenantInfo ), back0, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( FakeTenantInfo ), back0, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( IExternalAuthenticationInfo ), back1, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( ExternalAuthenticationInfo ), back2, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( ExternalCultureInfo ), back3, ServiceLifetime.Scoped ),
        ];
        // These declarations are only here as the defaults.
        // In practice they are overridden by the endpoint container definition ConfigureServices.
        Func<IServiceProvider, object> front0 = sp => ((IAmbientServiceDefaultProvider<FakeTenantInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof( DefaultTenantProvider ) )!).Default;
        Func<IServiceProvider, object> front1 = sp => ((IAmbientServiceDefaultProvider<ExternalAuthenticationInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof( DefaultAuthenticationInfoProvider ) )!).Default;
        Func<IServiceProvider, object> front3 = sp => ((IAmbientServiceDefaultProvider<ExternalCultureInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof( DefaultCultureProvider ) )!).Default;
        _ambientServiceEndpointDescriptors = [
                new ServiceDescriptor( typeof( IFakeTenantInfo ), front0, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( FakeTenantInfo ), front0, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( IExternalAuthenticationInfo ), front1, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( ExternalAuthenticationInfo ), front1, ServiceLifetime.Scoped ),
            new ServiceDescriptor( typeof( ExternalCultureInfo ), front3, ServiceLifetime.Scoped ),
        ];
    }

    // The instance constructor initializes the endpoint type from the definitions.
    public DIContainerHub_CK()
    {
        _containers =
        [
                new DIContainer<FakeBackDIContainerDefinition.Data>( new FakeBackDIContainerDefinition_CK() )
        ];
    }

    public override IReadOnlyList<DIContainerDefinition> ContainerDefinitions => _containerDefinitions;

    public override IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices => _endpointServices;

    public override IReadOnlyList<IDIContainer> Containers => _containers;

    public override IReadOnlyList<AmbientServiceMapping> AmbientServiceMappings => _ambientMappings;

    internal ServiceDescriptor[] CreateCommonDescriptors( IStObjMap stObjMap )
    {
        return
        [
            // This endpointTypeManager that is the relay to the global services.
            new ServiceDescriptor( typeof( DIContainerHub ), this ),
            // The StObjMap singleton.
            new ServiceDescriptor( typeof( IStObjMap ), stObjMap ),

            // The IDIContainer<TScopeData> are true singletons. (Done for each EndpoitType.)
            new ServiceDescriptor( typeof( IDIContainer<FakeBackDIContainerDefinition.Data> ), _containers[0] ),

            // ...as well as the IEnumerable<IDIContainer>.
            new ServiceDescriptor( typeof( IEnumerable<IDIContainer> ), _containers ),

            // And our fundamental scoped that holds the endpoint specific scoped Data.
            new ServiceDescriptor( typeof( ScopeDataHolder ), typeof( ScopeDataHolder ), ServiceLifetime.Scoped )
        ];
    }

}
