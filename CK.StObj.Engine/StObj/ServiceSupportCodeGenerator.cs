using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;

namespace CK.Setup;

class ServiceSupportCodeGenerator
{
    const string _stObjServiceClassDescriptor = """
                sealed class StObjServiceClassDescriptor : IStObjServiceClassDescriptor
                {
                    public StObjServiceClassDescriptor( Type t, Type finalType, AutoServiceKind k, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq )
                    {
                        ClassType = t;
                        FinalType = finalType;
                        AutoServiceKind = k;
                        MultipleMappings = mult;
                        UniqueMappings = uniq;
                   }

                    public Type ClassType { get; }

                    public Type FinalType { get; }

                    public bool IsScoped => (AutoServiceKind&AutoServiceKind.IsScoped) != 0;

                    public AutoServiceKind AutoServiceKind { get; }

                    public IReadOnlyCollection<Type> MultipleMappings { get; }

                    public IReadOnlyCollection<Type> UniqueMappings { get; }
                }
                """;

    readonly ITypeScope _rootType;
    readonly IFunctionScope _rootCtor;

    public ServiceSupportCodeGenerator( ITypeScope rootType, IFunctionScope rootCtor )
    {
        _rootType = rootType;
        _rootCtor = rootCtor;
    }

    public void CreateServiceSupportCode( IStObjEngineMap engineMap )
    {
        var serviceMap = engineMap.Services;
        _rootType.Namespace.Append( _stObjServiceClassDescriptor );

        using( _rootType.Region() )
        {
            _rootType.Append( @"
static readonly Dictionary<Type, IStObjFinalImplementation> _objectServiceMappings;
static readonly IStObjFinalImplementation[] _objectServiceMappingList;
static readonly Dictionary<Type, IStObjServiceClassDescriptor> _serviceMappings;
static readonly IStObjServiceClassDescriptor[] _serviceMappingList;
static readonly Dictionary<Type,IStObjMultipleInterface> _multipleMappings;

// Direct static access to the IStObjServiceClassDescriptor services.
// - (TODO: Unify MappingIndex on a GFinalRealObjectWithAutoService or something like that and rename this static AutoServices) The GenServices list indexed by IStObjServiceClassDescriptor.MappingIndex.
// - The ToServiceLeaf (IStObjServiceMap.ToLeaf) that returns a IStObjServiceClassDescriptor or a real object only if the Type is a Auto service.
// - The ToLeaf (IStObjMap.ToLeaf) that returns any real object or a IStObjServiceClassDescriptor.
public static IReadOnlyList<IStObjServiceClassDescriptor> GenServices => _serviceMappingList;
public static IStObjFinalClass? ToServiceLeaf( Type t )
{
    return _serviceMappings.TryGetValue( t, out var service )
                                    ? service
                                    : _objectServiceMappings.TryGetValue( t, out var realObject ) ? realObject : null;
}
public static IStObjFinalClass? ToLeaf( Type t ) => ToServiceLeaf( t ) ?? ToRealObjectLeaf( t );

IStObjServiceMap IStObjMap.Services => this;
IReadOnlyDictionary<Type,IStObjMultipleInterface> IStObjMap.MultipleMappings => _multipleMappings;
IStObjFinalClass? IStObjMap.ToLeaf( Type t ) => ToServiceLeaf( t ) ?? ToRealObjectLeaf( t );

IStObjFinalClass? IStObjServiceMap.ToLeaf( Type t ) => ToServiceLeaf( t );
IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => _objectServiceMappings;
IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => _objectServiceMappingList;
IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.Mappings => _serviceMappings;
IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.MappingList => _serviceMappingList;" )
                     .NewLine();
        }

        using( _rootCtor.Region() )
        {
            // Object mappings.
            _rootCtor.Append( $"_objectServiceMappings = new Dictionary<Type, IStObjFinalImplementation>({serviceMap.ObjectMappings.Count});" ).NewLine();
            foreach( var map in serviceMap.ObjectMappings )
            {
                _rootCtor.Append( "_objectServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", _stObjs[" ).Append( map.Value.IndexOrdered ).Append( "].FinalImplementation );" )
                       .NewLine();
            }
            if( serviceMap.ObjectMappingList.Count > 0 )
            {
                _rootCtor.Append( "_objectServiceMappingList = new IStObjFinalImplementation[] {" ).NewLine();
                foreach( var o in serviceMap.ObjectMappingList )
                {
                    _rootCtor.Append( "_stObjs[" ).Append( o.IndexOrdered ).Append( "].FinalImplementation," ).NewLine();
                }
                _rootCtor.Append( "};" ).NewLine();
            }
            else
            {
                _rootCtor.Append( $"_objectServiceMappingList = Array.Empty<IStObjFinalImplementation>();" ).NewLine();
            }
            // Service mappings.
            _rootCtor.Append( "_serviceMappingList = new IStObjServiceClassDescriptor[] {" ).NewLine();
            foreach( var d in serviceMap.MappingList )
            {
                Debug.Assert( d.MappingIndex >= 0 );
                _rootCtor.Append( "new StObjServiceClassDescriptor(" )
                            .AppendTypeOf( d.ClassType )
                            .Append( ", " )
                            .AppendTypeOf( d.FinalType )
                            .Append( ", " )
                            .Append( d.AutoServiceKind )
                            .Append( ", " )
                            .AppendArray( d.MultipleMappings )
                            .Append( ", " )
                            .AppendArray( d.UniqueMappings )
                            .Append( ")," ).NewLine();
            }
            _rootCtor.Append( "};" ).NewLine();

            _rootCtor.Append( $"_serviceMappings = new Dictionary<Type, IStObjServiceClassDescriptor>({serviceMap.Mappings.Count});" ).NewLine();
            foreach( var map in serviceMap.Mappings )
            {
                _rootCtor.Append( $"_serviceMappings.Add( " )
                            .AppendTypeOf( map.Key )
                            .Append( ", " )
                            .Append( "_serviceMappingList[" ).Append( map.Value.MappingIndex ).Append( "] );" )
                            .NewLine();
            }
            _rootCtor.Append( $"_multipleMappings = new Dictionary<Type, IStObjMultipleInterface>({engineMap.MultipleMappings.Count});" ).NewLine();
            foreach( var (t,m) in engineMap.MultipleMappings )
            {
                _rootCtor.Append( "_multipleMappings.Add(" ).AppendTypeOf( t )
                         .Append( ", new GMultiple( " )
                         .Append( m.IsScoped ).Append( ", " ).NewLine()
                         .Append( m.ItemType ).Append( ", " ).NewLine()
                         .Append( m.EnumerableType ).Append( ", " ).NewLine()
                         .Append( "new IStObjFinalClass[] {" );
                foreach( var i in m.Implementations )
                {
                    if( i is IStObjFinalImplementationResult realObject )
                    {
                        _rootCtor.Append( "_stObjs[" ).Append( realObject.IndexOrdered ).Append( "].FinalImplementation" );
                    }
                    else
                    {
                        Debug.Assert( i is IStObjServiceFinalSimpleMapping );
                        _rootCtor.Append( "_serviceMappingList[" ).Append( ((IStObjServiceFinalSimpleMapping)i).MappingIndex ).Append( "]" );
                    }
                    _rootCtor.Append( ", " );
                }
                _rootCtor.Append( "}) );" ).NewLine();
            }
        }
    }

    public void CreateRealObjectConfigureServiceMethod( IReadOnlyList<IStObjResult> orderedStObjs )
    {
        using var region = _rootType.Region();
        var configure = _rootType.CreateFunction( "void RealObjectConfigureServices( in StObjContextRoot.ServiceRegister register )" );

        configure.Append( "register.StartupServices.Add( typeof(IStObjObjectMap), this );" ).NewLine()
                 .Append( "object[] registerParam = new object[]{ register.Monitor, register.StartupServices };" ).NewLine();

        // Calls the RegisterStartupServices methods.
        foreach( MutableItem m in orderedStObjs ) 
        {
            foreach( var reg in m.RealObjectType.AllRegisterStartupServices )
            {
                if( reg == m.RealObjectType.RegisterStartupServices ) configure.Append( $"_stObjs[{m.IndexOrdered}].ClassType" );
                else configure.AppendTypeOf( reg.DeclaringType! );

                configure.Append( ".GetMethod(" )
                         .AppendSourceString( StObjContextRoot.RegisterStartupServicesMethodName )
                         .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                         .NewLine();
                configure.Append( $".Invoke( _stObjs[{m.IndexOrdered}].FinalImplementation.Implementation, registerParam );" )
                         .NewLine();
            }
        }

        // Calls the ConfigureServices methods.
        foreach( MutableItem m in orderedStObjs )
        {
            foreach( var parameters in m.RealObjectType.AllConfigureServicesParameters )
            {
                configure.AppendOnce( "GRealObject s;" ).NewLine();
                configure.AppendOnce( "MethodInfo m;" ).NewLine();

                configure.Append( $"s = _stObjs[{m.IndexOrdered}];" ).NewLine();

                configure.Append( "m = " );
                if( parameters == m.RealObjectType.ConfigureServicesParameters )
                    configure.Append( "s.ClassType" );
                else configure.AppendTypeOf( parameters[0].Member.DeclaringType! );

                configure.Append( ".GetMethod(" )
                         .AppendSourceString( StObjContextRoot.ConfigureServicesMethodName )
                         .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly );" )
                         .NewLine();

                if( parameters.Length > 1 )
                {
                    configure.AppendOnce( @"
            var services = register.StartupServices;
            var monitor = register.Monitor;
            T Resolve<T>( bool o ) where T : class
            {
                var r = (T)services.GetService(typeof(T));
                if( r == null )
                {
                    var mStr = $""{m.DeclaringType.FullName + '.' + m.Name}: unable to resolve service '{typeof(T).FullName}' from StartupServices."";
                    if( !o ) throw new Exception( mStr );
                    monitor.Info( mStr + "" Optional service ignored."" );
                }
                return r;
            }" );
                }
                configure.Append( "m.Invoke( s.FinalImplementation.Implementation, new object[]{ register" );
                foreach( var p in parameters.Skip( 1 ) )
                {
                    configure.Append( $", Resolve<" ).AppendCSharpName( p.ParameterType, true, false, true ).Append( ">(" ).Append( p.HasDefaultValue ).Append( ')' );
                }
                configure.Append( " } );" ).NewLine();
            }
        }
    }

    public void CreateConfigureServiceMethod( IActivityMonitor monitor, IStObjEngineMap engineMap )
    {
        var endpointResult = engineMap.EndpointResult;
        bool hasDIContainer = endpointResult.Containers.Count > 0;

        EndpointSourceCodeGenerator.GenerateSupportCode( _rootType.Workspace, hasDIContainer );

        var fScope = _rootType.CreateFunction( "public bool ConfigureServices( in StObjContextRoot.ServiceRegister reg )" );
        using var region = fScope.Region();

        fScope.Append( "RealObjectConfigureServices( in reg );" ).NewLine()
              .Append( "if( !EndpointHelper.CheckAndNormalizeAmbientServices( reg.Monitor, reg.Services, true ) ) return false;" ).NewLine();

        // Common endpoint container configuration is done on the global, externally configured services so that
        // we minimize the number of registrations to process.
        if( hasDIContainer )
        {
            fScope.Append( "var mappings = EndpointHelper.CreateInitialMapping( reg.Monitor, reg.Services, DIContainerHub_CK._endpointServices.ContainsKey );" ).NewLine();
        }
        // No one else can register the purely code generated HostedServiceLifetimeTrigger hosted service: we do it here.
        // We insert it at the start of the global container: it will be the very first Hosted service to be instantiated.
        // The common descriptors are then injected: the "true" singleton DIContainerHub is registered is the relay from endpoint containers
        // to the global one and the ScopedDataHolder is also registered.
        fScope.Append( """
                    reg.Services.Insert( 0, new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(
                                                    typeof( Microsoft.Extensions.Hosting.IHostedService ),
                                                    typeof( HostedServiceLifetimeTrigger ),
                                                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton ) );
                    var theEPTM = new DIContainerHub_CK();
                    var commonDescriptors = theEPTM.CreateCommonDescriptors( this );
                    reg.Services.AddRange( commonDescriptors );
                    """ ).NewLine();
        if( !hasDIContainer )
        {
            // If there's no endpoint, we must only register the StObjMap (real objects and auto services) and we are done
            // (we have no mappings for endpoint container).
            fScope.Append( "EndpointHelper.FillStObjMappingsNoEndpoint( reg.Monitor, this, reg.Services );" ).NewLine()
                    .Append( "// Waiting for .Net 8: (reg.Services as Microsoft.Extensions.DependencyInjection.ServiceCollection)?.MakeReadOnly();" ).NewLine()
                    .Append( "return true;" );
            return;
        }
        // Fills both the global and the common endpoint containers with the real objects (true singletons) and the auto services registrations.
        // We specifically handle the IEnumerable multiple mappings in the endpoint container and eventually enables
        // the endpoints to configure their endpoint services.
        fScope.Append( """
                    EndpointHelper.FillStObjMappingsWithEndpoints( reg.Monitor, this, reg.Services, mappings );
                    // Waiting for .Net 8: (reg.Services as Microsoft.Extensions.DependencyInjection.ServiceCollection)?.MakeReadOnly();
                    bool success = true;
                    foreach( var e in theEPTM._containers )
                    {
                        if( !e.ConfigureServices( reg.Monitor, this, mappings, commonDescriptors ) ) success = false;
                    }
                    return success;
                    """ ).NewLine();
    }

}
