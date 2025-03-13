using CK.Core;

namespace CK.StObj.Engine.Tests.CrisLike;

[CKTypeDefiner]
public interface ICommandAuthImpersonation : ICommandAuthUnsafe
{
    int ActualActorId { get; set; }
}
