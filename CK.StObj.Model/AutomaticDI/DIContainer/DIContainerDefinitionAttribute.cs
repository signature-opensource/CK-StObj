using CK.Setup;

namespace CK.Core
{
    /// <summary>
    /// Required attribute for <see cref="DIContainerDefinition{TScopeData}"/>.
    /// This triggers code generation of endpoint definitions.
    /// </summary>
    public sealed class DIContainerDefinitionAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="DIContainerDefinitionAttribute"/>.
        /// </summary>
        /// <param name="kind">The required kind of endpoint.</param>
        public DIContainerDefinitionAttribute( DIContainerKind kind )
            : base( "CK.Setup.DIContainerDefinitionImpl, CK.StObj.Engine" )
        {
            Kind = kind;
        }

        /// <summary>
        /// Gets whether this is a <see cref="DIContainerKind.Endpoint"/> or <see cref="DIContainerKind.Background"/>
        /// endpoint.
        /// </summary>
        public DIContainerKind Kind { get; }
    }
}
