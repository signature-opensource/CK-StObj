using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public class SomeSecondaryTypeAttribute : SecondaryTypeAttribute<SomePrimaryTypeAttribute>
{
    public SomeSecondaryTypeAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomeSecondaryTypeAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}
