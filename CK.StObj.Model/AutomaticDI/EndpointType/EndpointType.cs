using CK.Setup;

namespace CK.Core
{
    /// <summary>
    /// Base class for a end point.
    /// </summary>
    [CKTypeDefiner]
    [ContextBoundDelegation( "CK.Setup.EndpointDefinitionImpl, CK.StObj.Engine" )]
    public abstract class EndpointDefinition : IRealObject
    {
    }

}
