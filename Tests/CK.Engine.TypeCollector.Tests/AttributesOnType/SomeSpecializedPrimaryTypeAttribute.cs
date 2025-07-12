namespace CK.Engine.TypeCollector.Tests;

public class SomeSpecializedPrimaryTypeAttribute : SomePrimaryTypeAttribute
{
    public SomeSpecializedPrimaryTypeAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomeSpecializedPrimaryTypeAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name + "[Spec]";
    }
}
