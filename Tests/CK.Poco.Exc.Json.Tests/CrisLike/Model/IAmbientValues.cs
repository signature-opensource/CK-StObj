using CK.Core;

namespace CK.CrisLike;

/// <summary>
/// Defines an extensible set of properties that are global
/// to a Client/Server context: the <see cref="IAmbientValuesCollectCommand"/> sent to
/// the endpoint returns the values.
/// </summary>
public interface IAmbientValues : IPoco
{
}
