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
        readonly EndpointKind _kind;
        readonly Type? _instanceDataType;

        internal EndpointContext( IStObjResult endpointDefinition, string name, EndpointKind kind, Type? instanceDataType )
        {
            _name = name;
            _kind = kind;
            _instanceDataType = instanceDataType;
            _endpointDefinition = endpointDefinition;
        }

        /// <inheritdoc />
        public IStObjResult EndpointDefinition => _endpointDefinition;

        /// <inheritdoc />
        public string Name => _name;

        /// <inheritdoc />
        public Type? ScopeDataType => _instanceDataType;

        /// <inheritdoc />
        public EndpointKind Kind => _kind;

        internal static ReadOnlySpan<char> DefinitionName( Type definition ) => definition.Name.AsSpan( 0, definition.Name.Length - 18 );

        internal static bool CheckEndPointDefinition( IActivityMonitor monitor, Type t )
        {
            var b = t.BaseType;
            if( b == null || !b.IsGenericType || b.GetGenericTypeDefinition() != typeof( EndpointDefinition<> ) )
            {
                monitor.Error( $"EndpointDefinition type '{t:C}' must directly specialize EndpointDefinition<TScopeData> (not '{b:C}')." );
                return false;
            }
            var n = t.Name;
            if( n.Length <= 18 || !n.EndsWith( "EndpointDefinition", StringComparison.Ordinal ) )
            {
                monitor.Error( $"Invalid EndpointDefinition type '{t:C}': EndpointDefinition type name must end with \"EndpointDefinition\" (the prefix becomes the simple endpoint name)." );
                return false;
            }
            return true;
        }


    }
}
