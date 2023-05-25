using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        readonly ITypeScope _infoType;

        public ServiceSupportCodeGenerator( ITypeScope rootType, IFunctionScope rootCtor )
        {
            _rootType = rootType;
            _infoType = rootType.Namespace.CreateType( "public static class SFInfo" );
            _rootCtor = rootCtor;
        }

        public void CreateServiceSupportCode( IStObjServiceEngineMap liftedMap )
        {
            _infoType.Namespace.Append( _sourceServiceSupport );

            _rootType.GeneratedByComment().Append( @"
static readonly Dictionary<Type, IStObjFinalImplementation> _objectServiceMappings;
static readonly IStObjFinalImplementation[] _objectServiceMappingList;
static readonly Dictionary<Type, IStObjServiceClassDescriptor> _simpleServiceMappings;
static readonly IStObjServiceClassDescriptor[] _simpleServiceList;

public IStObjServiceMap Services => this;
IStObjFinalClass? IStObjServiceMap.ToLeaf( Type t ) => ToServiceLeaf( t );

IStObjFinalClass? ToServiceLeaf( Type t )
{
    return _simpleServiceMappings.TryGetValue( t, out var service )
                                    ? service
                                    : _objectServiceMappings.TryGetValue( t, out var realObject ) ? realObject : null;
}
public IStObjFinalClass? ToLeaf( Type t ) => ToServiceLeaf( t ) ?? GToLeaf( t );
IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => _objectServiceMappings;
IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => _objectServiceMappingList;
IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappings => _simpleServiceMappings;
IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappingList => _simpleServiceList;" )
                     .NewLine();

            // Object mappings.
            _rootCtor.GeneratedByComment().NewLine()
                     .Append( $"_objectServiceMappings = new Dictionary<Type, IStObjFinalImplementation>({liftedMap.ObjectMappings.Count});" ).NewLine();
            foreach( var map in liftedMap.ObjectMappings )
            {
                _rootCtor.Append( "_objectServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", _stObjs[" ).Append( map.Value.IndexOrdered ).Append( "].FinalImplementation );" )
                       .NewLine();
            }
            if( liftedMap.ObjectMappingList.Count > 0 )
            {
                _rootCtor.Append( $"_objectServiceMappingList = new IStObjFinalImplementation[] {{" ).NewLine();
                foreach( var o in liftedMap.ObjectMappingList )
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
            _rootCtor.Append( $"_simpleServiceList = " );
            AppendArrayDecl( _rootCtor, nameof( IStObjServiceClassDescriptor ), liftedMap.SimpleMappingList.Count );
            foreach( var d in liftedMap.SimpleMappingList )
            {
                Debug.Assert( d.SimpleMappingIndex >= 0 );
                _rootCtor.Append( "_simpleServiceList[" ).Append( d.SimpleMappingIndex ).Append( "] = new StObjServiceClassDescriptor(" )
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

            _rootCtor.Append( $"_simpleServiceMappings = new Dictionary<Type, IStObjServiceClassDescriptor>({liftedMap.SimpleMappings.Count});" ).NewLine();
            foreach( var map in liftedMap.SimpleMappings )
            {
                _rootCtor.Append( $"_simpleServiceMappings.Add( " )
                            .AppendTypeOf( map.Key )
                            .Append( ", " )
                            .Append( "_simpleServiceList[" ).Append( map.Value.SimpleMappingIndex ).Append("] );")
                            .NewLine();
            }
        }

        public void CreateConfigureServiceMethod( IReadOnlyList<IStObjResult> orderedStObjs )
        {
            _rootType.GeneratedByComment().NewLine();
            var configure = _rootType.CreateFunction( "void IStObjObjectMap.ConfigureServices( in StObjContextRoot.ServiceRegister register )" );

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

        public void CreateFillRealObjectMappingsMethod()
        {
            _rootType.GeneratedByComment().NewLine()
                     .Append( """
                        void FillRealObjectMappings( IActivityMonitor monitor,
                                                     Microsoft.Extensions.DependencyInjection.IServiceCollection global,
                                                     Microsoft.Extensions.DependencyInjection.IServiceCollection? commonEndpoint )
                        {
                            foreach( var o in _finalStObjs )
                            {
                                if( o.ClassType != o.FinalType )
                                {
                                    monitor.Trace( $"Registering real object '{o.ClassType.Name}' to its code generated implementation instance." );
                                }
                                else
                                {
                                    monitor.Trace( $"Registering real object '{o.ClassType.Name}' instance." );
                                }
                                var typeMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( o.ClassType, o.Implementation );
                                global.Add( typeMapping );
                                commonEndpoint?.Add( typeMapping );
                                foreach( var unique in o.UniqueMappings )
                                {
                                    monitor.Trace( $"Registering unique mapping from '{unique.Name}' to real object '{o.ClassType.Name}' instance." );
                                    var uMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( unique, o.Implementation );
                                    global.Add( uMapping );
                                    commonEndpoint?.Add( uMapping );
                                }
                                foreach( var multi in o.MultipleMappings )
                                {
                                    monitor.Trace( $"Registering multiple mapping from '{multi.Name}' to real object '{o.ClassType.Name}' instance." );
                                    var mMapping = new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( multi, o.Implementation );
                                    global.Add( mMapping );
                                    commonEndpoint?.Add( mMapping );
                                }
                            }
                        }
                        """ );
        }

    }
}
