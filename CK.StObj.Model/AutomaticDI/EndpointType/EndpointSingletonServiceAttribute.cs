using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a singleton endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>. The service can be:
    /// <list type="bullet">
    /// <item>Created and owned by the endpoint itself.</item>
    /// <item>Or bound to an instance owned by another <see cref="Owner"/>.</item>
    /// </list>
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public sealed class EndpointSingletonServiceAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceAttribute"/> that declares the ownership of the
        /// decorated type.
        /// </summary>
        /// <param name="ownerEndpointDefinition">The endpoint type that creates, owns and exposes the decorated service.</param>
        /// <param name="exclusive">
        /// True to forbid other endpoint to expose the decorated singleton service, false to allow
        /// the service instance to be exposed by other types of endpoint.
        /// </param>
        public EndpointSingletonServiceAttribute( Type ownerEndpointDefinition, bool exclusive )
        {
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( ownerEndpointDefinition ) );
            EndpointDefinition = ownerEndpointDefinition;
            Exclusive = exclusive;
        }

        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceAttribute"/> that declares the exposure of the
        /// decorated service from another endpoint.
        /// </summary>
        /// <param name="endpointDefinition">The endpoint type that exposes the decorated service.</param>
        /// <param name="ownerEndpointDefinition">The endpoint type that owns the service instance.</param>
        public EndpointSingletonServiceAttribute( Type endpointDefinition, Type ownerEndpointDefinition )
        {
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( ownerEndpointDefinition ) );
            EndpointDefinition = endpointDefinition;
            Owner = ownerEndpointDefinition;
        }

        /// <summary>
        /// Gets the endpoint type where the service is exposed.
        /// </summary>
        public Type EndpointDefinition { get; }

        /// <summary>
        /// Gets the owner endpoint type if any.
        /// </summary>
        public Type? Owner { get; }

        /// <summary>
        /// Gets whether the decorated service is exclusively exposed by the <see cref="EndpointDefinition"/> or can
        /// also be exposed by other endpoint types. This is always false if there is a <see cref="Owner"/>.
        /// </summary>
        public bool Exclusive { get; }
    }
}
