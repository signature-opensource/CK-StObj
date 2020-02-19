using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using static CK.Testing.MonitorTestHelper;


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
                public bool Implement(IActivityMonitor monitor, MethodInfo m, IDynamicAssembly dynamicAssembly, ITypeScope b)
                {
                    b.AppendOverrideSignature( m )
                     .Append( $"=> default({m.ReturnType.FullName});" )
                     .NewLine();
                    return true;
                }
            }

            public class A : IRealObject
            {
            }

            public abstract class B : A
            {
                readonly string _str;

                protected B( string injectableCtor )
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
                public A TheA { get; private set; }
            }

            public class D : C
            {
                [AmbientProperty( IsOptional = true )]
                public string AnOptionalString { get; private set; }
            }
            
            const string ctorParam = "Protected Ctor is called by public's finalType's constructor.";

            class StObjRuntimeBuilder : IStObjRuntimeBuilder
            {
                public object CreateInstance( Type finalType )
                {
                    if( typeof( B ).IsAssignableFrom( finalType ) ) return Activator.CreateInstance( finalType, ctorParam );
                    else return Activator.CreateInstance( finalType, false );
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

                r.GenerateFinalAssembly( TestHelper.Monitor, Path.Combine( AppContext.BaseDirectory, "TEST_SimpleEmit.dll" ), false, null, false );
                var a = Assembly.Load( "TEST_SimpleEmit" );
                IStObjMap c = StObjContextRoot.Load( a, runtimeBuilder, TestHelper.Monitor );
                Assert.That( typeof( B ).IsAssignableFrom( c.StObjs.ToLeafType( typeof( A ) ) ) );
                Assert.That( c.StObjs.ToLeafType( typeof( IC ) ), Is.SameAs( typeof( D ) ) );
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
                public string StObjPower { get; set; }

                void StObjConstruct( IActivityMonitor monitor )
                {
                    monitor.Trace( $"At A level: StObjPower = '{StObjPower}'." );
                }
            }

            public abstract class ASpec : A
            {
                [StObjProperty]
                new public string StObjPower { get; set; }

                void StObjConstruct( IActivityMonitor monitor, B b )
                {
                    monitor.Trace( $"At ASpec level: StObjPower = '{StObjPower}'." );
                    TheB = b;
                }

                public B TheB { get; private set; }
            }

            public class B : IRealObject
            {
                void StObjConstruct( A a )
                {
                    TheA = a;
                }

                public A TheA { get; private set; }
            }

            class StObjPropertyConfigurator : IStObjStructuralConfigurator
            {
                public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
                {
                    if( o.ObjectType == typeof( A ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "This is the A property." );
                    if( o.ObjectType == typeof( ASpec ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "ASpec level property." );
                }
            }

            public void DoTest()
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new StObjPropertyConfigurator() );
                collector.RegisterType( typeof( B ) );
                collector.RegisterType( typeof( ASpec ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );
                var r = collector.GetResult(  );
                {
                    Assert.That( r.HasFatalError, Is.False );

                    Assert.That( r.StObjs.Obtain<B>().TheA, Is.SameAs( r.StObjs.Obtain<A>() ).And.SameAs( r.StObjs.Obtain<ASpec>() ) );
                    Assert.That( r.StObjs.Obtain<ASpec>().TheB, Is.SameAs( r.StObjs.Obtain<B>() ) );
                    Assert.That( r.StObjs.ToStObj( typeof( A ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( r.StObjs.ToStObj( typeof( ASpec ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "ASpec level property." ) );

                    ASpec theA = (ASpec)r.StObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" ).GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                }

                r.GenerateFinalAssembly( TestHelper.Monitor, Path.Combine( AppContext.BaseDirectory, "TEST_ConstructCalled.dll" ), false, null, false );
                {
                    var a = Assembly.Load( "TEST_ConstructCalled" );
                    IStObjMap c = StObjContextRoot.Load( a, StObjContextRoot.DefaultStObjRuntimeBuilder, TestHelper.Monitor );
                    Assert.That( c.StObjs.Obtain<B>().TheA, Is.SameAs( c.StObjs.Obtain<A>() ).And.SameAs( c.StObjs.Obtain<ASpec>() ) );
                    Assert.That( c.StObjs.Obtain<ASpec>().TheB, Is.SameAs( c.StObjs.Obtain<B>() ) );

                    ASpec theA = (ASpec)c.StObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" ).GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
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
                public string StObjPower { get; private set; }

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
                    Assert.That( map.Implementations.OfType<IRealObject>().Count, Is.EqualTo( 2 ) );
                    StObjInitializeOnACalled = true;
                }

                [InjectObject]
                public BSpec TheB { get; private set; }
            }

            public abstract class ASpec : A
            {
                [StObjProperty]
                new public string StObjPower { get; set; }

                public bool StObjInitializeOnASpecCalled;

                void StObjConstruct( IActivityMonitor monitor )
                {
                    monitor.Trace( $"At ASpec level: StObjPower = '{StObjPower}'." );
                }

                void StObjInitialize( IActivityMonitor monitor, IStObjObjectMap map )
                {
                    Assert.That( map.Implementations.OfType<IRealObject>().Count, Is.EqualTo( 2 ) );
                    Assert.That( StObjInitializeOnACalled );
                    StObjInitializeOnASpecCalled = true;
                }

            }

            [StObj( ItemKind = DependentItemKindSpec.Container )]
            public class B : IRealObject
            {
                [InjectObject]
                public A TheA { get; private set; }

                [InjectObject]
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
                    if( o.ObjectType == typeof( A ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "This is the A property." );
                    if( o.ObjectType == typeof( ASpec ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "ASpec level property." );
                }
            }

            public void DoTest()
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new StObjPropertyConfigurator() );
                collector.RegisterType( typeof( BSpec ) );
                collector.RegisterType( typeof( ASpec ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );
                var r = collector.GetResult(  );
                {
                    Assert.That( r.HasFatalError, Is.False );

                    Assert.That( r.StObjs.Obtain<B>().TheA, Is.SameAs( r.StObjs.Obtain<A>() ).And.SameAs( r.StObjs.Obtain<ASpec>() ) );
                    Assert.That( r.StObjs.Obtain<ASpec>().TheB, Is.SameAs( r.StObjs.Obtain<B>() ) );
                    Assert.That( r.StObjs.ToStObj( typeof( A ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( r.StObjs.ToStObj( typeof( ASpec ) ).GetStObjProperty( "StObjPower" ), Is.EqualTo( "ASpec level property." ) );

                    ASpec theA = (ASpec)r.StObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" ).GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( theA.StObjInitializeOnACalled, Is.False, "StObjInitialize is NOT called on setup instances." );

                }

                StObjCollectorResult.CodeGenerateResult genResult = r.GenerateFinalAssembly( TestHelper.Monitor, Path.Combine( AppContext.BaseDirectory, "TEST_PostBuildSet.dll" ), false, null, false );
                Assert.That( genResult.Success );
                {
                    var a = Assembly.Load( "TEST_PostBuildSet" );
                    IStObjMap c = StObjContextRoot.Load( a, StObjContextRoot.DefaultStObjRuntimeBuilder, TestHelper.Monitor );
                    Assert.That( c.StObjs.Obtain<B>().TheA, Is.SameAs( c.StObjs.Obtain<A>() ).And.SameAs( c.StObjs.Obtain<ASpec>() ) );
                    Assert.That( c.StObjs.Obtain<ASpec>().TheB, Is.SameAs( c.StObjs.Obtain<B>() ) );

                    ASpec theA = (ASpec)c.StObjs.Obtain<A>();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" ).GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );

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



    }
}
