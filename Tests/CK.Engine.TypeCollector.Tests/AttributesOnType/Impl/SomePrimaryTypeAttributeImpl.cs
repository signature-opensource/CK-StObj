using CK.Core;
using Shouldly;
using System.Linq;

namespace CK.Engine.TypeCollector.Tests;

public class SomePrimaryTypeAttributeImpl : PrimaryTypeAttributeImpl<SomePrimaryTypeAttribute>, IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;

    protected override bool Initialize( IActivityMonitor monitor )
    {
        SecondaryAttributes.All( a => a is SomeSecondaryTypeAttributeImpl )
                           .ShouldBeTrue();
        SecondaryAttributes.All( a => ((SomeSecondaryTypeAttributeImpl)a).HasBeenInitialized is false )
                           .ShouldBeTrue();
        base.Initialize( monitor ).ShouldBeTrue();
        SecondaryAttributes.All( a => ((SomeSecondaryTypeAttributeImpl)a).HasBeenInitialized is true )
                           .ShouldBeTrue();

        return true;
    }
}
