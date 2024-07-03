namespace CK.Core
{
    /// <summary>
    /// Auto singleton service that implements an export protocol.
    /// This is a <see cref="IPocoSerializer"/> with a <see cref="ProtocolName"/>.
    /// </summary>
    [IsMultiple]
    public interface IPocoExporter : IPocoSerializer, ISingletonAutoService
    {
        /// <summary>
        /// Gets the protocol name that this exporter implements.
        /// </summary>
        string ProtocolName { get; }
    }
}
