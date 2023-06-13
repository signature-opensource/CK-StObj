using CK.Setup;

namespace CK.Core
{
    /// <summary>
    /// Required attribute for <see cref="EndpointDefinition{TScopeData}"/>.
    /// This triggers code generation of endpoint definitions.
    /// </summary>
    public sealed class EndpointDefinitionAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointDefinitionAttribute"/>.
        /// </summary>
        /// <param name="kind">The required kind of endpoint.</param>
        public EndpointDefinitionAttribute( EndpointKind kind )
            : base( "CK.Setup.EndpointDefinitionImpl, CK.StObj.Engine" )
        {
            Kind = kind;
        }

        /// <summary>
        /// Gets whether this is a <see cref="EndpointKind.Front"/> or <see cref="EndpointKind.Back"/>
        /// endpoint.
        /// </summary>
        public EndpointKind Kind { get; }
    }
}
