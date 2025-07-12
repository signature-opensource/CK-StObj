using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// This attribute can be specialized. Specializations can be bound to a different implementation
/// than the default one thanks to the protected constructor.
/// </summary>
public class SomePrimaryMemberAttribute : PrimaryMemberAttribute
{
    public SomePrimaryMemberAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomePrimaryMemberAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    protected SomePrimaryMemberAttribute( string actualAttributeTypeAssemblyQualifiedName, string name )
        : base( actualAttributeTypeAssemblyQualifiedName )
    {
        Name = name;
    }

    public string Name { get; init; }
}
