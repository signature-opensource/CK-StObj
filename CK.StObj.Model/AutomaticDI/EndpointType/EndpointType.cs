using CK.Setup;

namespace CK.Core
{
    /// <summary>
    /// Base class for a end point.
    /// </summary>
    [CKTypeDefiner]
    [ContextBoundDelegation( "CK.Setup.EndpointTypeImpl, CK.StObj.Engine" )]
    public abstract class EndpointType : IRealObject
    {
    }

}
