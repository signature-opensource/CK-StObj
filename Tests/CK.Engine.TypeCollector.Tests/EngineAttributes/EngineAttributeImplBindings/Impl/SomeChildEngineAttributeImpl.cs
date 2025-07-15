namespace CK.Engine.TypeCollector.Tests;

public class SomeChildEngineAttributeImpl : EngineAttributeImpl<SomeChildEngineAttribute>,
                                            IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;
}
