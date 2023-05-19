using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Describes a <see cref="EndpointDefinition"/>.
    /// </summary>
    public sealed class EndpointContext : IEndpointContext
    {
        readonly IStObjResult _endpointDefinition;
        readonly string _name;
        internal readonly List<(Type Service, IEndpointContext? Owner)> _singletons;
        internal readonly List<Type> _scoped;

        internal EndpointContext( IStObjResult endpointDefinition )
        {
            Debug.Assert( endpointDefinition.ClassType.Name.EndsWith( "EndpointDefinition" ) && "EndpointDefinition".Length == 18 );
            string name = endpointDefinition.ClassType.Name;
            _name = name.Substring( 0, name.Length - 18 );
            _endpointDefinition = endpointDefinition;
            _singletons = new List<(Type Service, IEndpointContext? Owner)>();
            _scoped = new List<Type>();
        }

        /// <summary>
        /// Gets the endpoint definition.
        /// </summary>
        public IStObjResult EndpointDefinition => _endpointDefinition;

        /// <summary>
        /// Gets the endpoint name (this is the endpoint definition type name without "EndpointDefinition" suffix):
        /// "Default" for <see cref="DefaultEndpointDefinition"/>.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the singletons service types exposed by this service mapped to their owner.
        /// When the owner is null, it is this context that is in charge of the service.
        /// </summary>
        public IReadOnlyList<(Type Service, IEndpointContext? Owner)> SingletonServices => _singletons;

        /// <summary>
        /// Gets the scoped service types exposed by this service.
        /// </summary>
        public IReadOnlyList<Type> ScopedServices => _scoped;
    }
}
