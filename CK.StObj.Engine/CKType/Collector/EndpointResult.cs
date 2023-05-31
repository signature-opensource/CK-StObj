using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures the information about endpoint services: this is a reverse index of the
    /// <see cref="CKTypeEndpointServiceInfo"/> based on existing <see cref="EndpointDefinition"/>.
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
            var def = engineMap.ToLeaf( typeof( DefaultEndpointDefinition ) )!;
            var defaultContext = new EndpointContext( def, "Default", null );
            var contexts = new List<EndpointContext>() { defaultContext };
            foreach( var d in engineMap.FinalImplementations.Where( d => d != def && typeof( EndpointDefinition ).IsAssignableFrom( d.ClassType ) ) )
            {
                var rName = CKTypeEndpointServiceInfo.DefinitionName( d.ClassType ).ToString();
                var sameName = contexts.FirstOrDefault( c => c.Name == rName );
                if( sameName != null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' has Name = '{rName}' but type '{sameName.EndpointDefinition.ClassType:C}' has the same name." +
                                   " Endpoint definition names must be different." );
                    return null;
                }
                var scopeDataType = d.ClassType.BaseType!.GetGenericArguments()[0];
                var sameType = contexts.FirstOrDefault( c => c.ScopeDataType == scopeDataType );
                if( sameType != null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' declares the same ScopeData as '{sameType.EndpointDefinition.ClassType:C}'." +
                                   " Endpoint definition ScopeData must be different." );
                    return null;
                }
                contexts.Add( new EndpointContext( d, rName, scopeDataType ) );
            }

            bool hasError = false;
            foreach( var (type, info) in endpointServiceInfoMap )
            {
                foreach( var definition in info.Services )
                {
                    var c = Find( monitor, engineMap, contexts, definition );
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

            static EndpointContext? Find( IActivityMonitor monitor, IStObjObjectEngineMap engineMap, List<EndpointContext> contexts, Type t )
            {
                var c = contexts.FirstOrDefault( c => c.EndpointDefinition.ClassType == t );
                if( c == null )
                {
                    monitor.Error( $"Expected EndpointDefinition type '{t:C}' is not registered in StObjMap." );
                    return null;
                }
                return c;
            }
        }
    }
}
