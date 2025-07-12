using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public class AnotherSecondaryTypeAttributeImpl : SecondaryTypeAttributeImpl<AnotherSecondaryTypeAttribute>, IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;
}
