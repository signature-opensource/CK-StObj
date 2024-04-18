using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures the information about endpoint services: this is a reverse index of the
    /// <see cref="EndpointContext"/> based on existing <see cref="EndpointDefinition"/>.
    /// </summary>
    public sealed class EndpointResult : IEndpointResult
    {
        readonly IReadOnlyDictionary<Type, AutoServiceKind> _endpointServices;
        readonly IReadOnlyList<EndpointContext> _contexts;
        readonly IReadOnlyList<Type> _rawAmbientServices;
        readonly List<EndpointTypeManager.AmbientServiceMapping> _ubiquitousMappings;
        readonly List<IEndpointResult.AmbientServiceDefault> _ubiquitousDefaults;

        /// <inheritdoc />
        public IReadOnlyList<IEndpointContext> EndpointContexts => _contexts;

        /// <inheritdoc />
        public IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices => _endpointServices;

        /// <inheritdoc />
        public bool HasAmbientServices => _rawAmbientServices.Count > 0;

        /// <inheritdoc />
        public IReadOnlyList<EndpointTypeManager.AmbientServiceMapping> AmbientServiceMappings => _ubiquitousMappings;

        /// <inheritdoc />
        public IReadOnlyList<IEndpointResult.AmbientServiceDefault> DefaultAmbientServiceValueProviders => _ubiquitousDefaults;

        EndpointResult( IReadOnlyList<EndpointContext> contexts,
                        IReadOnlyDictionary<Type, AutoServiceKind> endpointServices,
                        IReadOnlyList<Type> ubiquitousServices )
        {
            _contexts = contexts;
            _endpointServices = endpointServices;
            _rawAmbientServices = ubiquitousServices;
            _ubiquitousMappings = new List<EndpointTypeManager.AmbientServiceMapping>();
            _ubiquitousDefaults = new List<IEndpointResult.AmbientServiceDefault>();
        }

        internal static EndpointResult? Create( IActivityMonitor monitor,
                                                IStObjObjectEngineMap engineMap,
                                                CKTypeKindDetector kindDetector )
        {
            List<EndpointContext>? contexts = null;
            foreach( var d in engineMap.FinalImplementations.Where( d => typeof( EndpointDefinition ).IsAssignableFrom( d.ClassType ) ) )
            {
                var rName = EndpointContext.DefinitionName( d.ClassType ).ToString();
                var attr = d.Attributes.GetTypeCustomAttributes<EndpointDefinitionImpl>().FirstOrDefault();
                if( attr == null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' must be decorated with a [EndpointDefinition( EndpointKind.XXX )] attribute." );
                    return null;
                }
                var kind = attr.Kind;
                var nestedDataType = d.ClassType.GetNestedType( "Data" );
                if( nestedDataType == null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' must define a nested 'public class Data : ScopedData' class." );
                    return null;
                }
                var scopeDataType = d.ClassType.BaseType!.GetGenericArguments()[0];
                if( scopeDataType != nestedDataType )
                {
                    monitor.Error( $"The generic parameter of '{d.ClassType:C}' must be '{d.ClassType.Name}.Data'." );
                    return null;
                }
                bool isBackData = typeof( EndpointDefinition.BackScopedData ).IsAssignableFrom( scopeDataType );
                if( isBackData != (kind == EndpointKind.Back) )
                {
                    if( isBackData )
                    {
                        monitor.Error( $"Type '{d.ClassType:C}.Data' must not specialize BackScopedData, it must simply support the IScopedData interface because it is a Front endpoint." );
                    }
                    else
                    {
                        monitor.Error( $"Type '{d.ClassType:C}.Data' must specialize BackScopedData because it is a Back endpoint." );
                    }
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
                contexts.Add( new EndpointContext( d, rName, attr.Kind, scopeDataType ) );
            }
            return new EndpointResult( (IReadOnlyList<EndpointContext>?)contexts ?? Array.Empty<EndpointContext>(),
                                       kindDetector.EndpointServices,
                                       kindDetector.AmbientServices );
        }

        internal bool BuildAmbientServiceMappingsAndCheckDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services )
        {
            using var gLog = monitor.OpenInfo( $"Checking IEndpointUbiquitousServiceDefault availability for {_rawAmbientServices.Count} ubiquitous information services and build mappings." );

            bool success = true;
            // Use list and not hash set (no volume here).
            var ubiquitousTypes = new List<Type>( _rawAmbientServices );
            int current = 0;
            while( ubiquitousTypes.Count > 0 )
            {
                var t = ubiquitousTypes[ubiquitousTypes.Count - 1];
                var auto = services.ToLeaf( t );
                if( auto != null )
                {
                    // We check that if more than one default value provider exists,
                    // they are the same final type.
                    IEndpointResult.AmbientServiceDefault? defaultProvider = null;
                    // We (heavily) rely on the fact that the UniqueMappings are ordered
                    // from most abstract to leaf type here.
                    foreach( var m in auto.UniqueMappings )
                    {
                        _ubiquitousMappings.Add( new EndpointTypeManager.AmbientServiceMapping( m, current ) );
                        ubiquitousTypes.Remove( m );
                        if( !FindSameDefaultProvider( monitor, services, m, ref defaultProvider ) ) success = false;
                    }
                    _ubiquitousMappings.Add( new EndpointTypeManager.AmbientServiceMapping( auto.ClassType, current ) );
                    ubiquitousTypes.Remove( auto.ClassType );
                    if( !FindSameDefaultProvider( monitor, services, t, ref defaultProvider ) )
                    {
                        success = false;
                    }
                    else if( !defaultProvider.HasValue )
                    {
                        monitor.Error( $"Unable to find an implementation of at least one 'IEndpointUbiquitousServiceDefault<T>' where T is " +
                                       $"one of '{auto.UniqueMappings.Append( auto.ClassType ).Select( t => t.Name ).Concatenate( "', '" )}'. " +
                                       $"All ubiquitous service must have a default value provider." );
                        success = false;
                    }
                    else
                    {
                        _ubiquitousDefaults.Add( defaultProvider.Value );
                    }
                }
                else
                {
                    var defaultProvider = FindDefaultProvider( monitor, services, t, expected: true );
                    if( defaultProvider != null )
                    {
                        _ubiquitousDefaults.Add( defaultProvider.Value );
                    }
                    else
                    {
                        success = false;
                    }
                    _ubiquitousMappings.Add( new EndpointTypeManager.AmbientServiceMapping( t, current ) );
                    ubiquitousTypes.RemoveAt( ubiquitousTypes.Count - 1 );
                }
                ++current;
            }
            return success;

            static IEndpointResult.AmbientServiceDefault? FindDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services, Type ambientServiceType, bool expected )
            {
                Type defaultProviderType = typeof( IEndpointUbiquitousServiceDefault<> ).MakeGenericType( ambientServiceType );
                var defaultProvider = services.ToLeaf( defaultProviderType );
                if( defaultProvider == null )
                {
                    if( expected )
                    {
                        monitor.Error( $"Unable to find an implementation for '{defaultProviderType:C}'. " +
                                       $"Type '{ambientServiceType.Name}' is not a valid Ambient service, all ambient services must have a default value provider." );
                    }
                    return null;
                }
                return new IEndpointResult.AmbientServiceDefault( defaultProviderType, defaultProvider );
            }

            static bool FindSameDefaultProvider( IActivityMonitor monitor,
                                                 IStObjServiceEngineMap services,
                                                 Type t,
                                                 ref IEndpointResult.AmbientServiceDefault? defaultProvider )
            {
                var d = FindDefaultProvider( monitor, services, t, false );
                if( d != null )
                {
                    if( defaultProvider != null )
                    {
                        if( defaultProvider.Value.Provider != d.Value.Provider )
                        {
                            monitor.Error( $"Invalid ubiquitous service '{t.Name}': only one default value provider must exist. " +
                                           $"Found '{defaultProvider.Value.Provider.ClassType:C}' and '{d.Value.Provider.ClassType:C}'." );
                            return false;
                        }
                    }
                    // This is called from generalized to specialized: the final default provider
                    // is the most specialized one and this is fine (even if we eventually return an object from the
                    // ServiceDescriptor factory method).
                    defaultProvider = d;
                }
                return true;
            }
        }
    }
}
