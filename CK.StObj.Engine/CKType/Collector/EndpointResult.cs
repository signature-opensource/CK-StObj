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
    public sealed class EndpointResult
    {
        readonly IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo> _endpointServiceInfoMap;
        readonly IReadOnlyList<EndpointContext> _contexts;
        readonly IReadOnlyDictionary<Type, (EndpointContext Owner, bool Exclusive)> _singletons;

        /// <summary>
        /// Gets all the <see cref="EndpointContext"/>. The first one is the <see cref="DefaultEndpointDefinition"/>.
        /// </summary>
        public IReadOnlyList<EndpointContext> EndpointContexts => _contexts;

        /// <summary>
        /// Gets all the endpoint service types.
        /// </summary>
        public IEnumerable<Type> EndpointServices => _endpointServiceInfoMap.Keys;

        /// <summary>
        /// Gets whether a type is a endpoint service.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>True if the type is a endpoint service, false otherwise.</returns>
        public bool IsEndpointService( Type type ) => _endpointServiceInfoMap.ContainsKey( type );

        /// <summary>
        /// Gets all the singleton endpoint services mapped to their owner and endpoint and whether it is
        /// exclusive to the endpoint.
        /// </summary>
        public IReadOnlyDictionary<Type, (EndpointContext Owner, bool Exclusive)> Singletons => _singletons;

        EndpointResult( IReadOnlyList<EndpointContext> contexts,
                        IReadOnlyDictionary<Type, (EndpointContext Owner, bool Exclusive)> singletons,
                        IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo> endpointServiceInfoMap )
        {
            _contexts = contexts;
            _singletons = singletons;
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
                if( info.Owner != null )
                {
                    var c = FindOrCreate( monitor, engineMap, contexts, info.Owner );
                    if( c == null )
                    {
                        hasError = true;
                        continue;
                    }
                    Debug.Assert( (info.Kind & CKTypeKind.IsSingleton) != 0 );
                    var ownerAndExclusive = (c, info.Exclusive);
                    singletons.Add( type, ownerAndExclusive );
                    c._singletons.Add( (type, null) );
                }
                foreach( var e in info.Endpoints )
                {
                    if( e == info.Owner ) continue;
                    var c = FindOrCreate( monitor, engineMap, contexts, e );
                    if( c == null )
                    {
                        hasError = true;
                        continue;
                    }

                    if( (info.Kind & CKTypeKind.IsSingleton) != 0 )
                    {
                        if( singletons.TryGetValue( type, out var ownerAndExclusive ) && ownerAndExclusive.Exclusive )
                        {
                            monitor.Error( $"Singleton endpoint service '{type}' is exclusively owned by '{ownerAndExclusive.Owner.Name}'. It cannot be exposed by '{c.Name}'." );
                            hasError = true;
                            continue;
                        }
                        c._singletons.Add( (type, c) );
                    }
                    else
                    {
                        c._scoped.Add( type );
                    }
                }
            }
            return hasError ? null : new EndpointResult( contexts, singletons, endpointServiceInfoMap );

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
