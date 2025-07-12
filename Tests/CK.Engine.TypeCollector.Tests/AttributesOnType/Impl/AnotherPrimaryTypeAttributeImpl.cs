namespace CK.Engine.TypeCollector.Tests;

public class AnotherPrimaryTypeAttributeImpl : PrimaryTypeAttributeImpl<AnotherPrimaryTypeAttribute>, IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;
}

