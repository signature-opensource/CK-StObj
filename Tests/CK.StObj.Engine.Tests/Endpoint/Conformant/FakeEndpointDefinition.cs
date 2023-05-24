using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    sealed class FakeEndpointDefinition : EndpointDefinition<object>
    {
        public override string Name => "Fake";

        public override IReadOnlyList<Type> ScopedServices => Type.EmptyTypes;

        public override IReadOnlyList<Type> SingletonServices => Type.EmptyTypes;

        public override void ConfigureEndpointServices( IServiceCollection services )
        {
        }

        sealed class FakeStObjMap : IStObjMap
        {
            public IStObjObjectMap StObjs => throw new NotImplementedException();

            public SHA1Value GeneratedSignature => throw new NotImplementedException();

            public IStObjServiceMap Services => throw new NotImplementedException();

            public IReadOnlyList<string> Names => throw new NotImplementedException();

            public IReadOnlyCollection<VFeature> Features => throw new NotImplementedException();

            public void ConfigureEndpointServices( in StObjContextRoot.ServiceRegister serviceRegister )
            {
                throw new NotImplementedException();
            }

            public IStObjFinalClass? ToLeaf( Type t ) => null;
        }

        public static EndpointServiceProvider<object> CreateServiceProvider( ServiceCollection globalConfiguration, IServiceProvider globalProvider )
        {
            var endpointType = new EndpointType<object>( new FakeEndpointDefinition() );
            endpointType.ConfigureServices( TestHelper.Monitor, new FakeStObjMap(), globalConfiguration ).Should().BeTrue();
            endpointType.SetGlobalContainer( globalProvider );
            return endpointType.GetContainer();
        }

    }
}
