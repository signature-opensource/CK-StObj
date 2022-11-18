namespace CK.Core
{
    [IsMultiple]
    public interface IPocoExporter : IPocoSerializer, ISingletonAutoService
    {
        string ProtocolName { get; }
    }
}
