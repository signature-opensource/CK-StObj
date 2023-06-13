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
    /// <see cref="CKTypeEndpointServiceInfo"/> based on existing <see cref="EndpointDefinition"/>.
    /// </summary>
    public sealed class EndpointResult : IEndpointResult
    {
        readonly IReadOnlyDictionary<Type, AutoServiceKind> _endpointServices;
        readonly IReadOnlyList<EndpointContext> _contexts;
        readonly IReadOnlyList<Type> _rawUbiquitousServices;
        readonly List<EndpointTypeManager.UbiquitousMapping> _ubiquitousMappings;
        readonly List<IStObjFinalClass> _defaultUbiquitousProviders;

        /// <inheritdoc />
        public IReadOnlyList<IEndpointContext> EndpointContexts => _contexts;

        /// <inheritdoc />
        public IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices => _endpointServices;

        /// <inheritdoc />
        public bool HasUbiquitousInfoServices => _rawUbiquitousServices.Count > 0;

        /// <inheritdoc />
        public IReadOnlyList<EndpointTypeManager.UbiquitousMapping> UbiquitousMappings => _ubiquitousMappings;

        /// <inheritdoc />
        public IReadOnlyList<IStObjFinalClass> DefaultUbiquitousValueProviders => _defaultUbiquitousProviders;

        EndpointResult( IReadOnlyList<EndpointContext> contexts,
                        IReadOnlyDictionary<Type, AutoServiceKind> endpointServices,
                        IReadOnlyList<Type> ubiquitousServices )
        {
            _contexts = contexts;
            _endpointServices = endpointServices;
            _rawUbiquitousServices = ubiquitousServices;
            _ubiquitousMappings = new List<EndpointTypeManager.UbiquitousMapping>();
            _defaultUbiquitousProviders = new List<IStObjFinalClass>();
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
                var attr = d.Attributes.GetTypeCustomAttributes<EndpointDefinitionAttribute>().FirstOrDefault();
                if( attr == null )
                {
                    monitor.Error( $"EndpointDefinition type '{d.ClassType:C}' must be decorated with a [EndpointDefinition( EndpointKind.XXX )] attribute." );
                    return null;
                }
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
                contexts.Add( new EndpointContext( d, rName, attr.Kind, scopeDataType ) );
            }
            return new EndpointResult( (IReadOnlyList<EndpointContext>?)contexts ?? Array.Empty<EndpointContext>(),
                                       kindDetector.EndpointServices,
                                       kindDetector.UbiquitousInfoServices );
        }

        internal bool BuildUbiquitousMappingsAndCheckDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services )
        {
            using var gLog = monitor.OpenInfo( $"Checking IEndpointUbiquitousServiceDefault availability for {_rawUbiquitousServices.Count} ubiquitous information services and build mappings." );

            bool success = true;
            // Use list and not hash set (no volume here).
            var ubiquitousTypes = new List<Type>( _rawUbiquitousServices );
            int current = 0;
            while( ubiquitousTypes.Count > 0 )
            {
                var t = ubiquitousTypes[ubiquitousTypes.Count - 1];
                var auto = services.ToLeaf( t );
                if( auto != null )
                {
                    // We check that if more than one default value provider exists,
                    // they are the same final type.
                    IStObjFinalClass? defaultProvider = null;
                    // We (heavily) rely on the fact that the UniqueMappings are ordered
                    // from most abstract to leaf type here.
                    foreach( var m in auto.UniqueMappings )
                    {
                        _ubiquitousMappings.Add( new EndpointTypeManager.UbiquitousMapping( m, current) );
                        ubiquitousTypes.Remove( m );
                        if( !FindSameDefaultProvider( monitor, services, m, ref defaultProvider ) ) success = false;
                    }
                    _ubiquitousMappings.Add( new EndpointTypeManager.UbiquitousMapping(auto.ClassType, current) );
                    ubiquitousTypes.Remove( auto.ClassType );
                    if( defaultProvider == null )
                    {
                        defaultProvider = FindDefaultProvider( monitor, services, t, expected: false );
                        if( defaultProvider == null )
                        {
                            monitor.Error( $"Unable to find an implementation of at least one 'IEndpointUbiquitousServiceDefault<T>' where T is " +
                                           $"one of '{auto.UniqueMappings.Append( auto.ClassType ).Select( t => t.Name ).Concatenate( "', '")}'. " +
                                           $"All ubiquitous service must have a default value provider." );
                            success = false;
                        }
                    }
                }
                else
                {
                    var defaultProvider = FindDefaultProvider( monitor, services, t, expected: true );
                    if( defaultProvider != null )
                    {
                        _defaultUbiquitousProviders.Add( defaultProvider );
                    }
                    else
                    {
                        success = false;
                    }
                    _ubiquitousMappings.Add( new EndpointTypeManager.UbiquitousMapping( t, current) );
                    ubiquitousTypes.RemoveAt( ubiquitousTypes.Count - 1 );
                }
                ++current;
            }
            return success;

            static IStObjFinalClass? FindDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services, Type ubiquitousType, bool expected )
            {
                Type defaultProviderType = typeof( IEndpointUbiquitousServiceDefault<> ).MakeGenericType( ubiquitousType );
                var defaultProvider = services.ToLeaf( defaultProviderType );
                if( defaultProvider == null && expected )
                {
                    monitor.Error( $"Unable to find an implementation for '{defaultProviderType:C}'. " +
                                   $"Type '{ubiquitousType.Name}' is not a valid Ubiquitous information service, all ubiquitous service must have a default value provider." );
                }
                return defaultProvider;
            }

            static bool FindSameDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services, Type t, ref IStObjFinalClass? defaultProvider )
            {
                var d = FindDefaultProvider( monitor, services, t, false );
                if( d != null )
                {
                    if( defaultProvider != null && defaultProvider != d )
                    {
                        monitor.Error( $"Invalid ubiquitous service '{t.Name}': only one default value provider must exist. Found '{defaultProvider.ClassType:C}' and '{d.ClassType:C}'." );
                        return false;
                    }
                    defaultProvider = d;
                }
                return true;
            }
        }
    }
}
