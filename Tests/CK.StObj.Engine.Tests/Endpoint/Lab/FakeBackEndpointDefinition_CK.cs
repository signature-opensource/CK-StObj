using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant;

sealed class FakeBackDIContainerDefinition_CK : FakeBackDIContainerDefinition
{
    public override string Name => "FakeBack";

    public override DIContainerKind Kind => DIContainerKind.Background;
}
