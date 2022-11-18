namespace CK.Core
{
    /// <summary>
    /// Standard Poco importer implementation identified by its <see cref="ProtocolName"/>.
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
