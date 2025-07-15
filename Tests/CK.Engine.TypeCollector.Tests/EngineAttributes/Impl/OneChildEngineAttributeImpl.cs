namespace CK.Engine.TypeCollector.Tests;

public class OneChildEngineAttributeImpl : EngineAttributeImpl<OneChildEngineAttribute>,
                                           IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;
}
