using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;

namespace CK.Setup
{

    class ServiceSupportCodeGenerator
    {
        const string _sourceServiceSupport = @"

        sealed class StObjServiceClassDescriptor : IStObjServiceClassDescriptor
        {
            public StObjServiceClassDescriptor( Type t, Type finalType, AutoServiceKind k, IReadOnlyCollection<Type> marshallableTypes, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq )
            {
                ClassType = t;
                FinalType = finalType;
                AutoServiceKind = k;
                MarshallableTypes = marshallableTypes;
                MultipleMappings = mult;
                UniqueMappings = uniq;
           }

            public Type ClassType { get; }

            public Type FinalType { get; }

            public bool IsScoped => (AutoServiceKind&AutoServiceKind.IsScoped) != 0;

            public AutoServiceKind AutoServiceKind { get; }

            public IReadOnlyCollection<Type> MarshallableTypes { get; }

            public IReadOnlyCollection<Type> MultipleMappings { get; }

            public IReadOnlyCollection<Type> UniqueMappings { get; }
        }
";
        readonly ITypeScope _rootType;
        readonly IFunctionScope _rootCtor;

        public ServiceSupportCodeGenerator( ITypeScope rootType, IFunctionScope rootCtor )
        {
            _rootType = rootType;
            _rootCtor = rootCtor;
        }

        public void CreateServiceSupportCode( IStObjServiceEngineMap serviceMap )
        {
            _rootType.Namespace.Append( _sourceServiceSupport );

            _rootType.GeneratedByComment().Append( @"
static readonly Dictionary<Type, IStObjFinalImplementation> _objectServiceMappings;
static readonly IStObjFinalImplementation[] _objectServiceMappingList;
static readonly Dictionary<Type, IStObjServiceClassDescriptor> _serviceMappings;
static readonly IStObjServiceClassDescriptor[] _serviceMappingList;

public IStObjServiceMap Services => this;
IStObjFinalClass? IStObjServiceMap.ToLeaf( Type t ) => ToServiceLeaf( t );

IStObjFinalClass? ToServiceLeaf( Type t )
{
    return _serviceMappings.TryGetValue( t, out var service )
                                    ? service
                                    : _objectServiceMappings.TryGetValue( t, out var realObject ) ? realObject : null;
}
public IStObjFinalClass? ToLeaf( Type t ) => ToServiceLeaf( t ) ?? GToLeaf( t );
IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => _objectServiceMappings;
IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => _objectServiceMappingList;
IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.Mappings => _serviceMappings;
IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.MappingList => _serviceMappingList;" )
                     .NewLine();

            // Object mappings.
            _rootCtor.GeneratedByComment().NewLine()
                     .Append( $"_objectServiceMappings = new Dictionary<Type, IStObjFinalImplementation>({serviceMap.ObjectMappings.Count});" ).NewLine();
            foreach( var map in serviceMap.ObjectMappings )
            {
                _rootCtor.Append( "_objectServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", _stObjs[" ).Append( map.Value.IndexOrdered ).Append( "].FinalImplementation );" )
                       .NewLine();
            }
            if( serviceMap.ObjectMappingList.Count > 0 )
            {
                _rootCtor.Append( $"_objectServiceMappingList = new IStObjFinalImplementation[] {{" ).NewLine();
                foreach( var o in serviceMap.ObjectMappingList )
                {
                    _rootCtor.NewLine().Append( "_stObjs[" ).Append( o.IndexOrdered ).Append( "].FinalImplementation," );
                }
                _rootCtor.NewLine().Append( "};" ).NewLine();
            }
            else
            {
                _rootCtor.Append( $"_objectServiceMappingList = Array.Empty<IStObjFinalImplementation>();" ).NewLine();
            }
            // 

            static void AppendArrayDecl( IFunctionScope f, string typeName, int count )
            {
                if( count > 0 )
                {
                    f.Append( "new " ).Append( typeName ).Append( "[" ).Append( count ).Append( "];" ).NewLine();
                }
                else
                {
                    f.Append( "Array.Empty<" ).Append( typeName ).Append( ">();" ).NewLine();
                }
            }

            // Service mappings (Simple).
            _rootCtor.Append( $"_serviceMappingList = " );
            AppendArrayDecl( _rootCtor, nameof( IStObjServiceClassDescriptor ), serviceMap.MappingList.Count );
            foreach( var d in serviceMap.MappingList )
            {
                Debug.Assert( d.MappingIndex >= 0 );
                _rootCtor.Append( "_serviceMappingList[" ).Append( d.MappingIndex ).Append( "] = new StObjServiceClassDescriptor(" )
                            .AppendTypeOf( d.ClassType )
                            .Append( ", " )
                            .AppendTypeOf( d.FinalType )
                            .Append( ", " )
                            .Append( d.AutoServiceKind )
                            .Append( ", " )
                            .AppendArray( d.MarshallableTypes )
                            .Append( ", " )
                            .AppendArray( d.MultipleMappings )
                            .Append( ", " )
                            .AppendArray( d.UniqueMappings )
                            .Append( ");" ).NewLine();
            }

            _rootCtor.Append( $"_serviceMappings = new Dictionary<Type, IStObjServiceClassDescriptor>({serviceMap.Mappings.Count});" ).NewLine();
            foreach( var map in serviceMap.Mappings )
            {
                _rootCtor.Append( $"_serviceMappings.Add( " )
                            .AppendTypeOf( map.Key )
                            .Append( ", " )
                            .Append( "_serviceMappingList[" ).Append( map.Value.MappingIndex ).Append("] );")
                            .NewLine();
            }
        }

        public void CreateRealObjectConfigureServiceMethod( IReadOnlyList<IStObjResult> orderedStObjs )
        {
            _rootType.GeneratedByComment().NewLine();
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
                    configure.AppendOnce( "GStObj s;" ).NewLine();
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

        public void CreateFillUniqueMappingsAndGlobalMultipleMappingsMethod()
        {
            _rootType.GeneratedByComment().NewLine()
                     .Append( """
                            void FillUniqueMappingsAndGlobalMultipleMappings( IActivityMonitor monitor,
                                                                              Microsoft.Extensions.DependencyInjection.IServiceCollection global,
                                                                              Microsoft.Extensions.DependencyInjection.IServiceCollection? commonEndpoint )
                            {
                                var mapMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( IStObjMap ), this );
                                global.Add( mapMapping );
                                commonEndpoint?.Add( mapMapping );
                                foreach( var o in _finalStObjs )
                                {
                                    var typeMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( o.ClassType, o.Implementation );
                                    global.Add( typeMapping );
                                    commonEndpoint?.Add( typeMapping );
                                    foreach( var unique in o.UniqueMappings )
                                    {
                                        var uMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( unique, o.Implementation );
                                        global.Add( uMapping );
                                        commonEndpoint?.Add( uMapping );
                                    }
                                    foreach( var multi in o.MultipleMappings )
                                    {
                                        var mMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( multi, o.Implementation );
                                        global.Add( mMapping );
                                    }
                                }
                                foreach( var m in _serviceMappingList )
                                {
                                    if( m.IsScoped )
                                    {
                                        var typeMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( m.ClassType, m.FinalType, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped );
                                        global.Add( typeMapping );
                                        commonEndpoint?.Add( typeMapping );
                                        foreach( var unique in m.UniqueMappings )
                                        {
                                            var uMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( unique, sp => sp.GetService( m.ClassType ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped );
                                            global.Add( uMapping );
                                            commonEndpoint?.Add( uMapping );
                                        }
                                        foreach( var multi in m.MultipleMappings )
                                        {
                                            var mMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( multi, sp => sp.GetService( m.ClassType ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped );
                                            global.Add( mMapping );
                                        }
                                    }
                                    else
                                    {
                                        if( m.ClassType == typeof( EndpointTypeManager ) ) continue;
                                        var gMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( m.ClassType, m.FinalType, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton );
                                        global.Add( gMapping );
                                        if( commonEndpoint != null )
                                        {
                                            var eMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( m.ClassType, sp => ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider.GetService( m.ClassType ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton );
                                            commonEndpoint.Add( eMapping );
                                        }
                                        foreach( var unique in m.UniqueMappings )
                                        {
                                            var guMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( unique, sp => sp.GetService( m.ClassType ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton );
                                            global.Add( guMapping );
                                            if( commonEndpoint != null )
                                            {
                                                var euMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( unique, sp => ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider.GetService( m.ClassType ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton );
                                                commonEndpoint?.Add( euMapping );
                                            }
                                        }
                                        foreach( var multi in m.MultipleMappings )
                                        {
                                            var mMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( multi, sp => sp.GetService( m.ClassType ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton );
                                            global.Add( mMapping );
                                        }
                                    }
                                }
                            }
                            """ );
        }

        public void CreateConfigureServiceMethod( IStObjEngineMap engineMap )
        {
            var endpointResult = engineMap.EndpointResult;
            bool hasEndpoint = endpointResult.EndpointContexts.Count > 1;

            EndpointSourceCodeGenerator.GenerateSupportCode( _rootType.Workspace, hasEndpoint );
            if( hasEndpoint )
            {
                EndpointSourceCodeGenerator.CreateFillMultipleEndpointMappingsMethod( _rootType, engineMap.Multiplemappings );
            }

            var fScope = _rootType.CreateFunction( "public bool ConfigureServices( in StObjContextRoot.ServiceRegister reg )" );

            fScope.Append( "RealObjectConfigureServices( in reg );" ).NewLine();

            // Common endpoint container configuration is done on the global, externally configured services so that
            // we minimize the number of registrations to process.
            if( hasEndpoint )
            {
                fScope.Append( "var externalMappings = new Dictionary<Type, object>();" ).NewLine()
                      .Append( "var common = EndpointHelper.CreateCommonEndpointContainer( reg.Monitor, reg.Services, EndpointTypeManager_CK._endpointServices.Contains, externalMappings );" )
                      .NewLine();
            }
            // No one else can register the purely code generated HostedServiceLifetimeTrigger hosted service: we do it here.
            // We insert it at the start of the global container: it will be the very first Hosted service to be instantiated.
            // The "true" singleton EndpointTypeManager is registered: it is the relay from endpoint containers to the global one.
            fScope.Append( """
                           reg.Services.Insert( 0, new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(
                                    typeof( Microsoft.Extensions.Hosting.IHostedService ),
                                    typeof( HostedServiceLifetimeTrigger ),
                                    Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton ) );
                           var theEPTM = new EndpointTypeManager_CK();
                           var descEPTM = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( typeof( EndpointTypeManager ), theEPTM );
                           reg.Services.Add( descEPTM );
                           """ ).NewLine();
            if( !hasEndpoint )
            {
                // If there's no endpoint, we are done (and we have no common endpoint container).
                fScope.Append( "FillUniqueMappingsAndGlobalMultipleMappings( reg.Monitor, reg.Services, null );" ).NewLine()
                      .Append( "// Waiting for .Net 8: (reg.Services as Microsoft.Extensions.DependencyInjection.ServiceCollection)?.MakeReadOnly();" ).NewLine()
                      .Append( "return true;" );
                return;
            }
            // Fills both the global and the common endpoint containers with the real objects (true singletons) and the auto services registrations.
            // We specifically handle the IEnumerable multiple mappings in the endpoint container and eventually enables
            // the endpoints to configure their endpoint services.
            fScope.Append( """
                           FillUniqueMappingsAndGlobalMultipleMappings( reg.Monitor, reg.Services, common );
                           // Waiting for .Net 8: (reg.Services as Microsoft.Extensions.DependencyInjection.ServiceCollection)?.MakeReadOnly();
                           common.Add( descEPTM );
                           FillMultipleEndpointMappings( reg.Monitor, common, externalMappings );
                           bool success = true;
                           foreach( var e in theEPTM._endpointTypes )
                           {
                               if( !e.ConfigureServices( reg.Monitor, this, common ) ) success = false;
                           }
                           return success;
                           """ ).NewLine();
        }

    }
}
