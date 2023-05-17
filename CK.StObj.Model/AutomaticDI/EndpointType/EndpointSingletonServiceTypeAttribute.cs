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
        /// <param name="ownerEndpointDefinition">The endpoint type that creates, owns and exposes the decorated service.</param>
        /// <param name="exclusive">
        /// True to forbid other endpoint to expose the decorated singleton service, false to allow
        /// the service instance to be exposed by other types of endpoint.
        /// </param>
        public EndpointSingletonServiceTypeAttribute( Type serviceType, Type ownerEndpointDefinition, bool exclusive )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( ownerEndpointDefinition ) );
            ServiceType = serviceType;
            EndpointDefinition = ownerEndpointDefinition;
            Exclusive = exclusive;
        }

        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceTypeAttribute"/> that declares the exposure of a
        /// service from another endpoint.
        /// </summary>
        /// <param name="serviceType">The service type to expose.</param>
        /// <param name="endpointDefinition">The endpoint type that exposes the decorated service.</param>
        /// <param name="ownerEndpointDefinition">The endpoint type that owns the service instance.</param>
        public EndpointSingletonServiceTypeAttribute( Type serviceType, Type endpointDefinition, Type ownerEndpointDefinition )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( ownerEndpointDefinition ) );
            ServiceType = serviceType;
            EndpointDefinition = endpointDefinition;
            Owner = ownerEndpointDefinition;
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

        /// <summary>
        /// Gets whether the <see cref="ServiceType"/> is exclusively exposed by the <see cref="EndpointDefinition"/> or can
        /// also be exposed by other endpoint types. This is always false if there is a <see cref="Owner"/>.
        /// </summary>
        public bool Exclusive { get; }
    }
}
