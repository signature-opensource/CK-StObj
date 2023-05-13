using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a endpoint singleton service that is created by
    /// the specified <see cref="EndpointDefinition"/>.
    /// This obviously implies that the service is available in the context of the EndpointDefinition.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public sealed class EndpointSingletonServiceOwnerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceOwnerAttribute"/>.
        /// </summary>
        /// <param name="endpointDefinition">The endpoint type that creates the decorated service.</param>
        /// <param name="exclusiveEndpoint">
        /// True to forbid other endpoint to expose the decorated singleton service, false to allow
        /// the service to be exposed by other types of endpoint.
        /// </param>
        public EndpointSingletonServiceOwnerAttribute( Type endpointDefinition, bool exclusiveEndpoint )
        {
            if( !typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) )
            {
                Throw.ArgumentException( nameof( endpointDefinition ), $"The type '{endpointDefinition.ToCSharpName()}' must be a EndpointDefinition." );
            }
            EndpointDefinition = endpointDefinition;
            ExclusiveEndpoint = exclusiveEndpoint;
        }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointDefinition { get; }

        /// <summary>
        /// Gets whether the decorated service is exclusively exposed by the <see cref="EndpointDefinition"/> or can
        /// also be exposed by other endpoint types.
        /// </summary>
        public bool ExclusiveEndpoint { get; }
    }
}
