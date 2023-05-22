using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a scoped endpoint service that is
    /// available on the specified <see cref="EndpointDefinition"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointScopedServiceTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointScopedServiceTypeAttribute"/>.
        /// </summary>
        /// <param name="serviceType">The type of the service that is a scoped endpoint service.</param>
        /// <param name="endpointDefinition">The type that must be a <see cref="EndpointDefinition"/>.</param>
        public EndpointScopedServiceTypeAttribute( Type serviceType, Type endpointDefinition )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            ServiceType = serviceType;
            EndpointDefinition = endpointDefinition;
        }

        /// <summary>
        /// Gets the endpoint service type.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointDefinition { get; }
    }

}
