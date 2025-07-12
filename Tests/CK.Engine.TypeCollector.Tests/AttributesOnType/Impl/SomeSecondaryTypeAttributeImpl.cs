using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public class SomeSecondaryTypeAttributeImpl : SecondaryTypeAttributeImpl<SomeSecondaryTypeAttribute>, IAttributeHasNameProperty
{
    public bool HasBeenInitialized { get; set; }

    public string TheAttributeName => Attribute.Name;

    protected override bool Initialize( IActivityMonitor monitor )
    {
        HasBeenInitialized = true;
        return true;
    }
}
