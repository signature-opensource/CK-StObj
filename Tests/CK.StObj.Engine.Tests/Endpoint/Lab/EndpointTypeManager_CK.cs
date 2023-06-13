using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using static CK.Core.ActivityMonitorSimpleCollector;
using static CK.Core.EndpointTypeManager;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    // The EndpointTypeManager is code generated.
    sealed class EndpointTypeManager_CK : EndpointTypeManager
    {
        // EndpointDefinition are IRealObject: they are static and resolved from
        // the GeneratedRootContext.GenStObj.
        static readonly EndpointDefinition[] _endpoints;
        internal static readonly UbiquitousMapping[] _ubiquitousMappings;
        internal static Dictionary<Type,AutoServiceKind> _endpointServices;
        internal static readonly IStObjFinalClass[] _defaultUbiquitousValueProviders;

        internal readonly IEndpointTypeInternal[] _endpointTypes;

        static EndpointTypeManager_CK()
        {
            _endpointServices = new Dictionary<Type, AutoServiceKind>()
            {
                { typeof(IActivityMonitor), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(IParallelLogger), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(IFakeAuthenticationInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(FakeAuthenticationInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(IFakeTenantInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(FakeTenantInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
                { typeof(FakeCultureInfo), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped },
            };
            _endpoints = new EndpointDefinition[] { new FakeEndpointDefinition_CK() };
            _ubiquitousMappings = new UbiquitousMapping[]
            {
                new UbiquitousMapping( typeof(IFakeTenantInfo), 0 ),
                new UbiquitousMapping( typeof(FakeTenantInfo), 0 ),
                new UbiquitousMapping( typeof(IFakeAuthenticationInfo), 1 ),
                new UbiquitousMapping( typeof(FakeAuthenticationInfo), 2 ),
                new UbiquitousMapping( typeof(FakeCultureInfo), 3 )
            };
            _defaultUbiquitousValueProviders = new IStObjFinalClass[]
            {
                GeneratedRootContext.ToLeaf( typeof(DefaultTenantProvider ) )!,
                GeneratedRootContext.ToLeaf( typeof(DefaultAuthenticationInfoProvider) )!,
                GeneratedRootContext.ToLeaf( typeof(DefaultAuthenticationInfoProvider) )!,
                GeneratedRootContext.ToLeaf( typeof(DefaultCultureProvider) )!
            };
        }

        // The instance constructor initializes the endpoint type from the definitions.
        public EndpointTypeManager_CK()
        {
            _endpointTypes = new IEndpointTypeInternal[]
            {
                    new EndpointType<FakeEndpointDefinition.Data>( new FakeEndpointDefinition_CK() )
            };
        }

        public override IReadOnlyList<EndpointDefinition> EndpointDefinitions => _endpoints;

        public override IReadOnlyDictionary<Type,AutoServiceKind> EndpointServices => _endpointServices;

        public override IReadOnlyList<IEndpointType> EndpointTypes => _endpointTypes;

        public override IReadOnlyList<UbiquitousMapping> UbiquitousMappings => _ubiquitousMappings;

        public override IReadOnlyList<IStObjFinalClass> DefaultUbiquitousValueProviders => throw new NotImplementedException();

        internal ServiceDescriptor[] CreateCommonDescriptors( IStObjMap stObjMap )
        {
            return new ServiceDescriptor[]
            {
                // This endpointTypeManager that is the relay to the global services.
                new ServiceDescriptor( typeof( EndpointTypeManager ), this ),
                // The StObjMap singleton.
                new ServiceDescriptor( typeof( IStObjMap ), stObjMap ),

                // The IEndpointType<TScopeData> are true singletons. (Done for each EndpoitType.)
                new ServiceDescriptor( typeof( IEndpointType<FakeEndpointDefinition.Data> ), _endpointTypes[0] ),

                // ...as well as the IEnumerable<IEndpointType>.
                new ServiceDescriptor( typeof( IEnumerable<IEndpointType> ), _endpointTypes ),

                // And our fundamental scoped that holds the endpoint specific scoped Data.
                new ServiceDescriptor( typeof( ScopeDataHolder ), typeof( ScopeDataHolder ), ServiceLifetime.Scoped )
            };
        }

        // This is called by the code generated HostedServiceLifetimeTrigger constructor. 
        internal void SetGlobalContainer( IServiceProvider serviceProvider ) => _global = serviceProvider;
    }
}
