namespace CK.Engine.TypeCollector.Tests;

public class SomeSecondaryMemberAttributeImpl : SecondaryMemberAttributeImpl<SomeSecondaryMemberAttribute>, IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;
}
