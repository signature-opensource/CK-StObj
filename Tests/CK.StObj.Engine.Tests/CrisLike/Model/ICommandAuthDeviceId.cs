using CK.Core;

namespace CK.StObj.Engine.Tests.CrisLike;

[CKTypeDefiner]
public interface ICommandAuthDeviceId : ICommandAuthUnsafe
{
    string? DeviceId { get; set; }
}
