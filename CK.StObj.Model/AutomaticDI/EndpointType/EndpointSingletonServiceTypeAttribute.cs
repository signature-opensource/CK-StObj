using System;

namespace CK.Core
{
    /// <summary>
    /// States that the <see cref="ServiceType"/> is a singleton endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>. The service can be:
    /// <list type="bullet">
    /// <item>Created and owned by the endpoint itself.</item>
    /// <item>Or bound to an instance owned by another <see cref="Owner"/>.</item>
    /// </list>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointSingletonServiceTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceTypeAttribute"/> that declares the ownership of a service.
        /// </summary>
        /// <param name="serviceType">The service type to expose.</param>
        /// <param name="endpointDefinition">
        /// The endpoint type that exposes the <see cref="ServiceType"/>. If <paramref name="ownerEndpointDefinition"/> is null,
        /// the endpoint creates and owns the service.
        /// </param>
        /// <param name="ownerEndpointDefinition">
        /// Optional other endpoint definition type from which the service will be retrieved.
        /// </param>
        public EndpointSingletonServiceTypeAttribute( Type serviceType, Type endpointDefinition, Type? ownerEndpointDefinition = null )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            Throw.CheckArgument( ownerEndpointDefinition == null || typeof( EndpointDefinition ).IsAssignableFrom( ownerEndpointDefinition ) );
            ServiceType = serviceType;
            EndpointDefinition = endpointDefinition;
        }

        /// <summary>
        /// Gets the service type.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the endpoint type where the service is exposed.
        /// </summary>
        public Type EndpointDefinition { get; }

        /// <summary>
        /// Gets the owner endpoint type if any.
        /// </summary>
        public Type? Owner { get; }
    }
}
