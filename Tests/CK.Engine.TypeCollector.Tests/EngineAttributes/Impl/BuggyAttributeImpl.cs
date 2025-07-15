using CK.Core;

namespace CK.Engine.TypeCollector.Tests;

public sealed class CanBeBuggyAttributeImpl : EngineAttributeImpl<CanBeBuggyAttribute>, IAttributeHasNameProperty
{
    public string TheAttributeName => Attribute.Name;

    protected override bool Initialize( IActivityMonitor monitor )
    {
        if( CanBeBuggyAttribute.ImplInitializationThrow )
        {
            Throw.CKException( "Expected: CanBeBuggyAttribute.ImplInitializationThrow is true." ); 
        }
        return CanBeBuggyAttribute.ImplInitializeFalse
                ? false
                : base.Initialize( monitor );
    }

    protected override bool OnInitialized( IActivityMonitor monitor )
    {
        if( CanBeBuggyAttribute.ImplOnInitializedThrow )
        {
            Throw.CKException( "Expected: CanBeBuggyAttribute.ImplOnInitializedThrow is true." ); 
        }
        return !CanBeBuggyAttribute.ImplOnInitializedFalse;
    }
}
