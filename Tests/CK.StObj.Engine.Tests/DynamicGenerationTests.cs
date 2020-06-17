using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjEngine;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Internal;
using SmartAnalyzers.CSharpExtensions.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static CK.Testing.StObjEngineTestHelper;


namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    [Category( "DynamicGeneration" )]
    public class DynamicGenerationTests
    {
        public class CSimpleEmit
        {
            class AutoImplementedAttribute : Attribute, IAutoImplementorMethod
            {
                public AutoImplementationResult Implement( IActivityMonitor monitor, MethodInfo m, ICodeGenerationContext c, ITypeScope b )
                {
                    b.AppendOverrideSignature( m )
                     .Append( $"=> default({m.ReturnType.FullName});" )
                     .NewLine();
                    return AutoImplementationResult.Success;
                }
            }

            public class A : IRealObject
            {
            }

            public abstract class B : A
            {
                readonly string _str;

                /// <summary>
                /// Public constructors are automatically replicated on the generated
                /// implementations.
                /// Protected ones are not automatically replicated: they may be
                /// called by generated constructors.
                /// </summary>
                /// <param name="injectableCtor"></param>
                public B( string injectableCtor )
                {
                    _str = injectableCtor;
                }

                public string InjectedString 
                { 
                    get { return _str; } 
                }

                [AutoImplemented]
                public abstract int Auto( int i );
            }

            public interface IC : IRealObject
            {
                A TheA { get; }
            }

            public class C : IC
            {
                [InjectObject]
                [InitRequired]
                public A TheA { get; private set; }
            }

            public class D : C
            {
                [AmbientProperty( IsOptional = true )]
                public string? AnOptionalString { get; private set; }
            }
            
            const string ctorParam = "Protected Ctor is called by public's finalType's constructor.";

            class StObjRuntimeBuilder : IStObjRuntimeBuilder
            {
                public object CreateInstance( Type finalType )
                {
                    if( typeof( B ).IsAssignableFrom( finalType ) ) return Activator.CreateInstance( finalType, ctorParam )!;
                    else return Activator.CreateInstance( finalType, false )!;
                }
            }

            public void DoTest()
            {
                var runtimeBuilder = new StObjRuntimeBuilder();

                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), runtimeBuilder: runtimeBuilder );
                collector.RegisterType( typeof( B ) );
                collector.RegisterType( typeof( D ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );
                var r = collector.GetResult();
                Assert.That( r.HasFatalError, Is.False );

                // no source, only compilation
                SimpleEngineRunContext.GenerateAssembly( TestHelper.Monitor, r, "TEST_SimpleEmit" );
                var a = Assembly.Load( "TEST_SimpleEmit" );
                IStObjMap? c = StObjContextRoot.Load( a, runtimeBuilder, TestHelper.Monitor );
                Debug.Assert( c != null );
                Assert.That( typeof( B ).IsAssignableFrom( c.StObjs.ToLeaf( typeof( A ) ).ClassType ) );
                Assert.That( c.StObjs.ToLeaf( typeof( IC ) ).ClassType, Is.SameAs( typeof( D ) ) );
                Assert.That( c.StObjs.Obtain<B>().Auto( 3 ), Is.EqualTo( 0 ) );
                Assert.That( c.StObjs.Obtain<B>().InjectedString, Is.EqualTo( ctorParam ) );
            }

        }

        [Test]
        public void SimpleEmit()
        {
            new CSimpleEmit().DoTest();
        }

        public class CConstructCalledAndStObjProperties
        {
            public class A : IRealObject
            {
                [StObjProperty]
                public string? StObjPower { get; set; }

                void StObjConstruct( IActivityMonitor monitor )
                {
                    monitor.Trace( $"At A level: StObjPower = '{StObjPower}'." );
                }
            }

            public abstract class ASpec : A
            {
                [StObjProperty]
                new public string? StObjPower { get; set; }

                void StObjConstruct( IActivityMonitor monitor, B b )
                {
                    monitor.Trace( $"At ASpec level: StObjPower = '{StObjPower}'." );
                    TheB = b;
                }

                [InitRequired]
                public B TheB { get; private set; }
            }

            public class B : IRealObject
            {
                void StObjConstruct( A a )
                {
                    TheA = a;
                }

                [InitRequired]
                public A TheA { get; private set; }
            }

            class StObjPropertyConfigurator : IStObjStructuralConfigurator
            {
                public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
                {
                    if( o.ClassType == typeof( A ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "This is the A property." );
                    if( o.ClassType == typeof( ASpec ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "ASpec level property." );
                }
            }

            public void DoTest()
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new StObjPropertyConfigurator() );
                collector.RegisterType( typeof( B ) );
                collector.RegisterType( typeof( ASpec ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );
                var r = collector.GetResult();
                {
                    Assert.That( r.HasFatalError, Is.False );
                    Debug.Assert( r.EngineMap != null, "Since HasFatalError is false." );
                    IStObjObjectEngineMap stObjs = r.EngineMap.StObjs;

                    Assert.That( stObjs.Obtain<B>().TheA, Is.SameAs( stObjs.Obtain<A>() ).And.SameAs( stObjs.Obtain<ASpec>() ) );
                    Assert.That( stObjs.Obtain<ASpec>().TheB, Is.SameAs( stObjs.Obtain<B>() ) );
                    Assert.That( stObjs.ToHead( typeof( A ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( stObjs.ToHead( typeof( ASpec ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "ASpec level property." ) );

                    ASpec theA = (ASpec)stObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                }

                SimpleEngineRunContext.GenerateAssembly( TestHelper.Monitor, r, "TEST_ConstructCalled" );
                {
                    var a = Assembly.Load( "TEST_ConstructCalled" );
                    IStObjMap? c = StObjContextRoot.Load( a, StObjContextRoot.DefaultStObjRuntimeBuilder, TestHelper.Monitor );
                    Debug.Assert( c != null );
                    c.Should().NotBeNull();
                    Assert.That( c.StObjs.Obtain<B>().TheA, Is.SameAs( c.StObjs.Obtain<A>() ).And.SameAs( c.StObjs.Obtain<ASpec>() ) );
                    Assert.That( c.StObjs.Obtain<ASpec>().TheB, Is.SameAs( c.StObjs.Obtain<B>() ) );

                    ASpec theA = (ASpec)c.StObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                }
            }

        }

        [Test]
        public void ConstructCalledAndStObjProperties()
        {
            new CConstructCalledAndStObjProperties().DoTest();
        }

        public class PostBuildSet
        {
            public class A : IRealObject
            {
                [StObjProperty]
                public string? StObjPower { get; private set; }

                /// <summary>
                /// StObjInitialize is NOT called on setup instances.
                /// </summary>
                public bool StObjInitializeOnACalled; 

                void StObjConstruct( IActivityMonitor monitor, [Container]BSpec bIsTheContainerOfA )
                {
                    monitor.Trace( $"At A level: StObjPower = '{StObjPower}'." );
                }

                void StObjInitialize( IActivityMonitor monitor, IStObjObjectMap map )
                {
                    map.FinalImplementations.Count( f => f.Implementation is IRealObject ).Should().Be( 2 );
                    StObjInitializeOnACalled = true;
                }

                [InjectObject]
                [InitRequired]
                public BSpec TheB { get; private set; }
            }

            public abstract class ASpec : A
            {
                [StObjProperty]
                new public string? StObjPower { get; set; }

                public bool StObjInitializeOnASpecCalled;

                void StObjConstruct( IActivityMonitor monitor )
                {
                    monitor.Trace( $"At ASpec level: StObjPower = '{StObjPower}'." );
                }

                void StObjInitialize( IActivityMonitor monitor, IStObjObjectMap map )
                {
                    map.FinalImplementations.Count( f => f.Implementation is IRealObject ).Should().Be( 2 );
                    Assert.That( StObjInitializeOnACalled );
                    StObjInitializeOnASpecCalled = true;
                }
            }

            [StObj( ItemKind = DependentItemKindSpec.Container )]
            public class B : IRealObject
            {
                [InjectObject]
                [InitRequired]
                public A TheA { get; private set; }

                [InjectObject]
                [InitRequired]
                public A TheInjectedA { get; private set; }
            }

            public abstract class BSpec : B
            {
                void StObjConstruct( )
                {
                }

            }

            /// <summary>
            /// Configures the 2 A's StObjPower with "This is the A property." (for A) and "ASpec level property." (for ASpec).
            /// </summary>
            class StObjPropertyConfigurator : IStObjStructuralConfigurator
            {
                public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
                {
                    if( o.ClassType == typeof( A ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "This is the A property." );
                    if( o.ClassType == typeof( ASpec ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "ASpec level property." );
                }
            }

            public void DoTest()
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new StObjPropertyConfigurator() );
                collector.RegisterType( typeof( BSpec ) );
                collector.RegisterType( typeof( ASpec ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );
                var r = collector.GetResult();
                {
                    Assert.That( r.HasFatalError, Is.False );
                    Debug.Assert( r.EngineMap != null, "Since HasFatalError is false." );
                    IStObjObjectEngineMap stObjs = r.EngineMap.StObjs;

                    Assert.That( stObjs.Obtain<B>().TheA, Is.SameAs( stObjs.Obtain<A>() ).And.SameAs( stObjs.Obtain<ASpec>() ) );
                    Assert.That( stObjs.Obtain<ASpec>().TheB, Is.SameAs( stObjs.Obtain<B>() ) );
                    Assert.That( stObjs.ToHead( typeof( A ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( stObjs.ToHead( typeof( ASpec ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "ASpec level property." ) );

                    ASpec theA = (ASpec)stObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( theA.StObjInitializeOnACalled, Is.False, "StObjInitialize is NOT called on setup instances." );
                }

                SimpleEngineRunContext.GenerateAssembly( TestHelper.Monitor, r, "TEST_PostBuildSet" );
                {
                    var a = Assembly.Load( "TEST_PostBuildSet" );
                    IStObjMap? c = StObjContextRoot.Load( a, StObjContextRoot.DefaultStObjRuntimeBuilder, TestHelper.Monitor );
                    c.Should().NotBeNull();
                    Debug.Assert( c != null );
                    Assert.That( c.StObjs.Obtain<B>().TheA, Is.SameAs( c.StObjs.Obtain<A>() ).And.SameAs( c.StObjs.Obtain<ASpec>() ) );
                    Assert.That( c.StObjs.Obtain<ASpec>().TheB, Is.SameAs( c.StObjs.Obtain<B>() ) );

                    ASpec theA = (ASpec)c.StObjs.Obtain<A>();
                    theA.Should().NotBeNull();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );

                    Assert.That( theA.TheB, Is.SameAs( c.StObjs.Obtain<B>() ) );
                    Assert.That( c.StObjs.Obtain<B>().TheInjectedA, Is.SameAs( theA ) );

                    Assert.That( theA.StObjInitializeOnACalled, Is.True );
                    Assert.That( theA.StObjInitializeOnASpecCalled, Is.True );

                }
            }

        }

        [Test]
        public void PostBuildAndInjectObjects()
        {
            new PostBuildSet().DoTest();
        }


        public class CTypeImplementor
        {
            /// <summary>
            /// Actual implementation that takes care of all the abstract properties.
            /// This doesn't handle abstract methods at all.
            /// </summary>
            public class DefaultPropertyImplementationAttributeImpl : IAutoImplementorType, IAutoImplementorProperty
            {
                readonly DefaultPropertyImplementationAttribute _attr;

                /// <summary>
                /// The "attribute implementation" is provided with the original, ("Model" only) attribute: any configuration
                /// can be used.
                /// </summary>
                /// <param name="attr">The model layer attribute.</param>
                public DefaultPropertyImplementationAttributeImpl( DefaultPropertyImplementationAttribute attr )
                {
                    _attr = attr;
                }

                // We don't know how to handle any method.
                public IAutoImplementorMethod? HandleMethod( IActivityMonitor monitor, MethodInfo m ) => null;

                // Here we tell the engine: "I'm handling this property implementation" only if the property name starts with a 'H'.
                // This is rather stupid but this shows an easy way to enforce naming rules.
                // We could have returned a dedicated instance but instead we implement the IAutoImplementorProperty interface directly.
                public IAutoImplementorProperty? HandleProperty( IActivityMonitor monitor, PropertyInfo p ) => p.Name.StartsWith( "H" ) ? this : null;

                // We choose to implement all the properties as a whole in Implement method below: by returning true
                // we tell the engine: "Okay, I handled it, please continue your business."
                // (We can also implement each property here and do nothing in the Implement method.)
                AutoImplementationResult IAutoImplementor<PropertyInfo>.Implement( IActivityMonitor monitor, PropertyInfo p, ICodeGenerationContext c, ITypeScope typeBuilder ) => AutoImplementationResult.Success;

                public AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                {
                    foreach( var p in classType.GetProperties() )
                    {
                        scope.Append( "public override " ).Append( p.PropertyType.FullName ).Append( " " ).Append( p.Name ).Append( " => " );
                        if( typeof( int ).IsAssignableFrom( p.PropertyType ) )
                        {
                            scope.Append( _attr.Value );
                        }
                        else if( typeof( string ).IsAssignableFrom( p.PropertyType ) )
                        {
                            scope.AppendSourceString( $@"Value is ""{_attr.Value}""..." );
                        }
                        else
                        {
                            scope.Append( "default(" ).AppendCSharpName( p.PropertyType ).Append( ")" );
                        }
                        scope.Append( ";" ).NewLine();
                    }
                    return AutoImplementationResult.Success;
                }
            }

            /// <summary>
            /// This is the atribute that will trigger the abstract properties implementation.
            /// This is a very small attribute that does nothing else than redirecting to the
            /// actual implementation that must be in a ".Runtime" or ".Engine" assembly.
            /// <para>
            /// This attribute accepts a parameter name value that will drive/configure the code generation.
            /// </para>
            /// </summary>
            [AttributeUsage( AttributeTargets.Class )]
            public class DefaultPropertyImplementationAttribute : ContextBoundDelegationAttribute
            {
                public DefaultPropertyImplementationAttribute( int value )
                    : base( "CK.StObj.Engine.Tests.DynamicGenerationTests+CTypeImplementor+DefaultPropertyImplementationAttributeImpl, CK.StObj.Engine.Tests" )
                {
                    Value = value;
                }

                public int Value { get; }
            }

            /// <summary>
            /// This auto service is abstract and its abstract properties will be implemented
            /// "by the attribute".
            /// </summary>
            [DefaultPropertyImplementation( 3712 )]
            public abstract class AutomaticallyImplemented : IAutoService
            {
                /// <summary>
                /// AutoServices MUST have a single public constructor.
                /// </summary>
                public AutomaticallyImplemented()
                {
                }

                public abstract string Hip { get; }
                public abstract int Hop { get; }
                public abstract double Hup { get; }
            }

            public void DoTest()
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( AutomaticallyImplemented ) );
                var r = collector.GetResult();
                r.HasFatalError.Should().BeFalse();
                SimpleEngineRunContext.GenerateAssembly( TestHelper.Monitor, r, "TEST_TypeImplementor" );
                var a = Assembly.Load( "TEST_TypeImplementor" );
                Type generated = a.GetTypes().Single( t => t.IsClass && typeof( AutomaticallyImplemented ).IsAssignableFrom( t ) );
                AutomaticallyImplemented done = (AutomaticallyImplemented)Activator.CreateInstance( generated )!;
                done.Hip.Should().Be( "Value is \"3712\"..." );
                done.Hop.Should().Be( 3712 );
                done.Hup.Should().Be( 0.0 );
            }

        }

        [Test]
        public void IAutoImplementorType_implements_interface()
        {
            new CTypeImplementor().DoTest();
        }

        public class ContextBoundDelegationAttributeDI
        {
            public class AttributeImpl 
            {
                // Attributes can depend on any service registered in the root initial service container.
                // Aspects typically configure this container (The SqlServer aspect for instance publishes
                // a Sql database context with the default database and secondary databases).
                public AttributeImpl( IServiceProvider p, Func<string> hello )
                {
                    TestHelper.Monitor.Info( hello );
                }
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+ContextBoundDelegationAttributeDI+AttributeImpl, CK.StObj.Engine.Tests" )]
            public abstract class JustForTheAttribute : IAutoService
            {
            }

            public void DoTest()
            {
                var extraServices = new SimpleServiceContainer();
                extraServices.Add<Func<string>>( () => "Hello World!" );

                StObjCollector collector = new StObjCollector( TestHelper.Monitor, extraServices );
                collector.RegisterType( typeof( JustForTheAttribute ) );
                var r = collector.GetResult();
                Assert.That( r.HasFatalError, Is.False );
            }

        }

        [Test]
        public void ContextBoundDelegation_dependency_injection()
        {
            new ContextBoundDelegationAttributeDI().DoTest();
        }

        public class SecondPassCodeGenerationDI
        {
            public interface ISourceCodeHelper1
            {
                string IHelpTheCodeGeneration();
            }

            public class AutoImpl1 : AutoImplementorType
            {
                class SourceCodeHelper1 : ISourceCodeHelper1
                {
                    public string IHelpTheCodeGeneration() => "I'm great!";
                }

                public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                {
                    var helper = new SourceCodeHelper1();
                    c.CurrentRun.ServiceContainer.Add( helper );
                    c.CurrentRun.ServiceContainer.Add<ISourceCodeHelper1>( helper );
                    return new AutoImplementationResult( typeof( ActualImpl1 ) );
                }

                class ActualImpl1 : AutoImplementorType
                {
                    readonly SourceCodeHelper1 _h1;
                    readonly SourceCodeHelper2 _h2;
                    readonly AutoImpl1 _theOwner;

                    // The implementor type can rely on its creator (here it has access to the AutoImpl1 and, as a nested type,
                    // it can access its private fields).
                    public ActualImpl1( AutoImpl1 theOwner, SourceCodeHelper1 h1, SourceCodeHelper2 h2 )
                    {
                        _theOwner = theOwner;
                        _h1 = h1;
                        _h2 = h2;
                    }

                    public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                    {
                        _theOwner.Should().NotBeNull();
                        monitor.Info( $"ActualImpl1: {_h1.IHelpTheCodeGeneration()}, {_h2.IAlsoHelpTheCodeGeneration()}." );
                        return AutoImplementationResult.Success;
                    }
                }
            }

            public class SourceCodeHelper2
            {
                public string IAlsoHelpTheCodeGeneration() => "I'm SOOOO great!";
            }

            public class SourceCodeHelper3
            {
                public string ICannotHelpTheCodeGeneration() => "Because nobody added me :'(.";
            }

            public class AutoImpl2 : AutoImplementorType
            {
                public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                {
                    c.CurrentRun.ServiceContainer.Add( new SourceCodeHelper2() );
                    return new AutoImplementationResult( typeof( ActualImpl2 ) );
                }

                public class ActualImpl2 : AutoImplementorType
                {
                    readonly ISourceCodeHelper1 _h1;
                    readonly SourceCodeHelper2 _h2;

                    public ActualImpl2( ISourceCodeHelper1 h1, SourceCodeHelper2 h2, SourceCodeHelper3? h3 = null )
                    {
                        _h1 = h1;
                        _h2 = h2;
                    }

                    public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                    {
                        monitor.Info( $"ActualImpl2: {_h1.IHelpTheCodeGeneration()}, {_h2.IAlsoHelpTheCodeGeneration()}." );
                        return AutoImplementationResult.Success;
                    }
                }
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+SecondPassCodeGenerationDI+AutoImpl1, CK.StObj.Engine.Tests" )]
            public abstract class S1 : IAutoService
            {
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+SecondPassCodeGenerationDI+AutoImpl2, CK.StObj.Engine.Tests" )]
            public abstract class S2 : IAutoService
            {
            }

            public void DoTest()
            {
                IReadOnlyList<ActivityMonitorSimpleCollector.Entry>? logs = null;
                using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Trace, 1000 ) )
                {
                    StObjCollector collector = TestHelper.CreateStObjCollector( typeof( S1 ), typeof( S2 ) );
                    TestHelper.GenerateCode( collector ).CodeGenResult.Success.Should().BeTrue();
                }
                logs.Should().Contain( e => e.Text == "ActualImpl1: I'm great!, I'm SOOOO great!." );
                logs.Should().Contain( e => e.Text == "ActualImpl2: I'm great!, I'm SOOOO great!." );
            }

        }

        [Test]
        public void SecondPass_code_generation_can_use_dependency_injection()
        {
            new SecondPassCodeGenerationDI().DoTest();
        }

        public class SecondPassCodeGenerationParameterInjectionDI
        {
            public interface ISourceCodeHelper
            {
                string IHelpTheCodeGeneration();
            }

            public class AutoImpl1 : AutoImplementorType
            {
                class SourceCodeHelper : ISourceCodeHelper
                {
                    public string IHelpTheCodeGeneration() => "I'm great!";
                }

                public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                {
                    c.CurrentRun.ServiceContainer.Add<ISourceCodeHelper>( new SourceCodeHelper() );
                    return AutoImplementationResult.Success;
                }
            }

            public class AutoImpl2 : AutoImplementorType
            {
                public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
                {
                    return new AutoImplementationResult( nameof(DoImplement) );
                }

                void DoImplement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope, ISourceCodeHelper helper )
                {
                    c.Should().NotBeNull();
                    scope.Should().NotBeNull();
                    monitor.Info( $"AutoImpl2: {helper.IHelpTheCodeGeneration()}." );
                }
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+SecondPassCodeGenerationParameterInjectionDI+AutoImpl1, CK.StObj.Engine.Tests" )]
            public abstract class S1 : IAutoService
            {
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+SecondPassCodeGenerationParameterInjectionDI+AutoImpl2, CK.StObj.Engine.Tests" )]
            public abstract class S2 : IAutoService
            {
            }

            public void DoTest()
            {
                IReadOnlyList<ActivityMonitorSimpleCollector.Entry>? logs = null;
                using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Trace, 1000 ) )
                {
                    StObjCollector collector = TestHelper.CreateStObjCollector( typeof( S1 ), typeof( S2 ) );
                    TestHelper.GenerateCode( collector ).CodeGenResult.Success.Should().BeTrue();
                }
                logs.Should().Contain( e => e.Text == "AutoImpl2: I'm great!." );
            }

        }

        [Test]
        public void SecondPass_code_generation_can_use_parameter_injection_on_another_method()
        {
            new SecondPassCodeGenerationParameterInjectionDI().DoTest();
        }


    }
}
