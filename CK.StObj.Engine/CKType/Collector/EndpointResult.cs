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
        readonly IReadOnlyList<Type> _ubiquitousServices;
        readonly IReadOnlyList<EndpointContext> _contexts;

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
                Type[] overriddenUbiquitousServices;
                var nestedDataType = d.ClassType.GetNestedType( "Data" );
                if( nestedDataType == null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' must define a nested 'public class Data : ScopedData' class." );
                    return null;
                }
                if( nestedDataType.BaseType != typeof( EndpointDefinition<>.ScopedData ) )
                {
                    monitor.Error( $"Type '{d.ClassType:C}.Data' must specialize ScopedData class (not {nestedDataType.BaseType:C})." );
                    return null;
                }
                var ctors = nestedDataType.GetConstructors();
                if( ctors.Length > 1 )
                {
                    monitor.Error( $"Type '{d.ClassType:C}.Data' can have one and only one constructor (found {ctors.Length})." );
                    return null;
                }
                // Should handle auto services mapping here: if IMoreAuthInfo : IAuthInfo and IAuthInfo is a IAutoService, both must be
                // handled. TODO: fix this (but currently the ubiquitous registrations are not controlled anyway).
                var parameters = ctors[0].GetParameters().Where( p => kindDetector.UbiquitousInfoServices.Contains( p.ParameterType ) );
                if( parameters.Any() )
                {
                    // Should consider nullability info of parameter here: a nullable parameter optionally overrides!
                    monitor.Info( $"Endpoint '{rName}' overrides ubiquitous endpoint services: {parameters.Select( p => p.ParameterType.ToCSharpName()).Concatenate()}." );
                    overriddenUbiquitousServices = parameters.Select( p => p.ParameterType ).ToArray();
                }
                else
                {
                    overriddenUbiquitousServices = Type.EmptyTypes;
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
                contexts.Add( new EndpointContext( d, rName, scopeDataType, overriddenUbiquitousServices ) );
            }
            return new EndpointResult( (IReadOnlyList<EndpointContext>?)contexts ?? Array.Empty<EndpointContext>(),
                                       kindDetector.EndpointServices,
                                       kindDetector.UbiquitousInfoServices );
        }
    }
}
