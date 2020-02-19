using System;

namespace CK.Core
{
    /// <summary>
    /// Poco factory.
    /// These interfaces are automatically implemented.
    /// </summary>
    public interface IPocoFactory<out T> : IRealObject where T : IPoco
    {
        /// <summary>
        /// Creates a new Poco instance.
        /// </summary>
        /// <returns>A new poco instance.</returns>
        T Create();

        /// <summary>
        /// Gets the type of the final, unified, poco.
        /// </summary>
        Type PocoClassType { get; }
    }
}
