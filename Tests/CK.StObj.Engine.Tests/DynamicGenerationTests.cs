using CK.CodeGen;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjEngine;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0051 // Remove unused private members


namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    [Category( "DynamicGeneration" )]
    public class DynamicGenerationTests
    {
        public static class CConstructCalledAndStObjProperties
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
                    if( o.ClassType == typeof( A ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "This is the A property." );
                    if( o.ClassType == typeof( ASpec ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "ASpec level property." );
                }
            }

            public static void DoTest()
            {
                using var container = new SimpleServiceContainer();
                StObjCollector collector = new StObjCollector( container, configurator: new StObjPropertyConfigurator() );
                collector.RegisterType( TestHelper.Monitor, typeof( B ) );
                collector.RegisterType( TestHelper.Monitor, typeof( ASpec ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );

                var (r,map) = TestHelper.CompileAndLoadStObjMap( collector );

                // Check collector result.
                {
                    Assert.That( r.HasFatalError, Is.False );
                    Debug.Assert( r.EngineMap != null, "Since HasFatalError is false." );
                    IStObjObjectEngineMap stObjs = r.EngineMap.StObjs;

                    Assert.That( stObjs.Obtain<B>()!.TheA, Is.SameAs( stObjs.Obtain<A>() ).And.SameAs( stObjs.Obtain<ASpec>() ) );
                    Assert.That( stObjs.Obtain<ASpec>()!.TheB, Is.SameAs( stObjs.Obtain<B>() ) );
                    Assert.That( stObjs.ToHead( typeof( A ) )!.GetStObjProperty( "StObjPower" ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( stObjs.ToHead( typeof( ASpec ) )!.GetStObjProperty( "StObjPower" ), Is.EqualTo( "ASpec level property." ) );

                    ASpec theA = (ASpec)stObjs.Obtain<A>()!;
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                }

                // Check compiled StObjMap.
                {
                    Assert.That( map.StObjs.Obtain<B>()!.TheA, Is.SameAs( map.StObjs.Obtain<A>() ).And.SameAs( map.StObjs.Obtain<ASpec>() ) );
                    Assert.That( map.StObjs.Obtain<ASpec>()!.TheB, Is.SameAs( map.StObjs.Obtain<B>() ) );

                    ASpec theA = (ASpec)map.StObjs.Obtain<A>()!;
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                }
            }

        }

        [Test]
        public void ConstructCalledAndStObjProperties()
        {
            CConstructCalledAndStObjProperties.DoTest();
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
                    StObjInitializeOnACalled = true;
                }

                [InjectObject]
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
                    if( o.ClassType == typeof( A ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "This is the A property." );
                    if( o.ClassType == typeof( ASpec ) ) o.SetStObjPropertyValue( monitor, "StObjPower", "ASpec level property." );
                }
            }

            public void DoTest()
            {
                StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: new StObjPropertyConfigurator() );
                collector.RegisterType( TestHelper.Monitor, typeof( BSpec ) );
                collector.RegisterType( TestHelper.Monitor, typeof( ASpec ) );
                collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
                collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );

                var (r, map) = TestHelper.CompileAndLoadStObjMap( collector );

                // Check collector result.
                {
                    Assert.That( r.HasFatalError, Is.False );
                    Debug.Assert( r.EngineMap != null, "Since HasFatalError is false." );
                    IStObjObjectEngineMap stObjs = r.EngineMap.StObjs;

                    Assert.That( stObjs.Obtain<B>()!.TheA, Is.SameAs( stObjs.Obtain<A>() ).And.SameAs( stObjs.Obtain<ASpec>() ) );
                    Assert.That( stObjs.Obtain<ASpec>()!.TheB, Is.SameAs( stObjs.Obtain<B>() ) );
                    Assert.That( stObjs.ToHead( typeof( A ) )!.GetStObjProperty( "StObjPower" ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( stObjs.ToHead( typeof( ASpec ) )!.GetStObjProperty( "StObjPower" ), Is.EqualTo( "ASpec level property." ) );

                    ASpec theA = (ASpec)stObjs.Obtain<A>()!;
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );
                    Assert.That( theA.StObjInitializeOnACalled, Is.False, "StObjInitialize is NOT called on setup instances." );
                }

                // Check generated StObjMap.
                {
                    Debug.Assert( map != null );
                    Assert.That( map.StObjs.Obtain<B>()!.TheA, Is.SameAs( map.StObjs.Obtain<A>() ).And.SameAs( map.StObjs.Obtain<ASpec>() ) );
                    Assert.That( map.StObjs.Obtain<ASpec>()!.TheB, Is.SameAs( map.StObjs.Obtain<B>() ) );

                    ASpec theA = (ASpec)map.StObjs.Obtain<A>()!;
                    theA.Should().NotBeNull();
                    Assert.That( theA.StObjPower, Is.EqualTo( "ASpec level property." ) );
                    Assert.That( typeof( A ).GetProperty( "StObjPower" )?.GetValue( theA, null ), Is.EqualTo( "This is the A property." ) );

                    Assert.That( theA.TheB, Is.SameAs( map.StObjs.Obtain<B>() ) );
                    Assert.That( map.StObjs.Obtain<B>()!.TheInjectedA, Is.SameAs( theA ) );

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
            public class DefaultPropertyImplementationAttributeImpl : ICSCodeGeneratorType, IAutoImplementorProperty
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
                public IAutoImplementorProperty? HandleProperty( IActivityMonitor monitor, PropertyInfo p ) => p.Name.StartsWith( "H", StringComparison.Ordinal ) ? this : null;

                // We choose to implement all the properties as a whole in Implement method below: by returning CSCodeGenerationResult.Success
                // we tell the engine: "Okay, I handled it, please continue your business."
                // (We can also implement each property here and do nothing in the Implement method.)
                CSCodeGenerationResult IAutoImplementor<PropertyInfo>.Implement( IActivityMonitor monitor, PropertyInfo p, ICSCodeGenerationContext c, ITypeScope typeBuilder ) => CSCodeGenerationResult.Success;

                public CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
                {
                    foreach( var p in classType.GetProperties() )
                    {
                        scope.Append( "public override " ).Append( p.PropertyType.FullName! ).Append( " " ).Append( p.Name ).Append( " => " );
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
                            scope.Append( "default(" ).AppendCSharpName( p.PropertyType, true, true, true ).Append( ")" );
                        }
                        scope.Append( ";" ).NewLine();
                    }
                    return CSCodeGenerationResult.Success;
                }
            }

            /// <summary>
            /// This is the attribute that will trigger the abstract properties implementation.
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
                StObjCollector collector = TestHelper.CreateStObjCollector( typeof( AutomaticallyImplemented ) );
                var map = TestHelper.CompileAndLoadStObjMap( collector ).Map;
                Debug.Assert( map != null );

                var a = map.GetType().Assembly;
                Type generated = a.GetTypes().Single( t => t.IsClass && typeof( AutomaticallyImplemented ).IsAssignableFrom( t ) );
                AutomaticallyImplemented done = (AutomaticallyImplemented)Activator.CreateInstance( generated )!;
                done.Hip.Should().Be( "Value is \"3712\"..." );
                done.Hop.Should().Be( 3712 );
                done.Hup.Should().Be( 0.0 );
            }

        }

        [Test]
        public void ICSCodeGenratorType_implements_interface()
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
                    TestHelper.Monitor.Info( hello() );
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

                StObjCollector collector = new StObjCollector( extraServices );
                collector.RegisterType( TestHelper.Monitor, typeof( JustForTheAttribute ) );
                TestHelper.GetSuccessfulResult( collector );
            }

        }

        [Test]
        public void ContextBoundDelegation_dependency_injection()
        {
            new ContextBoundDelegationAttributeDI().DoTest();
        }

        public class MultiPassCodeGenerationParameterInjectionDI
        {
            public interface ISourceCodeHelper
            {
                string IHelpTheCodeGeneration();
            }

            public class AutoImpl1 : CSCodeGeneratorType
            {
                class SourceCodeHelper : ISourceCodeHelper
                {
                    public string IHelpTheCodeGeneration() => "I'm great!";
                }

                public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
                {
                    c.CurrentRun.ServiceContainer.Add<ISourceCodeHelper>( new SourceCodeHelper() );
                    return CSCodeGenerationResult.Success;
                }
            }

            public class AutoImpl2 : CSCodeGeneratorType
            {
                public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
                {
                    return new CSCodeGenerationResult( nameof(DoImplement) );
                }

                CSCodeGenerationResult DoImplement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, ISourceCodeHelper helper )
                {
                    c.Should().NotBeNull();
                    scope.Should().NotBeNull();
                    monitor.Info( $"AutoImpl2: {helper.IHelpTheCodeGeneration()}." );
                    return new CSCodeGenerationResult( nameof( FinalizeImpl ) );
                }

                bool FinalizeImpl( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, ISourceCodeHelper helper )
                {
                    monitor.Info( $"AutoImpl in another pass: {helper.IHelpTheCodeGeneration()}." );
                    return true;
                }
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+MultiPassCodeGenerationParameterInjectionDI+AutoImpl1, CK.StObj.Engine.Tests" )]
            public abstract class S1 : IAutoService
            {
            }

            [ContextBoundDelegation( "CK.StObj.Engine.Tests.DynamicGenerationTests+MultiPassCodeGenerationParameterInjectionDI+AutoImpl2, CK.StObj.Engine.Tests" )]
            public abstract class S2 : IAutoService
            {
            }

            public void DoTest()
            {
                using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
                {
                    StObjCollector collector = TestHelper.CreateStObjCollector( typeof( S1 ), typeof( S2 ) );
                    TestHelper.CompileAndLoadStObjMap( collector ).Map.Should().NotBeNull();

                    entries.Should().Contain( e => e.Text == "AutoImpl2: I'm great!." )
                                    .And.Contain( e => e.Text == "AutoImpl in another pass: I'm great!." );
                }
            }

        }

        [Test]
        public void MultiPass_code_generation_can_use_parameter_injection()
        {
            new MultiPassCodeGenerationParameterInjectionDI().DoTest();
        }


    }
}
