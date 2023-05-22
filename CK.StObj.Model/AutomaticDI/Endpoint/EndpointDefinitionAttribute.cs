using CK.Setup;

namespace CK.Core
{
    /// <summary>
    /// Required attribute for <see cref="EndpointDefinition{TInstanceData}"/>.
    /// This triggers code generation of endpoint definitions.
    /// </summary>
    public sealed class EndpointDefinitionAttribute : ContextBoundDelegationAttribute
    {
        public EndpointDefinitionAttribute()
            : base( "CK.Setup.EndpointDefinitionImpl, CK.StObj.Engine" )
        {
        }
    }
}
