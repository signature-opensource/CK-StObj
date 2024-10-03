using CK.Core;

#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved

namespace CK.CrisLike;

/// <summary>
/// Extends <see cref="ICommandAuthNormal"/> to ensure that the authentication level is <see cref="AuthLevel.Critical"/>.
/// </summary>
[CKTypeDefiner]
public interface ICommandAuthCritical : ICommandAuthNormal
{
}
