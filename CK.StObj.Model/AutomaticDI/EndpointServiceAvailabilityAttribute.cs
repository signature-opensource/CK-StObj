using System;

namespace CK.Core
{

    /// <summary>
    /// States that the decorated type is a endpoint service that is available on
    /// the specified <see cref="EndpointType"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointServiceAvailabilityAttribute : Attribute
    {
        /// <summary>
        /// Initialize a new <see cref="EndpointServiceAvailabilityAttribute"/>.
        /// </summary>
        /// <param name="endpointType">The type that must be a <see cref="EndpointType"/>.</param>
        public EndpointServiceAvailabilityAttribute( Type endpointType )
        {
            if( !typeof( EndpointType ).IsAssignableFrom( endpointType ) )
            {
                Throw.ArgumentException( nameof( endpointType ), $"The type '{endpointType.ToCSharpName()}' must be a EndpointType." );
            }
            EndpointType = endpointType;
        }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointType { get; }
    }
}
