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

        internal EndpointContext( IStObjResult endpointDefinition, string name, Type? instanceDataType )
        {
            _name = name;
            _instanceDataType = instanceDataType;
            _endpointDefinition = endpointDefinition;
        }

        public IStObjResult EndpointDefinition => _endpointDefinition;

        public string Name => _name;

        public Type? ScopeDataType => _instanceDataType;
    }
}
