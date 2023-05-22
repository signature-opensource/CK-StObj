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
        internal readonly List<Type> _singletons;
        internal readonly List<Type> _scoped;

        internal EndpointContext( IStObjResult endpointDefinition, string name )
        {
            _name = name;
            _endpointDefinition = endpointDefinition;
            _singletons = new List<Type>();
            _scoped = new List<Type>();
        }

        public IStObjResult EndpointDefinition => _endpointDefinition;

        public string Name => _name;

        public IReadOnlyList<Type> SingletonServices => _singletons;

        public IReadOnlyList<Type> ScopedServices => _scoped;
    }
}
