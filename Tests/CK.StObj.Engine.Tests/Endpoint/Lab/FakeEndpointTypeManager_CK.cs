using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using static CK.StObj.Engine.Tests.Service.TypeCollector.CKTypeKindDetectorTests;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    // The EndpointTypeManager is code generated.
    sealed class FakeEndpointTypeManager_CK : EndpointTypeManager
    {
        // EndpointDefinition are IRealObject: they are static and resolved from
        // the GeneratedRootContext.GenStObj.
        static readonly EndpointDefinition[] _endpoints;
        internal static Dictionary<Type,AutoServiceKind> _endpointServices;
        internal readonly IEndpointTypeInternal[] _endpointTypes;
        // To avoid polluting the EndpointDefinition with another public generated method,
        // we hide the 

        static FakeEndpointTypeManager_CK()
        {
            _endpointServices = new Dictionary<Type, AutoServiceKind>();
            _endpoints = new EndpointDefinition[] { new FakeEndpointDefinition(), new Fake2EndpointDefinition() };
        }

        // The instance constructor initializes the endpoint type from the definitions.
        public FakeEndpointTypeManager_CK()
        {
            _endpointTypes = new IEndpointTypeInternal[]
            {
                    new EndpointType<FakeEndpointDefinition.Data>( new FakeEndpointDefinition() ),
                    new EndpointType<Fake2EndpointDefinition.Data>( new Fake2EndpointDefinition() ),
            };
        }

        public override IReadOnlyList<EndpointDefinition> EndpointDefinitions => _endpoints;

        public override IReadOnlyDictionary<Type,AutoServiceKind> EndpointServices => _endpointServices;

        public override IReadOnlyList<IEndpointType> EndpointTypes => _endpointTypes;

        // Creates a EndpointUbiquitousInfo initial content with the instances from the provided
        // container.
        protected override object GetInitialEndpointUbiquitousInfo( IServiceProvider services )
        {
            return new Dictionary<Type, object>
            {
                { typeof(IFakeAuthenticationInfo), (IFakeAuthenticationInfo)Required( services, typeof(IFakeAuthenticationInfo) ) },
                { typeof(IFakeTenantInfo), (IFakeTenantInfo)Required( services, typeof(IFakeTenantInfo) ) },
                { typeof(FakeCultureInfo), (FakeCultureInfo)Required(services, typeof(FakeCultureInfo) ) },
            };

            static object Required( IServiceProvider services, Type type )
            {
                var o = services.GetService( type );
                if( o != null ) return o;
                return Throw.InvalidOperationException<object>( $"Ubiquitous service '{type}' not registered! This type must always be resolvable." );
            }
        }

        internal ServiceDescriptor[] CreateTrueSingletons( IStObjMap stObjMap )
        {
            return new ServiceDescriptor[]
            {
                // This endpointTypeManager that is the relay to the global services.
                new ServiceDescriptor( typeof( EndpointTypeManager ), this ),
                // The StObjMap singleton.
                new ServiceDescriptor( typeof( IStObjMap ), stObjMap ),

                // The IEndpointType<TScopeData> are true singletons. (Done for each EndpoitType.)
                new ServiceDescriptor( typeof( IEndpointType<FakeEndpointDefinition.Data> ), _endpointTypes[0] ),
                new ServiceDescriptor( typeof( IEndpointType<Fake2EndpointDefinition.Data> ), _endpointTypes[1] ),

                // ...as well as the IEnumerable<IEndpointType>.
                new ServiceDescriptor( typeof( IEnumerable<IEndpointType> ), _endpointTypes )
            };
        }

        // This is called by the code generated HostedServiceLifetimeTrigger constructor. 
        internal void SetGlobalContainer( IServiceProvider serviceProvider ) => _global = serviceProvider;
    }
}
