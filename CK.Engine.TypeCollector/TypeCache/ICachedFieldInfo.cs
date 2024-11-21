namespace CK.Engine.TypeCollector;

public interface ICachedFieldInfo : ICachedMember
{
    ICachedType FieldType { get; }
}
