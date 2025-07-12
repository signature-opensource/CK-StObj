namespace CK.Engine.TypeCollector;

public interface ICachedPropertyInfo : ICachedMember
{
    ICachedType PropertyType { get; }
}
