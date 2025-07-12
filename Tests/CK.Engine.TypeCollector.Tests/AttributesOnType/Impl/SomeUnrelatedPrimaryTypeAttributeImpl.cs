using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public sealed class SomeUnrelatedPrimaryTypeAttributeImpl : PrimaryTypeAttributeImpl<SomeUnrelatedPrimaryTypeAttribute>, IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;

    protected override bool Initialize( IActivityMonitor monitor )
    {
        if( SomeUnrelatedPrimaryTypeAttribute.ImplInitializationThrow )
        {
            Throw.CKException( "Expected: SomeUnrelatedPrimaryTypeAttribute.ImplInitializationThrow is true." ); 
        }
        return base.Initialize( monitor );
    }

    protected override bool OnInitialized( IActivityMonitor monitor )
    {
        if( SomeUnrelatedPrimaryTypeAttribute.ImplOnInitializedThrow )
        {
            Throw.CKException( "Expected: SomeUnrelatedPrimaryTypeAttribute.ImplOnInitializedThrow is true." ); 
        }
        return true;
    }
}
