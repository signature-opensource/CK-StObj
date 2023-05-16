using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Describes a <see cref="EndpointDefinition"/>.
    /// </summary>
    public sealed class EndpointContext
    {
        readonly IStObjResult _endpointDefinition;
        readonly string _name;
        internal readonly List<(Type Service, EndpointContext? Owner)> _singletons;
        internal readonly List<Type> _scoped;

        internal EndpointContext( IStObjResult endpointDefinition )
        {
            Debug.Assert( endpointDefinition.ClassType.Name.EndsWith( "EndpointDefinition" ) && "EndpointDefinition".Length == 18 );
            string name = endpointDefinition.ClassType.Name;
            _name = name.Substring( 0, name.Length - 18 );
            _endpointDefinition = endpointDefinition;
            _singletons = new List<(Type Service, EndpointContext? Owner)>();
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
        /// Gets the singletons service type exposed by this service mapped to their owner.
        /// When the owner is null, this context is in charge of creating the service.
        /// </summary>
        public IReadOnlyList<(Type Service, EndpointContext? Owner)> SingletonServices => _singletons;

        /// <summary>
        /// Gets the scoped service type exposed by this service.
        /// </summary>
        public IReadOnlyList<Type> ScopedServices => _scoped;
    }
}
