namespace CK.Core;

/// <summary>
/// Poco factory.
/// These interfaces are automatically implemented.
/// </summary>
public interface IPocoFactory<out T> : IPocoFactory, IRealObject where T : IPoco
{
    /// <summary>
    /// Creates a new Poco instance.
    /// </summary>
    /// <returns>A new poco instance.</returns>
    new T Create();

    /// <summary>
    /// Creates a new Poco instance initialized from any other Poco. See <see cref="IPocoFactory.CreateFrom(IPoco)"/>.
    /// </summary>
    /// <param name="source">The source Poco.</param>
    /// <returns>A new poco instance.</returns>
    new T CreateFrom( IPoco source );
}
