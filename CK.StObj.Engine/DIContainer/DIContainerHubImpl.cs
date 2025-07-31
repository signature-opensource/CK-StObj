using CK.CodeGen;
using CK.Core;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Implements the DIContainerHub.
/// </summary>
public sealed class DIContainerHubImpl : CSCodeGeneratorType
{
    /// <inheritdoc />
    public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
    {
        Throw.DebugAssert( scope.FullName == "CK.Core.DIContainerHub_CK" );
        scope.Definition.Modifiers |= Modifiers.Sealed;

        scope.Append( """
            // This is static: there is only one global container per application.
            // More precisely, there is only one service configuration per loaded StObjMap
            // (there may be multiple StObjMap loaded in an application domain).
            static IServiceProvider? _globalServices;
            internal static IServiceProvider GlobalServices => _globalServices!;
            // This is called by the code generated HostedServiceLifetimeTrigger constructor. 
            internal static void SetGlobalServices( IServiceProvider serviceProvider ) => _globalServices = serviceProvider;

            internal static readonly DIContainerDefinition[] _containerDefinitions;
            internal static readonly IReadOnlyDictionary<Type,AutoServiceKind> _endpointServices;
            internal static readonly ImmutableArray<AmbientServiceMapping> _ambientMappings;
            internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _ambientServiceEndpointDescriptors;
            internal static Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] _ambientServiceBackendDescriptors;

            // instance field (bound to the DI world). 
            internal readonly CK.StObj.IDIContainerInternal[] _containers;

            public override IReadOnlyList<DIContainerDefinition> ContainerDefinitions => _containerDefinitions;
            public override IReadOnlyDictionary<Type,AutoServiceKind> EndpointServices => _endpointServices;
            public override IReadOnlyList<IDIContainer> Containers => _containers;
            public override IReadOnlyList<AmbientServiceMapping> AmbientServiceMappings => _ambientMappings;

            """ );

        var endpointResult = c.CurrentRun.EngineMap.EndpointResult;

        StaticConstructor( scope, c.CurrentRun.EngineMap );
        InstanceConstructor( scope, endpointResult );
        CreateCommonDescriptors( scope, endpointResult );

        return CSCodeGenerationResult.Success;
    }

    static void StaticConstructor( ITypeScope scope, IStObjEngineMap engineMap )
    {
        var endpointResult = engineMap.EndpointResult;

        scope.Append( "static DIContainerHub_CK()" )
             .OpenBlock();
        scope.Append( "_endpointServices = new Dictionary<Type,AutoServiceKind>() {" );
        foreach( var kv in endpointResult.EndpointServices )
        {
            scope.Append( "{" ).Append( kv.Key ).Append( "," ).Append( kv.Value ).Append( "}," ).NewLine();
        }
        scope.Append( " };" ).NewLine();

        scope.Append( "_containerDefinitions = new DIContainerDefinition[] {" ).NewLine();
        foreach( var e in endpointResult.Containers )
        {
            scope.Append( "(DIContainerDefinition)" ).Append( e.DIContainerDefinition.CodeInstanceAccessor ).Append( "," ).NewLine();
        }
        scope.Append( "};" ).NewLine();

        scope.Append( "_ambientMappings = ImmutableArray.Create<AmbientServiceMapping>( " ).NewLine();
        for( int i = 0; i < endpointResult.AmbientServiceMappings.Count; i++ )
        {
            DIContainerHub.AmbientServiceMapping e = endpointResult.AmbientServiceMappings[i];
            if( i > 0 ) scope.Append( "," ).NewLine();
            scope.Append( "new AmbientServiceMapping(" ).AppendTypeOf( e.AmbientServiceType ).Append( "," ).Append( e.MappingIndex ).Append( ")" );
        }
        scope.Append( ");" ).NewLine();

        var sharedPart = scope.CreatePart();
        scope.Append( "_ambientServiceBackendDescriptors = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {" ).NewLine();
        foreach( var e in endpointResult.AmbientServiceMappings )
        {
            if( !sharedPart.Memory.TryGetValue( e.MappingIndex, out var oGetter ) )
            {
                oGetter = $"back{e.MappingIndex}";
                sharedPart.Append( "Func<IServiceProvider,object> " ).Append( (string)oGetter )
                          .Append( " = sp => CK.StObj.ScopeDataHolder.GetAmbientService( sp, " ).Append( e.MappingIndex ).Append( " );" ).NewLine();
                sharedPart.Memory.Add( e.MappingIndex, oGetter );
            }
            Throw.DebugAssert( oGetter != null );
            scope.Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                 .AppendTypeOf( e.AmbientServiceType ).Append( ", " ).Append( (string)oGetter )
                 .Append( ", Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped )," ).NewLine();
        }
        scope.Append( "};" ).NewLine();

        sharedPart = scope.CreatePart();
        scope.Append( "_ambientServiceEndpointDescriptors = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {" ).NewLine();
        foreach( var e in endpointResult.AmbientServiceMappings )
        {
            var defaultProvider = endpointResult.DefaultAmbientServiceValueProviders[e.MappingIndex];
            if( defaultProvider.IsValid )
            {
                if( !sharedPart.Memory.TryGetValue( defaultProvider.Provider.ClassType, out var oGetter ) )
                {
                    oGetter = $"front{e.MappingIndex}";
                    sharedPart.Append( "Func<IServiceProvider,object> " ).Append( (string)oGetter )
                              .Append( " = sp => ((" ).AppendGlobalTypeName( defaultProvider.ProviderType )
                              .Append( "?)DIContainerHub_CK.GlobalServices.GetService( " ).AppendTypeOf( defaultProvider.Provider.ClassType )
                              .Append( " )!).Default;" ).NewLine();
                    sharedPart.Memory.Add( defaultProvider.Provider.ClassType, oGetter );
                }
                Throw.DebugAssert( oGetter != null );
                scope.Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                     .AppendTypeOf( e.AmbientServiceType ).Append( ", " ).Append( (string)oGetter )
                     .Append( ", Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped )," ).NewLine();
            }
        }
        scope.Append( "};" ).NewLine();

        scope.CloseBlock();
    }

    static void InstanceConstructor( ITypeScope scope, IDIContainerAnalysisResult endpointResult )
    {
        scope.Append( "internal DIContainerHub_CK()" )
             .OpenBlock();

        scope.Append( "_containers = new CK.StObj.IDIContainerInternal[] {" ).NewLine();
        int i = 0;
        foreach( var e in endpointResult.Containers )
        {
            // The DIContainerHub_CK directly and immediately instantiates the DIContainer<> objects.
            // These are singletons just like this DIContainerHub. They are registered
            // as singleton instances in the global container (and, as global singleton instances,
            // also in all containers). The IEnumerable<IDIContainer> is also explicitly
            // registered: that is the _containers array.
            var scopeDataTypeName = e.ScopeDataType.ToGlobalTypeName();
            scope.Append( "new CK.StObj.DIContainer<" )
                .Append( scopeDataTypeName )
                .Append( ">( (DIContainerDefinition<" ).Append( scopeDataTypeName ).Append( ">)_containerDefinitions[" ).Append( i++ ).Append( "] )," ).NewLine();
        }
        scope.Append( "};" ).NewLine();

        scope.CloseBlock();
    }

    static void CreateCommonDescriptors( ITypeScope scope, IDIContainerAnalysisResult endpointResult )
    {
        scope.Append( "internal Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] CreateCommonDescriptors( IStObjMap stObjMap )" )
             .OpenBlock()
             .Append( "return new Microsoft.Extensions.DependencyInjection.ServiceDescriptor[] {" ).NewLine()
             .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( DIContainerHub ), this )," ).NewLine()
             .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IStObjMap ), stObjMap )," ).NewLine()
             .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IEnumerable<IDIContainer> ), _containers )," ).NewLine()
             .Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( CK.StObj.ScopeDataHolder ), typeof( CK.StObj.ScopeDataHolder ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped )," ).NewLine();
        int i = 0;
        foreach( var e in endpointResult.Containers )
        {
            scope.Append( "new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IDIContainer<" )
                 .AppendGlobalTypeName( e.ScopeDataType )
                 .Append( "> ), _containers[" ).Append( i++ ).Append( "] )," ).NewLine();
        }
        scope.Append( "};" )
             .CloseBlock();
    }

}
