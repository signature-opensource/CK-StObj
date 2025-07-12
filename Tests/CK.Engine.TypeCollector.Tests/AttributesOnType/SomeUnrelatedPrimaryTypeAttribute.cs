using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public sealed class SomeUnrelatedPrimaryTypeAttribute : PrimaryTypeAttribute
{
    public static bool ImplInitializationThrow;
    public static bool ImplOnInitializedThrow;

    public SomeUnrelatedPrimaryTypeAttribute( string name )
        : base( "CK.Engine.TypeCollector.Tests.SomeUnrelatedPrimaryTypeAttributeImpl, CK.Engine.TypeCollector.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}
