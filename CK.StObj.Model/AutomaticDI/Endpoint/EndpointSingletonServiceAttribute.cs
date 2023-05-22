using System;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated type is a singleton endpoint service that is available on
    /// the specified <see cref="EndpointDefinition"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public sealed class EndpointSingletonServiceAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointSingletonServiceAttribute"/> that declares the ownership of the
        /// decorated type.
        /// </summary>
        /// <param name="endpointDefinition">
        /// The endpoint type that exposes the decorated service.
        /// </param>
        public EndpointSingletonServiceAttribute( Type endpointDefinition )
        {
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) );
            EndpointDefinition = endpointDefinition;
        }

        /// <summary>
        /// Gets the endpoint type where the service is exposed.
        /// </summary>
        public Type EndpointDefinition { get; }

    }
}
