using System;

namespace CK.Core
{
    /// <summary>
    /// States that the <see cref="ServiceType"/> is a singleton endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointSingletonServiceTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceTypeAttribute"/>.
        /// </summary>
        /// <param name="serviceType">The service type to expose.</param>
        /// <param name="endpointDefinition">The endpoint type that exposes the <see cref="ServiceType"/>.</param>
        public EndpointSingletonServiceTypeAttribute( Type serviceType, Type endpointDefinition )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
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
    }
}
