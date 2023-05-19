using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures the information about endpoint services: this is a reverse index of the
    /// <see cref="CKTypeEndpointServiceInfo"/>.
    /// </summary>
    public sealed class EndpointResult : IEndpointResult
    {
        readonly IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo> _endpointServiceInfoMap;
        readonly IReadOnlyList<EndpointContext> _contexts;

        /// <inheritdoc />
        public IEndpointContext DefaultEndpointContext => _contexts[0];

        /// <inheritdoc />
        public IReadOnlyList<IEndpointContext> EndpointContexts => _contexts;

        /// <inheritdoc />
        public IEnumerable<Type> EndpointServices => _endpointServiceInfoMap.Keys;

        /// <inheritdoc />
        public bool IsEndpointService( Type type ) => _endpointServiceInfoMap.ContainsKey( type );

        EndpointResult( IReadOnlyList<EndpointContext> contexts,
                        IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo> endpointServiceInfoMap )
        {
            _contexts = contexts;
            _endpointServiceInfoMap = endpointServiceInfoMap;
        }

        internal static EndpointResult? Create( IActivityMonitor monitor,
                                                IStObjObjectEngineMap engineMap,
                                                IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo> endpointServiceInfoMap )
        {
            var defaultContext = new EndpointContext( engineMap.ToLeaf( typeof( DefaultEndpointDefinition ) )! );
            var contexts = new List<EndpointContext>() { defaultContext };
            var singletons = new Dictionary<Type, (EndpointContext Owner, bool Exclusive)>();
            bool hasError = false;
            foreach( var (type, info) in endpointServiceInfoMap )
            {
                foreach( var definition in info.Services )
                {
                    var c = FindOrCreate( monitor, engineMap, contexts, definition );
                    if( c == null )
                    {
                        hasError = true;
                        continue;
                    }
                    Debug.Assert( !c._scoped.Contains( type ), "Handled at registration time." );
                    if( info.IsScoped )
                    {
                        c._scoped.Add( type );
                    }
                    else
                    {
                        c._singletons.Add( type );
                    }
                }

            }
            return hasError ? null : new EndpointResult( contexts, endpointServiceInfoMap );

            static EndpointContext? FindOrCreate( IActivityMonitor monitor, IStObjObjectEngineMap engineMap, List<EndpointContext> contexts, Type t )
            {
                var c = contexts.FirstOrDefault( c => c.EndpointDefinition.ClassType == t );
                if( c == null )
                {
                    var r = engineMap.ToLeaf( t );
                    if( r == null )
                    {
                        monitor.Error( $"Expected EndpointDefinition type '{t}' is not registered in StObjMap." );
                        return null;
                    }
                    c = new EndpointContext( r );
                    contexts.Add( c );
                }
                return c;
            }
        }
    }
}
