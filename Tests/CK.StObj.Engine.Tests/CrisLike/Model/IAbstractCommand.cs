using CK.Core;

namespace CK.StObj.Engine.Tests.CrisLike;

/// <summary>
/// Intermediate abstraction that tags <see cref="ICommand"/> and <see cref="ICommand{TResult}"/>.
/// This is not intended to be used directly: <see cref="ICommand"/> and <see cref="ICommand{TResult}"/> must
/// be used.
/// </summary>
[CKTypeSuperDefiner]
public interface IAbstractCommand : ICrisPoco
{
}
