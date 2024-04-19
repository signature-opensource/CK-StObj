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
                new AmbientServiceMapping( typeof(IFakeTenantInfo), 0 ),
                new AmbientServiceMapping( typeof(FakeTenantInfo), 0 ),
                new AmbientServiceMapping( typeof(IFakeAuthenticationInfo), 1 ),
                new AmbientServiceMapping( typeof(FakeAuthenticationInfo), 2 ),
                new AmbientServiceMapping( typeof(FakeCultureInfo), 3 )
            );
            Func<IServiceProvider, object> back0 = sp => ScopeDataHolder.GetAmbientService( sp, 0 );
            Func<IServiceProvider, object> back1 = sp => ScopeDataHolder.GetAmbientService( sp, 1 );
            Func<IServiceProvider, object> back2 = sp => ScopeDataHolder.GetAmbientService( sp, 2 );
            Func<IServiceProvider, object> back3 = sp => ScopeDataHolder.GetAmbientService( sp, 3 );
            _ambientServiceBackendDescriptors = new ServiceDescriptor[] {
                    new ServiceDescriptor( typeof( IFakeTenantInfo), back0, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeTenantInfo), back0, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( IFakeAuthenticationInfo), back1, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeAuthenticationInfo), back2, ServiceLifetime.Scoped ),
                    new ServiceDescriptor( typeof( FakeCultureInfo), back3, ServiceLifetime.Scoped ),
            };
            Func<IServiceProvider, object> front0 = sp => ((IEndpointUbiquitousServiceDefault<FakeTenantInfo>?)EndpointHelper.GetGlobalProvider( sp ).GetService( typeof( DefaultTenantProvider ) )!).Default;
            Func<IServiceProvider, object> front1 = sp => ((IEndpointUbiquitousServiceDefault<FakeAuthenticationInfo>?)EndpointHelper.GetGlobalProvider( sp ).GetService( typeof( DefaultAuthenticationInfoProvider ) )!).Default;
            Func<IServiceProvider, object> front3 = sp => ((IEndpointUbiquitousServiceDefault<FakeCultureInfo>?)EndpointHelper.GetGlobalProvider( sp ).GetService( typeof( DefaultCultureProvider ) )!).Default;
            _ambientServiceEndpointDescriptors = new ServiceDescriptor[] {
                    new ServiceDescriptor( typeof( IFakeTenantInfo), front0, ServiceLifetime.Singleton ),
                    new ServiceDescriptor( typeof( FakeTenantInfo), front0, ServiceLifetime.Singleton ),
                    new ServiceDescriptor( typeof( IFakeAuthenticationInfo), front1, ServiceLifetime.Singleton ),
                    new ServiceDescriptor( typeof( FakeAuthenticationInfo), front1, ServiceLifetime.Singleton ),
                    new ServiceDescriptor( typeof( FakeCultureInfo),front3, ServiceLifetime.Singleton ),
            };
        }

        // The instance constructor initializes the endpoint type from the definitions.
        public DIContainerHub_CK()
        {
            _containers = new IDIContainerInternal[]
            {
                    new EndpointType<FakeBackDIContainerDefinition.Data>( new FakeBackDIContainerDefinition_CK() )
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

        // This is called by the code generated HostedServiceLifetimeTrigger constructor. 
        internal void SetGlobalContainer( IServiceProvider serviceProvider ) => _global = serviceProvider;
    }
}
