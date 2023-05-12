using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a endpoint service that is available on
    /// the specified <see cref="EndpointType"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointServiceTypeAvailabilityAttribute : Attribute
    {
        /// <summary>
        /// Initialize a new <see cref="EndpointServiceTypeAvailabilityAttribute"/>.
        /// </summary>
        /// <param name="serviceType">The type of the service that is a endpoint service.</param>
        /// <param name="endpointType">The type that must be a <see cref="EndpointType"/>.</param>
        public EndpointServiceTypeAvailabilityAttribute( Type serviceType, Type endpointType )
        {
            Throw.CheckNotNullArgument( serviceType );
            if( !typeof( EndpointType ).IsAssignableFrom( endpointType ) )
            {
                Throw.ArgumentException( nameof( endpointType ), $"The type '{endpointType.ToCSharpName()}' must be a EndpointType." );
            }
            ServiceType = serviceType;
            EndpointType = endpointType;
        }

        /// <summary>
        /// Gets the endpoint service type.
        /// </summary>
        public Type ServiceType { get; }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointType { get; }
    }

}
