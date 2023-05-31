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
        // Here we fake it with a null default and our FakeEndpointDefinition.
        static readonly DefaultEndpointDefinition _default;
        static readonly EndpointDefinition[] _endpoints;
        internal static HashSet<Type> _endpointServices;
        internal readonly IEndpointTypeInternal[] _endpointTypes;

        static FakeEndpointTypeManager_CK()
        {
            _default = null!;
            _endpointServices = new HashSet<Type>();
            _endpoints = new EndpointDefinition[] { _default, new FakeEndpointDefinition() };
        }

        // The instance constructor initializes the endpoint type from the definitions.
        // (There is no EndpointType for the DefaultEndpointDefinition.)
        public FakeEndpointTypeManager_CK()
        {
            _endpointTypes = new IEndpointTypeInternal[]
            {
                    new EndpointType<FakeEndpointDefinition.Data>( new FakeEndpointDefinition() )
            };
        }

        public override DefaultEndpointDefinition DefaultEndpointDefinition => _default;

        public override IReadOnlyList<EndpointDefinition> AllEndpointDefinitions => _endpoints;

        public override IReadOnlySet<Type> EndpointServices => _endpointServices;

        public override IReadOnlyList<IEndpointType> EndpointTypes => _endpointTypes;

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
                // ...as well as the IEnumerable<IEndpointType>.
                new ServiceDescriptor( typeof( IEnumerable<IEndpointType> ), _endpointTypes )
            };
        }

        // This is called by the code generated HostedServiceLifetimeTrigger constructor. 
        internal void SetGlobalContainer( IServiceProvider serviceProvider ) => _global = serviceProvider;
    }
}
