using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointAvailableServiceTypeAttribute : Attribute
    {
        /// <summary>
        /// Initialize a new <see cref="EndpointAvailableServiceTypeAttribute"/>.
        /// </summary>
        /// <param name="serviceType">The type of the service that is a endpoint service.</param>
        /// <param name="endpointDefinition">The type that must be a <see cref="EndpointDefinition"/>.</param>
        public EndpointAvailableServiceTypeAttribute( Type serviceType, Type endpointDefinition )
        {
            Throw.CheckNotNullArgument( serviceType );
            if( !typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) )
            {
                Throw.ArgumentException( nameof( endpointDefinition ), $"The type '{endpointDefinition.ToCSharpName()}' must be a EndpointDefinition." );
            }
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
