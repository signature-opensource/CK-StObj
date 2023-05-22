using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a scoped endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false )]
    public sealed class EndpointScopedServiceAttribute : Attribute
    {
        /// <summary>
        /// Initialize a new <see cref="EndpointScopedServiceAttribute"/>.
        /// </summary>
        /// <param name="endpointDefinition">The type that must be a <see cref="EndpointDefinition"/>.</param>
        public EndpointScopedServiceAttribute( Type endpointDefinition )
        {
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            EndpointDefinition = endpointDefinition;
        }

        /// <summary>
        /// Gets the endpoint type.
        /// </summary>
        public Type EndpointDefinition { get; }
    }
}
