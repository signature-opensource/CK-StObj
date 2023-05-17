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
        /// <param name="endpointDefinition">
        /// The endpoint type that exposes the decorated service. If <paramref name="ownerEndpointDefinition"/> is null,
        /// this endpoint creates and owns the service.
        /// </param>
        /// <param name="ownerEndpointDefinition">
        /// Optional other endpoint definition type from which the exposed service will be retrieved.
        /// </param>
        public EndpointSingletonServiceAttribute( Type endpointDefinition, Type? ownerEndpointDefinition = null )
        {
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            Throw.CheckArgument( ownerEndpointDefinition == null || typeof( EndpointDefinition ).IsAssignableFrom( ownerEndpointDefinition ) );
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
    }
}
