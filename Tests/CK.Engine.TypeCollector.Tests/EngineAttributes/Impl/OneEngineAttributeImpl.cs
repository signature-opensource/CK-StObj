namespace CK.Engine.TypeCollector.Tests;

public class OneEngineAttributeImpl : EngineAttributeImpl<OneEngineAttribute,OneChildEngineAttributeImpl>,
                                      IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;
}

