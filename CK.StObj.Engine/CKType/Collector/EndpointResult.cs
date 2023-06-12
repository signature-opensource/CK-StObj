using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures the information about endpoint services: this is a reverse index of the
    /// <see cref="CKTypeEndpointServiceInfo"/> based on existing <see cref="EndpointDefinition"/>.
    /// </summary>
    public sealed class EndpointResult : IEndpointResult
    {
        readonly IReadOnlyDictionary<Type, AutoServiceKind> _endpointServices;
        readonly IReadOnlyList<EndpointContext> _contexts;
        readonly IReadOnlyList<Type> _ubiquitousServices;

        /// <inheritdoc />
        public IEndpointContext DefaultEndpointContext => _contexts[0];

        /// <inheritdoc />
        public IReadOnlyList<IEndpointContext> EndpointContexts => _contexts;

        /// <inheritdoc />
        public IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices => _endpointServices;

        /// <inheritdoc />
        public IReadOnlyList<Type> UbiquitousInfoServices => _ubiquitousServices;

        EndpointResult( IReadOnlyList<EndpointContext> contexts,
                        IReadOnlyDictionary<Type, AutoServiceKind> endpointServices,
                        IReadOnlyList<Type> ubiquitousServices )
        {
            _contexts = contexts;
            _endpointServices = endpointServices;
            _ubiquitousServices = ubiquitousServices;
        }

        internal static EndpointResult? Create( IActivityMonitor monitor,
                                                IStObjObjectEngineMap engineMap,
                                                CKTypeKindDetector kindDetector )
        {
            List<EndpointContext>? contexts = null;
            foreach( var d in engineMap.FinalImplementations.Where( d => typeof( EndpointDefinition ).IsAssignableFrom( d.ClassType ) ) )
            {
                var rName = EndpointContext.DefinitionName( d.ClassType ).ToString();
                var scopeDataType = d.ClassType.BaseType!.GetGenericArguments()[0];
                var nestedDataType = d.ClassType.GetNestedType( "Data" );
                if( nestedDataType == null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' must define a nested 'public class Data : ScopedData' class." );
                    return null;
                }
                if( nestedDataType.BaseType != typeof( EndpointDefinition.ScopedData ) )
                {
                    monitor.Error( $"Type '{d.ClassType:C}.Data' must specialize ScopedData class (not {nestedDataType.BaseType:C})." );
                    return null;
                }
                if( contexts != null )
                {
                    var sameName = contexts.FirstOrDefault( c => c.Name == rName );
                    if( sameName != null )
                    {
                        monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' has Name = '{rName}' but type '{sameName.EndpointDefinition.ClassType:C}' has the same name." +
                                       " Endpoint definition names must be different." );
                        return null;
                    }
                    var sameType = contexts.FirstOrDefault( c => c.ScopeDataType == scopeDataType );
                    if( sameType != null )
                    {
                        monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' declares the same ScopeData as '{sameType.EndpointDefinition.ClassType:C}'." +
                                       " Endpoint definition ScopeData must be different." );
                        return null;
                    }
                }
                else contexts = new List<EndpointContext>();
                contexts.Add( new EndpointContext( d, rName, scopeDataType ) );
            }
            return new EndpointResult( (IReadOnlyList<EndpointContext>?)contexts ?? Array.Empty<EndpointContext>(),
                                       kindDetector.EndpointServices,
                                       kindDetector.UbiquitousInfoServices );
        }
    }
}
