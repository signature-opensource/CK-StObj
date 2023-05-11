using System;

namespace CK.Core
{
    /// <summary>
    /// States that the <see cref="ServiceType"/> is a endpoint singleton service that is created by
    /// the specified <see cref="EndpointType"/>.
    /// This obviously implies that the service is available in the context of the EndpointType.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointSingletonServiceTypeOwnerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceOwnerAttribute"/>.
        /// </summary>
        /// <param name="serviceType">Type of the service.</param>
        /// <param name="endpointType">The endpoint type that creates the service.</param>
        /// <param name="exclusiveEndpoint">
        /// True to forbid other endpoint to expose the singleton service, false to allow
        /// the service to be exposed by other types of endpoint.
        /// </param>
        public EndpointSingletonServiceTypeOwnerAttribute( Type serviceType, Type endpointType, bool exclusiveEndpoint )
        {
            if( !typeof( EndpointType ).IsAssignableFrom( endpointType ) )
            {
                Throw.ArgumentException( nameof( endpointType ), $"The type '{endpointType.ToCSharpName()}' must be a EndpointType." );
            }
            ServiceType = serviceType;
            EndpointType = endpointType;
            ExclusiveEndpoint = exclusiveEndpoint;
        }

        /// <summary>
        /// Gets the service type.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointType { get; }

        /// <summary>
        /// Gets whether the <see cref="ServiceType"/> is exclusively exposed by the <see cref="EndpointType"/> or can
        /// also be exposed by other endpoint types.
        /// </summary>
        public bool ExclusiveEndpoint { get; }
    }
}
