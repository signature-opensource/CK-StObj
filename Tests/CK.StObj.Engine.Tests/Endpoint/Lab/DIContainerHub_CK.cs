using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static CK.Core.ActivityMonitorSimpleCollector;
using static CK.Core.DIContainerHub;
using static System.Formats.Asn1.AsnWriter;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
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
        internal static Dictionary<Type,AutoServiceKind> _endpointServices;
        internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _ambientServiceEndpointDescriptors;
        internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _ambientServiceBackendDescriptors;

        internal readonly IDIContainerInternal[] _containers;

        static DIContainerHub_CK()
        {
            _endpointServices = new Dictionary<Type, AutoServiceKind>()
            {
                { typeof(IActivityMonitor), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(IParallelLogger), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(IFakeAuthenticationInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(FakeAuthenticationInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(IFakeTenantInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped },
                { typeof(FakeTenantInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped },
                { typeof(FakeCultureInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
            };
            _containerDefinitions = new DIContainerDefinition[] { new FakeBackDIContainerDefinition_CK() };
            _ambientMappings = ImmutableArray.Create(
                // Intrinsic.
                new AmbientServiceMapping( typeof( AmbientServiceHub ), 0, true ),
                // IFakeTenantInfo is an auto service: the inheritance chain is analyzed, they share the same index.
                new AmbientServiceMapping( typeof( IFakeTenantInfo ), 1, false ),
                new AmbientServiceMapping( typeof( FakeTenantInfo ), 1, false ),
                // Not an auto service: autonmous entries.
                new AmbientServiceMapping( typeof( IFakeAuthenticationInfo ), 2, false ),
                new AmbientServiceMapping( typeof( FakeAuthenticationInfo ), 3, false ),
                // Single entry.
                new AmbientServiceMapping( typeof( FakeCultureInfo ), 4, false )
            );
            Func<IServiceProvider, object> back0 = sp => ScopeDataHolder.GetAmbientService( sp, 1 );
            Func<IServiceProvider, object> back1 = sp => ScopeDataHolder.GetAmbientService( sp, 2 );
            Func<IServiceProvider, object> back2 = sp => ScopeDataHolder.GetAmbientService( sp, 3 );
            Func<IServiceProvider, object> back3 = sp => ScopeDataHolder.GetAmbientService( sp, 4 );
            _ambientServiceBackendDescriptors = new ServiceDescriptor[] {
                    new ServiceDescriptor( typeof( AmbientServiceHub ), ScopeDataHolder.GetAmbientServiceHub, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( IFakeTenantInfo), back0, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeTenantInfo), back0, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( IFakeAuthenticationInfo), back1, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeAuthenticationInfo), back2, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeCultureInfo), back3, ServiceLifetime.Scoped ),
            };
            // These declarations are only here as the defaults.
            // In practice they are overridden by the endpoint container definition ConfigureServices.
            Func<IServiceProvider, object> front0 = sp => ((IEndpointUbiquitousServiceDefault<FakeTenantInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof( DefaultTenantProvider ) )!).Default;
            Func<IServiceProvider, object> front1 = sp => ((IEndpointUbiquitousServiceDefault<FakeAuthenticationInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof( DefaultAuthenticationInfoProvider ) )!).Default;
            Func<IServiceProvider, object> front3 = sp => ((IEndpointUbiquitousServiceDefault<FakeCultureInfo>?)DIContainerHub_CK.GlobalServices.GetService( typeof( DefaultCultureProvider ) )!).Default;
            _ambientServiceEndpointDescriptors = new ServiceDescriptor[] {
                    new ServiceDescriptor( typeof( AmbientServiceHub ), sp => new AmbientServiceHub_CK( sp ), ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( IFakeTenantInfo), front0, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeTenantInfo), front0, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( IFakeAuthenticationInfo), front1, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeAuthenticationInfo), front1, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeCultureInfo),front3, ServiceLifetime.Scoped ),
            };
        }

        // The instance constructor initializes the endpoint type from the definitions.
        public DIContainerHub_CK()
        {
            _containers = new IDIContainerInternal[]
            {
                    new DIContainer<FakeBackDIContainerDefinition.Data>( new FakeBackDIContainerDefinition_CK() )
            };
        }

        public override IReadOnlyList<DIContainerDefinition> ContainerDefinitions => _containerDefinitions;

        public override IReadOnlyDictionary<Type,AutoServiceKind> EndpointServices => _endpointServices;

        public override IReadOnlyList<IDIContainer> Containers => _containers;

        public override IReadOnlyList<AmbientServiceMapping> AmbientServiceMappings => _ambientMappings;

        internal ServiceDescriptor[] CreateCommonDescriptors( IStObjMap stObjMap )
        {
            return new ServiceDescriptor[]
            {
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
            };
        }

    }
}
