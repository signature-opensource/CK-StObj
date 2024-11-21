using CK.Core;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Specialized <see cref="ICachedType"/> when <see cref="ICachedType.Type"/> is <see cref="IPoco"/>.
/// </summary>
public interface IPocoCachedType : ICachedType
{
}
