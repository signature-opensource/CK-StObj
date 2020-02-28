using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    static class EEEEEEE
    {
        /// <summary>
        /// Creates a segment of code inside this function.
        /// </summary>
        /// <typeparam name="T">The function scope type.</typeparam>
        /// <param name="this">This function scope.</param>
        /// <param name="part">The function part to use to inject code at this location (or at the top).</param>
        /// <param name="top">Optionally creates the new part at the start of the code instead of at the current writing position in the code.</param>
        /// <returns>This function scope writer to enable fluent syntax.</returns>
        public static T CreatePart<T>( this T @this, out IFunctionScopePart part, bool top = false ) where T : IFunctionScope
        {
            part = @this.CreatePart( top );
            return @this;
        }

        /// <summary>
        /// Creates a segment of code inside this namespace.
        /// </summary>
        /// <typeparam name="T">The namespace scope type.</typeparam>
        /// <param name="this">This namespace scope.</param>
        /// <param name="part">The namespace part to use to inject code at this location (or at the top).</param>
        /// <param name="top">Optionally creates the new part at the start of the code instead of at the current writing position in the code.</param>
        /// <returns>This namespace scope writer to enable fluent syntax.</returns>
        public static T CreatePart<T>( this T @this, out INamespaceScopePart part, bool top = false ) where T : INamespaceScope
        {
            part = @this.CreatePart( top );
            return @this;
        }

        /// <summary>
        /// Creates a segment of code inside this type.
        /// </summary>
        /// <typeparam name="T">The type scope type.</typeparam>
        /// <param name="this">This type scope.</param>
        /// <param name="part">The type part to use to inject code at this location (or at the top).</param>
        /// <param name="top">Optionally creates the new part at the start of the code instead of at the current writing position in the code.</param>
        /// <returns>This type scope writer to enable fluent syntax.</returns>
        public static T CreatePart<T>( this T @this, out ITypeScopePart part, bool top = false ) where T : ITypeScope
        {
            part = @this.CreatePart( top );
            return @this;
        }

        /// <summary>
        /// Fluent function application.
        /// </summary>
        /// <typeparam name="T">Actual type of the code writer.</typeparam>
        /// <param name="this">This code writer.</param>
        /// <param name="f">Fluent function to apply.</param>
        /// <returns>This code writer to enable fluent syntax.</returns>
        public static T Apply<T>( this T @this, Func<T, T> f ) where T : ICodeWriter => f( @this );

        /// <summary>
        /// Fluent action application.
        /// </summary>
        /// <typeparam name="T">Actual type of the code writer.</typeparam>
        /// <param name="this">This code writer.</param>
        /// <param name="f">Actio to apply to this code writer.</param>
        /// <returns>This code writer to enable fluent syntax.</returns>
        public static T Apply<T>( this T @this, Action<T> f ) where T : ICodeWriter
        {
            f( @this );
            return @this;
        }

    }


    class ServiceSupportCodeGenerator
    {
        static readonly string _sourceServiceSupport = @"
        public sealed class StObjServiceParameterInfo : IStObjServiceParameterInfo
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

        public sealed class StObjServiceClassFactoryInfo : IStObjServiceClassFactoryInfo
        {
            public StObjServiceClassFactoryInfo( Type t, IReadOnlyList<IStObjServiceParameterInfo> a, bool s, FrontServiceKind k, IReadOnlyCollection<Type> marshallableTypes )
            {
                ClassType = t;
                Assignments = a;
                IsScoped = s;
                FrontServiceKind = k;
                MarshallableFrontServiceTypes = marshallableTypes;
            }

            public Type ClassType { get; }

            public bool IsScoped { get; }

            public FrontServiceKind FrontServiceKind { get; }

            public IReadOnlyList<IStObjServiceParameterInfo> Assignments { get; }

            public IReadOnlyCollection<Type> MarshallableFrontServiceTypes { get; }

        }

        public sealed class StObjServiceClassDescriptor : IStObjServiceClassDescriptor
        {
            public StObjServiceClassDescriptor( Type t, bool s, FrontServiceKind k, IReadOnlyCollection<Type> marshallableTypes )
            {
                ClassType = t;
                IsScoped = s;
                FrontServiceKind = k;
                MarshallableFrontServiceTypes = marshallableTypes;
           }

            public Type ClassType { get; }

            public bool IsScoped { get; }

            public FrontServiceKind FrontServiceKind { get; }

            public IReadOnlyCollection<Type> MarshallableFrontServiceTypes { get; }

        }
";
        readonly ITypeScope _rootType;
        readonly IFunctionScope _rootCtor;
        readonly ITypeScope _infoType;
        readonly Dictionary<IStObjServiceClassFactoryInfo, string> _names;

        public ServiceSupportCodeGenerator( ITypeScope rootType, IFunctionScope rootCtor )
        {
            _rootType = rootType;
            _infoType = rootType.Namespace.CreateType( "public static class SFInfo" );
            _rootCtor = rootCtor;
            _names = new Dictionary<IStObjServiceClassFactoryInfo, string>();
        }

        public void CreateServiceSupportCode( StObjObjectEngineMap liftedMap )
        {
            _infoType.Namespace.Append( _sourceServiceSupport );

            _rootType.Append( @"
Dictionary<Type, object> _objectServiceMappings;
Dictionary<Type, IStObjServiceClassDescriptor> _simpleServiceMappings;
Dictionary<Type, IStObjServiceClassFactory> _manualServiceMappings;

public IStObjServiceMap Services => this;
IReadOnlyDictionary<Type, object> IStObjServiceMap.ObjectMappings => _objectServiceMappings;
IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappings => _simpleServiceMappings;
IReadOnlyDictionary<Type, IStObjServiceClassFactory> IStObjServiceMap.ManualMappings => _manualServiceMappings;" )
                     .NewLine();

            // Object mappings.
            _rootCtor.Append( $"_objectServiceMappings = new Dictionary<Type, object>({liftedMap.ObjectMappings.Count});" ).NewLine();
            foreach( var map in liftedMap.ObjectMappings )
            {
                _rootCtor.Append( $"_objectServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", _stObjs[" ).Append( map.Value.IndexOrdered ).Append( "].Instance );" )
                       .NewLine();
            }
            // Service mappings (Simple).
            _rootCtor.Append( $"_simpleServiceMappings = new Dictionary<Type, IStObjServiceClassDescriptor>({liftedMap.ServiceSimpleMappings.Count});" ).NewLine();
            foreach( var map in liftedMap.ServiceSimpleMappings )
            {
                _rootCtor.Append( $"_simpleServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", " )
                       .Append( "new StObjServiceClassDescriptor(" )
                            .AppendTypeOf( map.Value.FinalType )
                            .Append( ", " )
                            .Append( map.Value.MustBeScopedLifetime.Value )
                            .Append( ", " )
                            .Append( map.Value.FinalFrontServiceKind.Value )
                            .Append( ", " )
                                .Apply( w =>
                                {
                                    bool atLeastOne = false;
                                    foreach( var t in map.Value.MarshallableFrontServiceTypes )
                                    {
                                        if( atLeastOne ) w.Append( ", " );
                                        else
                                        {
                                            w.Append( "new[] {" );
                                            atLeastOne = true;
                                        }
                                        w.AppendTypeOf( t );
                                    }
                                    if( atLeastOne ) w.Append( "}" );
                                    else w.Append( "Type.EmptyTypes" );
                                } )
                            .Append( ")" )
                       .Append( " );" )
                       .NewLine();
            }
            // Service mappings (Not so Simple :)).
            _rootCtor.Append( $"_manualServiceMappings = new Dictionary<Type, IStObjServiceClassFactory>( {liftedMap.ServiceManualMappings.Count} );" ).NewLine();
            foreach( var map in liftedMap.ServiceManualMappings )
            {
                _rootCtor.Append( $"_manualServiceMappings.Add( " )
                       .AppendTypeOf( map.Key )
                       .Append( ", " ).Append( GetServiceClassFactoryName( map.Value ) )
                       .Append( " );" ).NewLine();
            }
            foreach( var serviceFactory in liftedMap.ServiceManualList )
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
                foreach( var reg in m.Type.AllRegisterStartupServices )
                {
                    if( reg == m.Type.RegisterStartupServices ) configure.Append( $"_stObjs[{m.IndexOrdered}].ObjectType" );
                    else configure.AppendTypeOf( reg.DeclaringType );

                    configure.Append( ".GetMethod(" )
                             .AppendSourceString( StObjContextRoot.RegisterStartupServicesMethodName )
                             .Append( ", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly )" )
                             .NewLine();
                    configure.Append( $".Invoke( _stObjs[{m.IndexOrdered}].Instance, registerParam );" )
                             .NewLine();

                }
            }
            foreach( MutableItem m in orderedStObjs )
            {
                foreach( var parameters in m.Type.AllConfigureServicesParameters )
                {
                    configure.AppendOnce( "GStObj s;" ).NewLine();
                    configure.AppendOnce( "MethodInfo m;" ).NewLine();

                    configure.Append( $"s = _stObjs[{m.IndexOrdered}];" ).NewLine();

                    configure.Append( "m = " );
                    if( parameters == m.Type.ConfigureServicesParameters )
                        configure.Append( "s.ObjectType" );
                    else configure.AppendTypeOf( parameters[0].Member.DeclaringType );

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
                    configure.Append( "m.Invoke( s.Instance, new object[]{ register" );
                    foreach( var p in parameters.Skip( 1 ) )
                    {
                        configure.Append( $", Resolve<" ).AppendCSharpName( p.ParameterType, false ).Append( ">(" ).Append( p.HasDefaultValue ).Append( ')' );
                    }
                    configure.Append( " } );" ).NewLine();
                }
            }

        }

        string GetServiceClassFactoryName( IStObjServiceFinalManualMapping f ) => $"SFInfo.S{f.Number}.Default";

        void CreateServiceClassFactory( IStObjServiceFinalManualMapping c )
        {
            var t = _infoType.CreateType( $"public class S{c.Number} : StObjServiceClassFactoryInfo, IStObjServiceClassFactory" );

            t.CreateFunction( ctor =>
            {
                ctor.Append( "public S" ).Append( c.Number ).Append( "()" ).NewLine()
                    .Append( ": base( " ).AppendTypeOf( c.ClassType ).Append( ", " ).NewLine();
                GenerateStObjServiceFactortInfoAssignments( ctor, c.Assignments );
                ctor.Append( ", " ).Append( c.IsScoped ).Append( ", FrontServiceKind." ).Append( c.FrontServiceKind ).Append( ")" );
            } );

            t.CreateFunction( func =>
            {
                func.Append( "public object CreateInstance( IServiceProvider p ) {" );
                func.Append( "return new " ).AppendCSharpName( c.ClassType ).Append( "(" );
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
            t.Append( "public static readonly IStObjServiceClassFactory Default = new S" ).Append( c.Number ).Append( "();" ).NewLine();
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
