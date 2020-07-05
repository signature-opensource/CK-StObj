namespace CK.Core
{
    /// <summary>
    /// Interface for Poco class implementation.
    /// All poco final implementation implement this interface that gives access to its <see cref="IPocoFactory"/>.
    /// </summary>
    public interface IPocoClass
    {
        /// <summary>
        /// Gets the poco factory.
        /// </summary>
        IPocoFactory Factory { get; }
    }
}
