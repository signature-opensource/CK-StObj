namespace CK.Engine.TypeCollector;

public interface ICachedEventInfo : ICachedMember
{
    ICachedType EventHandlerType { get; }
}
