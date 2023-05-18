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

        /// <summary>
        /// Gets all the <see cref="EndpointContext"/>. The first one is the <see cref="DefaultEndpointDefinition"/>.
        /// </summary>
        public IReadOnlyList<IEndpointContext> EndpointContexts => _contexts;

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
                // Easy: handles the scoped services.
                if( info.IsScoped )
                {
                    foreach( var definition in info.Scoped )
                    {
                        var c = FindOrCreate( monitor, engineMap, contexts, definition );
                        if( c == null )
                        {
                            hasError = true;
                            continue;
                        }
                        Debug.Assert( !c._scoped.Contains( type ), "Handled at registration time." );
                        c._scoped.Add( type );
                    }
                }
                else
                {
                    // Singletons require a check: there may be more than
                    // one registration for the definition.
                    foreach( var (tDefinition, tOwner) in info.Singletons )
                    {
                        var definition = FindOrCreate( monitor, engineMap, contexts, tDefinition );
                        if( definition == null )
                        {
                            hasError = true;
                            continue;
                        }
                        bool hasDup = definition._singletons.Any( e => e.Service == type );
                        if( tOwner == null )
                        {
                            Debug.Assert( !hasDup, "No risk of duplicates: this has been handled at registration time." );
                            definition._singletons.Add( (type, null) );
                        }
                        else
                        {
                            var owner = FindOrCreate( monitor, engineMap, contexts, tOwner );
                            if( owner == null )
                            {
                                hasError = true;
                                continue;
                            }
                            if( hasDup )
                            {
                                Debug.Assert( definition._singletons.Where( e => e.Service == type ).All( e => e.Owner != null ) );
                                var duplicates = definition._singletons.Where( e => e.Service == type ).Select( c => c.Owner!.Name );
                                monitor.Error( $"In endpoint '{definition.Name}', service '{type:C}' is declared to be owned by '{duplicates.Concatenate( "', '" )}' and '{owner.Name}'." );
                                hasError = true;
                            }
                            definition._singletons.Add( (type, owner) );
                        }
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
