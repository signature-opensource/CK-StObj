namespace CK.Core
{
    /// <summary>
    /// Auto singleton service that implements an import protocol.
    /// This is a <see cref="IPocoDeserializer"/> with a <see cref="ProtocolName"/>.
    /// </summary>
    [IsMultiple]
    public interface IPocoImporter : IPocoDeserializer, ISingletonAutoService
    {
        /// <summary>
        /// Gets the protocol name that this importer implements.
        /// </summary>
        string ProtocolName { get; }
    }
}
