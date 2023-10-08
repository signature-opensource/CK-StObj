using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    sealed class FakeBackEndpointDefinition_CK : FakeBackEndpointDefinition
    {
        public override string Name => "FakeBack";

        public override EndpointKind Kind => EndpointKind.Back;
    }

}
