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
        static readonly string _sourceServiceSupport = @"
        sealed class StObjServiceParameterInfo : IStObjServiceParameterInfo
        {
            public StObjServiceParameterInfo( Type t, int p, string n, IReadOnlyList<Type> v, bool isEnum )
            {
                ParameterType = t;
                Position = p;
                Name = n;
                Value = v;
                IsEnumerated = isEnum;
            }

            public Type ParameterType { get; }

            public int Position { get; }

            public string Name { get; }

            public bool IsEnumerated { get; }

            public IReadOnlyList<Type> Value { get; }
        }

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

        sealed class StObjServiceClassFactoryInfo : IStObjServiceClassFactoryInfo
        {
            public StObjServiceClassFactoryInfo( Type t, Type finalType, IReadOnlyList<IStObjServiceParameterInfo> a, AutoServiceKind k, IReadOnlyCollection<Type> marshallableTypes, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq )
            {
                ClassType = t;
                FinalType = finalType;
                Assignments = a;
                AutoServiceKind = k;
                MarshallableTypes = marshallableTypes;
                MultipleMappings = mult;
                UniqueMappings = uniq;
            }

            public Type ClassType { get; }

            public Type FinalType { get; }

            public bool IsScoped => (AutoServiceKind&AutoServiceKind.IsScoped) != 0;

            public AutoServiceKind AutoServiceKind { get; }

            public IReadOnlyList<IStObjServiceParameterInfo> Assignments { get; }

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
readonly Dictionary<Type, IStObjFinalImplementation> _objectServiceMappings;
readonly IStObjFinalImplementation[] _objectServiceMappingList;
readonly Dictionary<Type, IStObjServiceClassDescriptor> _simpleServiceMappings;
readonly IStObjServiceClassDescriptor[] _simpleServiceList;
readonly Dictionary<Type, IStObjServiceClassFactory> _manualServiceMappings;
readonly IStObjServiceClassFactory[] _manualServiceList;

public IStObjServiceMap Services => this;
IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => _objectServiceMappings;
IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => _objectServiceMappingList;
IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappings => _simpleServiceMappings;
IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappingList => _simpleServiceList;
IReadOnlyDictionary<Type, IStObjServiceClassFactory> IStObjServiceMap.ManualMappings => _manualServiceMappings;
IReadOnlyList<IStObjServiceClassFactory> IStObjServiceMap.ManualMappingList => _manualServiceList;" )
                     .NewLine();

            // Object mappings.
            _rootCtor.Append( $"_objectServiceMappings = new Dictionary<Type, IStObjFinalImplementation>({liftedMap.ObjectMappings.Count});" ).NewLine();
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

            // Service mappings (Simple).
            _rootCtor.Append( $"_simpleServiceList = new IStObjServiceClassDescriptor[").Append( liftedMap.SimpleMappingList.Count ).Append( "];" ).NewLine();
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
            // Service mappings (Not so Simple :)).
            _rootCtor.Append( $"_manualServiceList = new IStObjServiceClassFactory[" ).Append( liftedMap.ManualMappingList.Count ).Append( "];" ).NewLine();
            foreach( var serviceFactory in liftedMap.ManualMappingList )
            {
                CreateServiceClassFactory( serviceFactory );
                _rootCtor.Append( "_manualServiceList[" ).Append( serviceFactory.ManualMappingIndex ).Append( "] = " )
                    .Append( GetServiceClassFactoryDefaultPropertyName( serviceFactory ) ).Append( ";" ).NewLine();
            }

            _rootCtor.Append( $"_manualServiceMappings = new Dictionary<Type, IStObjServiceClassFactory>( {liftedMap.ManualMappings.Count} );" ).NewLine();
            foreach( var map in liftedMap.ManualMappings )
            {
                _rootCtor.Append( $"_manualServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", " ).Append( GetServiceClassFactoryDefaultPropertyName( map.Value ) )
                       .Append( " );" ).NewLine();
            }
            foreach( var serviceFactory in liftedMap.ManualMappingList )
            {
                CreateServiceClassFactory( serviceFactory );
            }
        }

        public void CreateConfigureServiceMethod( IReadOnlyList<IStObjResult> orderedStObjs )
        {
            var configure = _rootType.CreateFunction( "void IStObjObjectMap.ConfigureServices( in StObjContextRoot.ServiceRegister register )" );
           
            configure.Append( "register.StartupServices.Add( typeof(IStObjObjectMap), this );" ).NewLine()
                     .Append( "object[] registerParam = new object[]{ register.Monitor, register.StartupServices };" ).NewLine();

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
                        configure.Append( $", Resolve<" ).AppendCSharpName( p.ParameterType, false ).Append( ">(" ).Append( p.HasDefaultValue ).Append( ')' );
                    }
                    configure.Append( " } );" ).NewLine();
                }
            }

        }

        string GetServiceClassFactoryDefaultPropertyName( IStObjServiceFinalManualMapping f ) => $"SFInfo.S{f.ManualMappingIndex}.Default";

        void CreateServiceClassFactory( IStObjServiceFinalManualMapping c )
        {
            var t = _infoType.GeneratedByComment().CreateType( $"class S{c.ManualMappingIndex} : StObjServiceClassFactoryInfo, IStObjServiceClassFactory" );

            t.CreateFunction( ctor =>
            {
                ctor.Append( "public S" ).Append( c.ManualMappingIndex ).Append( "()" ).NewLine()
                    .Append( ": base( " )
                        .AppendTypeOf( c.ClassType ).Append( ", " ).NewLine()
                        .AppendTypeOf( c.FinalType ).Append( ", " ).NewLine()
                        .Append( x => GenerateStObjServiceFactortInfoAssignments( x, c.Assignments ) )
                        .Append( ", " ).NewLine()
                        .Append( c.AutoServiceKind )
                        .Append( ", " ).NewLine()
                        .AppendArray( c.MarshallableTypes )
                        .Append( ", " ).NewLine()
                        .AppendArray( c.MultipleMappings )
                        .Append( ")" );
            } );

            t.CreateFunction( func =>
            {
                func.Append( "public object CreateInstance( IServiceProvider p ) {" );
                func.Append( "return new " ).AppendCSharpName( c.FinalType ).Append( "(" );
                var ctor = c.GetSingleConstructor();
                var parameters = ctor.GetParameters();
                for( int i = 0; i < parameters.Length; ++i )
                {
                    var p = parameters[i];
                    if( i > 0 ) func.Append( ", " );
                    var mapped = c.Assignments.Where( a => a.Position == p.Position ).FirstOrDefault();
                    if( mapped == null )
                    {
                        func.Append( "p.GetService( " ).AppendTypeOf( p.ParameterType ).Append( ")" );
                    }
                    else
                    {
                        if( mapped.Value == null )
                        {
                            func.Append( "null" );
                        }
                        else if( mapped.IsEnumerated )
                        {
                            func.Append( "new " ).AppendCSharpName( p.ParameterType ).Append( "[]{" );
                            for( int idxType = 0; idxType < mapped.Value.Count; ++idxType )
                            {
                                if( idxType > 0 ) func.Append( ", " );
                                func.Append( "p.GetService( " ).AppendTypeOf( mapped.Value[idxType] ).Append( ")" );
                            }
                            func.Append( "}" );
                        }
                        else
                        {
                            func.Append( "p.GetService( " ).AppendTypeOf( mapped.Value[0] ).Append( ")" );
                        }
                    }
                }
                func.Append( ");" ).NewLine();
            } );
            t.Append( "public static readonly IStObjServiceClassFactory Default = new S" ).Append( c.ManualMappingIndex ).Append( "();" ).NewLine();
        }

        void GenerateStObjServiceFactortInfoAssignments( ICodeWriter b, IReadOnlyList<IStObjServiceParameterInfo> assignments )
        {
            if( assignments.Count == 0 )
            {
                b.Append( "Array.Empty<StObjServiceParameterInfo>()" );
            }
            else
            {
                b.Append( "new[]{" ).NewLine();
                bool atLeastOne = false;
                foreach( var a in assignments )
                {
                    if( atLeastOne ) b.Append( ", " );
                    atLeastOne = true;
                    b.Append( "new StObjServiceParameterInfo( " )
                     .AppendTypeOf( a.ParameterType ).Append( ", " )
                     .Append( a.Position ).Append( ", " )
                     .AppendSourceString( a.Name ).Append( ", " );
                    if( a.Value == null )
                    {
                        b.Append( "null" );
                    }
                    else
                    {
                        b.Append( "new Type[]{ " );
                        for( int idxType = 0; idxType < a.Value.Count; ++idxType )
                        {
                            if( idxType > 0 ) b.Append( ", " );
                            b.AppendTypeOf( a.Value[idxType] );
                        }
                        b.Append( "}" );
                    }
                    b.Append( ", " )
                     .Append( a.IsEnumerated )
                     .Append( ")" ).NewLine();
                }
                b.Append( '}' );
            }
        }
    }
}
