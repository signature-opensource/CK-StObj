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
    /// Captures the information about existing DI containers.
    /// </summary>
    public sealed class DIContainerAnalysisResult : IDIContainerAnalysisResult
    {
        readonly IReadOnlyDictionary<Type, AutoServiceKind> _endpointServices;
        readonly IReadOnlyList<DIContainerInfo> _containers;
        readonly IReadOnlyList<Type> _rawAmbientServices;
        readonly List<DIContainerHub.AmbientServiceMapping> _ambientMappings;
        readonly List<IDIContainerAnalysisResult.AmbientServiceDefault> _ambienDefaults;

        /// <inheritdoc />
        public IReadOnlyList<IDIContainerInfo> Containers => _containers;

        /// <inheritdoc />
        public IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices => _endpointServices;

        /// <inheritdoc />
        public bool HasAmbientServices => _rawAmbientServices.Count > 0;

        /// <inheritdoc />
        public IReadOnlyList<DIContainerHub.AmbientServiceMapping> AmbientServiceMappings => _ambientMappings;

        /// <inheritdoc />
        public IReadOnlyList<IDIContainerAnalysisResult.AmbientServiceDefault> DefaultAmbientServiceValueProviders => _ambienDefaults;

        DIContainerAnalysisResult( IReadOnlyList<DIContainerInfo> contexts,
                                   IReadOnlyDictionary<Type, AutoServiceKind> endpointServices,
                                   IReadOnlyList<Type> ambientServices )
        {
            _containers = contexts;
            _endpointServices = endpointServices;
            _rawAmbientServices = ambientServices;
            _ambientMappings = new List<DIContainerHub.AmbientServiceMapping>();
            _ambienDefaults = new List<IDIContainerAnalysisResult.AmbientServiceDefault>();
        }

        internal static DIContainerAnalysisResult? Create( IActivityMonitor monitor,
                                                IStObjObjectEngineMap engineMap,
                                                CKTypeKindDetector kindDetector )
        {
            List<DIContainerInfo>? contexts = null;
            foreach( var d in engineMap.FinalImplementations.Where( d => typeof( DIContainerDefinition ).IsAssignableFrom( d.ClassType ) ) )
            {
                var rName = DIContainerInfo.DefinitionName( d.ClassType ).ToString();
                var attr = d.Attributes.GetTypeCustomAttributes<DIContainerDefinitionImpl>().FirstOrDefault();
                if( attr == null )
                {
                    monitor.Error( $"DIContainerDefinition type '{d.ClassType:C}' must be decorated with a [DIContainerDefinition( DIContainerKind.XXX )] attribute." );
                    return null;
                }
                var kind = attr.Kind;
                var nestedDataType = d.ClassType.GetNestedType( "Data" );
                if( nestedDataType == null )
                {
                    monitor.Error( $"DIContainerDefinition type '{d.ClassType:C}' must define a nested 'public class Data : ScopedData' class." );
                    return null;
                }
                var scopeDataType = d.ClassType.BaseType!.GetGenericArguments()[0];
                if( scopeDataType != nestedDataType )
                {
                    monitor.Error( $"The generic parameter of '{d.ClassType:C}' must be '{d.ClassType.Name}.Data'." );
                    return null;
                }
                bool isBackData = typeof( DIContainerDefinition.BackendScopedData ).IsAssignableFrom( scopeDataType );
                if( isBackData != (kind == DIContainerKind.Background) )
                {
                    if( isBackData )
                    {
                        monitor.Error( $"Type '{d.ClassType:C}.Data' must not specialize BackendScopedData, it must simply support the IScopedData interface because it is a Endpoint DI container." );
                    }
                    else
                    {
                        monitor.Error( $"Type '{d.ClassType:C}.Data' must specialize BackendScopedData because it is a Backend DI container." );
                    }
                    return null;
                }
                if( contexts != null )
                {
                    var sameName = contexts.FirstOrDefault( c => c.Name == rName );
                    if( sameName != null )
                    {
                        monitor.Error( $"DIContainerDefinition type '{d.ClassType:C}' has Name = '{rName}' but type '{sameName.DIContainerDefinition.ClassType:C}' has the same name." +
                                       " Container definition names must be different." );
                        return null;
                    }
                    var sameType = contexts.FirstOrDefault( c => c.ScopeDataType == scopeDataType );
                    if( sameType != null )
                    {
                        monitor.Error( $"DIContainerDefinition type '{d.ClassType:C}' declares the same ScopeData as '{sameType.DIContainerDefinition.ClassType:C}'." +
                                       " Container definition ScopeData must be different." );
                        return null;
                    }
                }
                else contexts = new List<DIContainerInfo>();
                contexts.Add( new DIContainerInfo( d, rName, attr.Kind, scopeDataType ) );
            }
            return new DIContainerAnalysisResult( (IReadOnlyList<DIContainerInfo>?)contexts ?? Array.Empty<DIContainerInfo>(),
                                                   kindDetector.ContainerConfiguredServices,
                                                   kindDetector.AmbientServices );
        }

        internal bool BuildAmbientServiceMappingsAndCheckDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services )
        {
            using var gLog = monitor.OpenInfo( $"Checking IEndpointUbiquitousServiceDefault availability for {_rawAmbientServices.Count} ubiquitous information services and build mappings." );

            bool success = true;
            // Use list and not hash set (no volume here).
            // This is not an easy part to understand. This builds the mappings and fills the _ambienDefaults
            // list. Mapping index reflects IAutoService inheritance chain (for auto service) and the
            // _ambienDefaults list is synchronized with this index.
            var ambientServiceTypes = new List<Type>( _rawAmbientServices );
            int current = 0;
            while( ambientServiceTypes.Count > 0 )
            {
                var t = ambientServiceTypes[ambientServiceTypes.Count - 1];
                var auto = services.ToLeaf( t );
                if( auto != null )
                {
                    // AmbientServiceHub is excluded from the ambient service list.
                    Throw.DebugAssert( t != typeof( AmbientServiceHub ) );
                    // We check that if more than one default value provider exists,
                    // they are the same final type.
                    IDIContainerAnalysisResult.AmbientServiceDefault? defaultProvider = null;
                    // We (heavily) rely on the fact that the UniqueMappings are ordered
                    // from most abstract to leaf type here.
                    foreach( var m in auto.UniqueMappings )
                    {
                        _ambientMappings.Add( new DIContainerHub.AmbientServiceMapping( m, current ) );
                        ambientServiceTypes.Remove( m );
                        if( !FindSameDefaultProvider( monitor, services, m, ref defaultProvider ) )
                        {
                            success = false;
                        }
                    }
                    _ambientMappings.Add( new DIContainerHub.AmbientServiceMapping( auto.ClassType, current ) );
                    ambientServiceTypes.Remove( auto.ClassType );
                    if( !FindSameDefaultProvider( monitor, services, t, ref defaultProvider ) )
                    {
                        success = false;
                    }
                    else if( !defaultProvider.HasValue )
                    {
                        monitor.Error( $"Unable to find an implementation of at least one 'IEndpointUbiquitousServiceDefault<T>' where T is " +
                                        $"one of '{auto.UniqueMappings.Append( auto.ClassType ).Select( t => t.Name ).Concatenate( "', '" )}'. " +
                                        $"All ambient service must have a default value provider." );
                        success = false;
                    }
                    else
                    {
                        _ambienDefaults.Add( defaultProvider.Value );
                    }
                }
                else
                {
                    var defaultProvider = FindDefaultProvider( monitor, services, t, expected: true );
                    if( defaultProvider != null )
                    {
                        _ambienDefaults.Add( defaultProvider.Value );
                    }
                    else
                    {
                        success = false;
                    }
                    _ambientMappings.Add( new DIContainerHub.AmbientServiceMapping( t, current ) );
                    ambientServiceTypes.RemoveAt( ambientServiceTypes.Count - 1 );
                }
                ++current;
            }
            return success;

            static IDIContainerAnalysisResult.AmbientServiceDefault? FindDefaultProvider( IActivityMonitor monitor, IStObjServiceEngineMap services, Type ambientServiceType, bool expected )
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
                return new IDIContainerAnalysisResult.AmbientServiceDefault( defaultProviderType, defaultProvider );
            }

            static bool FindSameDefaultProvider( IActivityMonitor monitor,
                                                 IStObjServiceEngineMap services,
                                                 Type t,
                                                 ref IDIContainerAnalysisResult.AmbientServiceDefault? defaultProvider )
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
