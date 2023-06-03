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
        readonly Type? _instanceDataType;
        internal readonly List<Type> _singletons;
        internal readonly List<Type> _scoped;

        internal EndpointContext( IStObjResult endpointDefinition, string name, Type? instanceDataType )
        {
            _name = name;
            _instanceDataType = instanceDataType;
            _endpointDefinition = endpointDefinition;
            _singletons = new List<Type>();
            // IActivityMonitor and IParallelLogger are the only ubiquitous endpoint services.: every endpoint MUST support them!
            _scoped = new List<Type>() { typeof( IActivityMonitor ), typeof( IParallelLogger ) };
        }

        public IStObjResult EndpointDefinition => _endpointDefinition;

        public string Name => _name;

        public IReadOnlyList<Type> SingletonServices => _singletons;

        public IReadOnlyList<Type> ScopedServices => _scoped;

        public Type? ScopeDataType => _instanceDataType;
    }
}
