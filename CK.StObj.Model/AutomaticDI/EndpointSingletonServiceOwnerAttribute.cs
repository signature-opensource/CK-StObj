using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a endpoint singleton service that is created by
    /// the specified <see cref="EndpointType"/>.
    /// This obviously implies that the service is available in the context of the EndpointType.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public sealed class EndpointSingletonServiceOwnerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceOwnerAttribute"/>.
        /// </summary>
        /// <param name="endpointType">The endpoint type that creates the decorated service.</param>
        /// <param name="exclusiveEndpoint">
        /// True to forbid other endpoint to expose the decorated singleton service, false to allow
        /// the service to be exposed by other types of endpoint.
        /// </param>
        public EndpointSingletonServiceOwnerAttribute( Type endpointType, bool exclusiveEndpoint )
        {
            if( !typeof( EndpointType ).IsAssignableFrom( endpointType ) )
            {
                Throw.ArgumentException( nameof( endpointType ), $"The type '{endpointType.ToCSharpName()}' must be a EndpointType." );
            }
            EndpointType = endpointType;
            ExclusiveEndpoint = exclusiveEndpoint;
        }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointType { get; }

        /// <summary>
        /// Gets whether the decorated service is exclusively exposed by the <see cref="EndpointType"/> or can
        /// also be exposed by other endpoint types.
        /// </summary>
        public bool ExclusiveEndpoint { get; }
    }
}
