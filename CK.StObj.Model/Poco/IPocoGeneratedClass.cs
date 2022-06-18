namespace CK.Core
{
    /// <summary>
    /// Interface for IPoco class generated implementation.
    /// All poco final implementation implement this interface that gives access to its <see cref="IPocoFactory"/>.
    /// The implementation is explicit so that this doesn't appear in the public properties of the final Type when
    /// using reflection.
    /// </summary>
    public interface IPocoGeneratedClass
    {
        /// <summary>
        /// Gets the poco factory.
        /// </summary>
        IPocoFactory Factory { get; }
    }
}
