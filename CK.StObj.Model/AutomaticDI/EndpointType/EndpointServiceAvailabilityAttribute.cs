using System;

namespace CK.Core
{

    /// <summary>
    /// States that the decorated type is a endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointServiceAvailabilityAttribute : Attribute
    {
        /// <summary>
        /// Initialize a new <see cref="EndpointServiceAvailabilityAttribute"/>.
        /// </summary>
        /// <param name="endpointDefinition">The type that must be a <see cref="EndpointDefinition"/>.</param>
        public EndpointServiceAvailabilityAttribute( Type endpointDefinition )
        {
            if( !typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) )
            {
                Throw.ArgumentException( nameof( endpointDefinition ), $"The type '{endpointDefinition.ToCSharpName()}' must be a EndpointDefinition." );
            }
            EndpointDefinition = endpointDefinition;
        }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointDefinition { get; }
    }
}
