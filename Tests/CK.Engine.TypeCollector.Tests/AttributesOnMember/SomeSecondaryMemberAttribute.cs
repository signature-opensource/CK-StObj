using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public class SomeSecondaryMemberAttribute : SecondaryMemberAttribute<SomePrimaryMemberAttribute>
{
    public SomeSecondaryMemberAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomeSecondaryMemberAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}
