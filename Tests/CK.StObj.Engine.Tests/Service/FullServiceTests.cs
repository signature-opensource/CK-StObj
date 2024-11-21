using CK.CodeGen;
using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable IDE0051 // Remove unused private members

namespace CK.StObj.Engine.Tests.Service;

public class FullServiceTests
{
    /// <summary>
    /// This service is scoped: its implementation automatically injected
    /// by the B.ConfigureServices.
    /// </summary>
    public interface IAliceOrBobProvider
    {
        object Obtain( bool isAlice );
    }

    /// <summary>
    /// This is a IAutoService that is, by default, implemented by B: it is
    /// then a ISingletonAutoService.
    /// However, it may be implemented by a dedicated class that can perfectly be scoped as
    /// long as the new implementation either use <see cref="ReplaceAutoServiceAttribute"/> or
    /// its constructor to supersed the initial Real Object implementation.
    /// </summary>
    public interface IAutoServiceCanBeImplementedByRealObject : IAutoService
    {
        void DoSomething( IActivityMonitor m );
    }

    /// <summary>
    /// This implementation, when registered, replaces the B's one.
    /// We don't even need to specify that this one is scoped because since it depends on
    /// an unknown service IAliceOrBobProvider, it is automatically considered as being a scoped service
    /// (unless IAliceOrBobProvider is explicitly registered as a Singleton).
    /// Important: RealObject.StObjConstruct parameters are irrelevant to Service resolution.
    /// We may have use it (we almost did) but we don't. 
    /// </summary>
    [ReplaceAutoService( typeof( B ) )]
    //
    // Note that using the qualified name is valid:
    // [ReplaceAutoService( "CK.StObj.Engine.Tests.Service.StObj.FullServiceTests+B, CK.StObj.Engine.Tests" )]
    //
    // Thanks to this string based declaration, you can replace implementation ONLY IF they exist in the context:
    // the replaced target is actually optional.
    //
    public class ScopedImplementation : IAutoServiceCanBeImplementedByRealObject
    {
        readonly IAliceOrBobProvider _objectProvider;

        public ScopedImplementation( IAliceOrBobProvider objectProvider )
        {
            _objectProvider = objectProvider;
        }

        public void DoSomething( IActivityMonitor m )
        {
            var o = _objectProvider.Obtain( Environment.TickCount % 2 == 0 );
            m.Info( $"Based on IAliceOrBobProvider: I'm working with '{o}'." );
        }
    }

    /// <summary>
    /// This implementation, when registered, replaces the B's one, not because of
    /// any <see cref="ReplaceAutoServiceAttribute"/> but because B appears in the
    /// constructor's parameters.
    /// Since B is singleton, nothing prevents this implementation to be singleton (this doesn't
    /// have to be specified).
    /// </summary>
    public class SingletonImplementation : IAutoServiceCanBeImplementedByRealObject
    {
        readonly IAutoServiceCanBeImplementedByRealObject _defaultImpl;

        public SingletonImplementation( B defaultImpl )
        {
            _defaultImpl = defaultImpl;
        }

        public void DoSomething( IActivityMonitor m )
        {
            m.Info( $"I'm wrapping the default B's implementation." );
            _defaultImpl.DoSomething( m );
        }
    }

    /// <summary>
    /// A real object that depends on B and wants to substitute its implementation.
    /// </summary>
    [ReplaceAutoService( typeof( B ) )]
    public abstract class BDependency : IRealObject, IAutoServiceCanBeImplementedByRealObject
    {
        B? _theB;

        void StObjConstruct( B b )
        {
            _theB = b;
        }

        void IAutoServiceCanBeImplementedByRealObject.DoSomething( IActivityMonitor m )
        {
            m.Info( "B is no more doing something." );
        }
    }

    /// <summary>
    /// This must be registered in the startupService container since
    /// A.ConfigureServices uses it.
    /// </summary>
    public class TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem
    {
        public bool AlwaysUseAlice { get; set; }

        public bool EmitErrorLogSoThatConfigureServicesFails { get; set; }
    }

    /// <summary>
    /// ConfigureServices handles optional dependencies by using null parameter default.
    /// </summary>
    public interface IOptionalStartupService { }

    /// <summary>
    /// This interface is not an IRealObject: it will not be mapped.
    /// </summary>
    public interface IA0 { }

    /// <summary>
    /// This is an Real object (that extends the mere interface IA0).
    /// </summary>
    public interface IA1 : IA0, IRealObject { }

    /// <summary>
    /// This class implements IA1: it is an IRealObject and as such can participate to
    /// service configuration.
    /// </summary>
    public class A : IA1
    {
        void RegisterStartupServices( IActivityMonitor m, SimpleServiceContainer startupServices )
        {
            startupServices.GetService( typeof( SuperStartupService ) )
                .Should().BeNull( "B depends on A: B.RegisterStartupServices is called after this one." );

            startupServices.GetService( typeof( IStObjObjectMap ) )
                .Should().NotBeNull( "The StObj side of the map (that handles the Real objects) is available." );
        }

        /// <summary>
        /// Configure the services (does nothing here: this just tests the parameter
        /// injection of the startup services). 
        /// </summary>
        /// <param name="register">This one is the only required parameter.</param>
        /// <param name="ambientObjects">
        /// IStObjObjectMap is available: configuring services can rely on any IRealObject since they are already initialized (this
        /// is even available in the RegisterStartupServices).
        /// </param>
        /// <param name="superService">This is injected.</param>
        /// <param name="ext">This is injected.</param>
        /// <param name="optionalService">
        /// This is injected if it exists in the StartupServices: startup services can be optional.
        /// </param>
        void ConfigureServices( StObjContextRoot.ServiceRegister register,
                                IStObjObjectMap ambientObjects,
                                SuperStartupService superService,
                                TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem ext,
                                IOptionalStartupService? optionalService = null )
        {
            ambientObjects.Obtain<IA1>().Should().BeSameAs( this );
            superService.Should().NotBeNull();
            ext.Should().NotBeNull();
            superService.Talk( register.Monitor );
        }
    }

    public interface IBIsRealObject : IRealObject
    {
        int BCanTalkToYou( IActivityMonitor m, string msg );
    }

    /// <summary>
    /// This startup service is registered by B. And consumed by A.
    /// It could also be used by B since all RegisterStartupServices are called
    /// before all ConfigureServices.
    /// </summary>
    public class SuperStartupService
    {
        readonly bool _mustFail;

        public SuperStartupService( bool mustFail )
        {
            _mustFail = mustFail;
        }

        public void Talk( IActivityMonitor m )
        {
            m.Info( "SuperStartupService is talking to you." );
            if( _mustFail ) m.Error( "But SuperStartupService has been told to fail miserably." );
        }
    }

    /// <summary>
    /// Very stupid attribute that shows how easy it is to participate in code generation.
    /// Note that in real life, the code generation is implemented in a "Setup dependency" (a Runtime or Engine component)
    /// and the Attribute itself carries only the definition of the code generation: see <see cref="ContextBoundDelegationAttribute"/>
    /// to easily implement this.
    /// </summary>
    class StupidCodeAttribute : Attribute, IAutoImplementorMethod
    {
        public StupidCodeAttribute( string actualCode, bool isLamda = false )
        {
            ActualCode = actualCode;
        }

        public bool IsLambda { get; }

        public string ActualCode { get; }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, MethodInfo m, ICSCodeGenerationContext c, ITypeScope b )
        {
            IFunctionScope mB = b.CreateOverride( m );
            mB.Parent.Should().BeSameAs( b, "The function is ready to be implemented." );

            if( IsLambda ) mB.Append( "=> " ).Append( ActualCode ).Append( ';' ).NewLine();
            else mB.Append( ActualCode );

            return CSCodeGenerationResult.Success;
        }
    }

    /// <summary>
    /// Real object.
    /// Note: being abstract implies that this type has 0 constructor (a concrete type with no constructor
    /// has automatically the generated public default constructor) and this has to be handled since, normally
    /// a Service MUST have one and only one public constructor.
    /// An RealObject that implements a Service is an exception to this rule.
    /// </summary>
    public abstract class B : IBIsRealObject, IAutoServiceCanBeImplementedByRealObject
    {
        void IAutoServiceCanBeImplementedByRealObject.DoSomething( IActivityMonitor m )
        {
            m.Info( "I'm doing something from B." );
        }

        /// <summary>
        /// B depends on A. (Its RegisterStartupServices is called after A's one.)
        /// </summary>
        /// <param name="a">The dependency to the IA real object.</param>
        void StObjConstruct( IA1 a )
        {
        }

        void RegisterStartupServices( IActivityMonitor m, SimpleServiceContainer startupServices )
        {
            m.Info( $"Registering the Super Startup service." );
            bool willFail = startupServices.GetRequiredService<TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem>().EmitErrorLogSoThatConfigureServicesFails;
            startupServices.Add( new SuperStartupService( willFail ) );
        }

        class PrivateAliceOrBobProvider : IAliceOrBobProvider
        {
            public object Obtain( bool isAlice ) => isAlice ? "This is 'Alice'." : "This is 'Bob'.";
        }

        class PrivateAlwaysAliceProvider : IAliceOrBobProvider
        {
            public object Obtain( bool isAlice ) => "This is ALWAYS 'Alice'.";
        }

        void ConfigureServices( StObjContextRoot.ServiceRegister register, TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem conf )
        {
            if( conf.AlwaysUseAlice )
            {
                register.Services.AddScoped<IAliceOrBobProvider, PrivateAlwaysAliceProvider>();
            }
            else
            {
                register.Services.AddScoped<IAliceOrBobProvider, PrivateAliceOrBobProvider>();
            }
        }

        [StupidCode( @"m.Info( ""This is from generated code: "" + msg ); return 3172;" )]
        public abstract int BCanTalkToYou( IActivityMonitor m, string msg );

    }

    [Test]
    public async Task code_generation_is_so_easy_on_real_objects_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( A ), typeof( B ) );

        var startupServices = new SimpleServiceContainer();
        startupServices.Add( new TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem() );
        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
        {
            var map = (await configuration.RunAsync().ConfigureAwait( false )).LoadMap();

            using var auto = map.CreateAutomaticServices( startupServices: startupServices );

            auto.Services.GetRequiredService<IBIsRealObject>()
                .BCanTalkToYou( TestHelper.Monitor, "Magic!" )
                .Should().Be( 3172 );

            auto.Services.GetRequiredService<IBIsRealObject>()
                .Should().BeSameAs( auto.Services.GetRequiredService<B>() )
                .And.BeSameAs( auto.Services.GetRequiredService<IAutoServiceCanBeImplementedByRealObject>(), "The Auto Service/Object must be the same instance!" );

            entries.Should().Contain( e => e.Text == "This is from generated code: Magic!" );
        }
    }

    public abstract class ServiceCanTalk : IAutoService
    {
        public ServiceCanTalk()
        {
        }

        [StupidCode( @"m.Info( ""This is from generated code: "" + msg ); return 3172;" )]
        public abstract int CodeCanBeInTheAttribute( IActivityMonitor m, string msg );
    }

    [Test]
    public async Task code_generation_is_also_easy_on_services_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ServiceCanTalk ) );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
        {
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Services.GetRequiredService<ServiceCanTalk>()
                .CodeCanBeInTheAttribute( TestHelper.Monitor, "Magic! (Again)" )
                .Should().Be( 3172 );

            entries.Should().Contain( e => e.Text == "This is from generated code: Magic! (Again)" );
        }
    }

    [Test]
    public async Task Service_implemented_by_a_real_object_can_be_overridden_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ScopedImplementation ), typeof( A ), typeof( B ) );

        var startupServices = new SimpleServiceContainer();
        startupServices.Add( new TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem() );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
        {
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).LoadMap().CreateAutomaticServices( startupServices: startupServices );
            // We are using here the ScopedImplementation.
            var s = auto.Services.GetRequiredService<IAutoServiceCanBeImplementedByRealObject>();
            s.DoSomething( TestHelper.Monitor );
            // Just to be sure it's actually a Scoped service.
            using( var scoped = auto.Services.CreateScope() )
            {
                scoped.ServiceProvider.GetRequiredService<IAutoServiceCanBeImplementedByRealObject>().Should().NotBeSameAs( s );
            }
            entries.Should().NotContain( e => e.MaskedLevel >= LogLevel.Error );
            entries.Should().Contain( e => e.Text == "SuperStartupService is talking to you." );
            entries.Should().NotContain( e => e.Text == "I'm doing something from B." );
            entries.Should().Contain( e => e.Text == "Based on IAliceOrBobProvider: I'm working with 'This is 'Alice'.'."
                                        || e.Text == "Based on IAliceOrBobProvider: I'm working with 'This is 'Bob'.'." );
        }
    }

    [Test]
    public async Task Initially_registered_StartupServices_may_be_used_as_configurator_or_options_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ScopedImplementation ), typeof( A ), typeof( B ) );

        var startupServices = new SimpleServiceContainer();
        startupServices.Add( new TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem() { AlwaysUseAlice = true } );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
        {
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).LoadMap().CreateAutomaticServices( startupServices: startupServices );
            // We are using here the ScopedImplementation.
            var s = auto.Services.GetRequiredService<IAutoServiceCanBeImplementedByRealObject>();
            s.Should().BeOfType<ScopedImplementation>();
            s.DoSomething( TestHelper.Monitor );

            entries.Should().NotContain( e => e.MaskedLevel >= LogLevel.Error );
            entries.Should().Contain( e => e.Text == "SuperStartupService is talking to you." );
            entries.Should().NotContain( e => e.Text == "I'm doing something from B." );
            entries.Should().Contain( e => e.Text == "Based on IAliceOrBobProvider: I'm working with 'This is ALWAYS 'Alice'.'." );
        }
    }

    [Test]
    public async Task superseding_a_IRealObject_implemented_service_by_a_wrapper_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SingletonImplementation ), typeof( A ), typeof( B ) );

        var startupServices = new SimpleServiceContainer();
        startupServices.Add( new TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem() );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
        {
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).LoadMap().CreateAutomaticServices( startupServices: startupServices );
            auto.Services.GetRequiredService<IAutoServiceCanBeImplementedByRealObject>().DoSomething( TestHelper.Monitor );
            auto.ServiceCollection.Should().ContainSingle( s => s.ServiceType == typeof( IAutoServiceCanBeImplementedByRealObject ) && s.Lifetime == ServiceLifetime.Singleton );

            entries.Should().NotContain( e => e.MaskedLevel >= LogLevel.Error );
            entries.Should().Contain( e => e.Text == "SuperStartupService is talking to you." );
            entries.Should().Contain( e => e.Text == "I'm wrapping the default B's implementation." );
            entries.Should().Contain( e => e.Text == "I'm doing something from B." );
        }
    }

    [Test]
    public async Task superseding_a_IRealObject_implemented_service_by_another_IAmbient_Object_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( BDependency ), typeof( A ), typeof( B ) );

        var startupServices = new SimpleServiceContainer();
        startupServices.Add( new TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem() );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
        {
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).LoadMap().CreateAutomaticServices( startupServices: startupServices );
            auto.Services.GetRequiredService<IAutoServiceCanBeImplementedByRealObject>().DoSomething( TestHelper.Monitor );

            entries.Should().NotContain( e => e.MaskedLevel >= LogLevel.Error );
            entries.Should().Contain( e => e.Text == "SuperStartupService is talking to you." );
            entries.Should().Contain( e => e.Text == "B is no more doing something." )
                            .And.NotContain( e => e.Text == "I'm doing something from B." );
        }
    }

    [Test]
    public async Task any_error_logged_during_Service_Configuration_make_AddStObjMap_returns_false_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( A ), typeof( B ) );

        var startupServices = new SimpleServiceContainer();
        startupServices.Add( new TotallyExternalStartupServiceThatActAsAConfiguratorOfTheWholeSystem() { EmitErrorLogSoThatConfigureServicesFails = true } );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Invalid, 1000 ) )
        {
            await configuration.GetFailedAutomaticServicesAsync( "But SuperStartupService has been told to fail miserably.",
                                                                 startupServices: startupServices );

            entries.Should().Contain( e => e.Text == "SuperStartupService is talking to you." && e.MaskedLevel == LogLevel.Info );
        }
    }


    public class ServiceWithValueTypeCtorParameters : IAutoService
    {
        public ServiceWithValueTypeCtorParameters( bool requiredValueType )
        {
            RequiredValueType = requiredValueType;
        }

        public bool RequiredValueType { get; }
    }

    [Test]
    public async Task ValueType_ctor_parameters_without_default_value_requires_an_explicit_registration_in_the_DI_container_at_runtime_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ServiceWithValueTypeCtorParameters ) );

        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn, 1000 ) )
            {
                using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
                auto.Services.Invoking( sp => sp.GetService<ServiceWithValueTypeCtorParameters>() ).Should().Throw<InvalidOperationException>();

                entries.Should().Contain( e => e.MaskedLevel == LogLevel.Warn
                                               && e.Text.Contains( "This requires an explicit registration in the DI container", StringComparison.Ordinal ) );
            }
        }
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
            {
                using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: services =>
                {
                    services.AddSingleton( typeof( bool ), true );

                } );
                var resolved = auto.Services.GetRequiredService<ServiceWithValueTypeCtorParameters>();
                resolved.RequiredValueType.Should().BeTrue();

                entries.Should().Contain( e => e.MaskedLevel == LogLevel.Warn
                                            && e.Text.Contains( "This requires an explicit registration in the DI container", StringComparison.OrdinalIgnoreCase ) );
            }
        }

    }

    // This works only if the type 'int[]' is available at runtime in the DI container.
    // And since its lifetime is not known, ServiceWithVaryingParams will be scoped.
    public class ServiceWithVaryingParams : IScopedAutoService
    {
        public ServiceWithVaryingParams( params int[] things )
        {
            Things = things;
        }

        public IReadOnlyList<int> Things { get; }
    }

    [Test]
    public async Task varying_params_requires_an_explicit_registration_in_the_DI_container_at_runtime_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ServiceWithVaryingParams ) );
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Services.Invoking( sp => sp.GetService<ServiceWithVaryingParams>() ).Should().Throw<InvalidOperationException>();
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ServiceWithVaryingParams ) );
            using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: services =>
            {
                services.AddSingleton( typeof( int[] ), new int[] { 1, 2, 3 } );

            } );
            var resolved = auto.Services.GetRequiredService<ServiceWithVaryingParams>();
            resolved.Things.Should().BeEquivalentTo( new[] { 1, 2, 3 } );
        }

    }

    public class ServiceWithOptionalValueTypeCtorParameters : IAutoService
    {
        public ServiceWithOptionalValueTypeCtorParameters( bool optionalValueType = true, string stringAreConsideredSameAsValueType = "Hop" )
        {
        }
    }

    [Test]
    public async Task ValueType_ctor_parameters_with_default_value_are_ignored_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ServiceWithOptionalValueTypeCtorParameters ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Services.GetService<ServiceWithOptionalValueTypeCtorParameters>().Should().NotBeNull();
    }

    public interface IPublicService : IAutoService
    {
    }

    interface IInternalInterface : IPublicService
    {
    }

    // A public interface cannot extend an internal one: internal interfaces are leaves so we don't need to
    // handle "holes" in the interface hierarchy.
    // Such final internal CKType interfaces are simply ignored: they can be used internally by implementations.
    //
    // Error CS0061: Inconsistent accessibility: base interface 'FullServiceTests.IInternalInterface' is less accessible than interface 'FullServiceTests.IMorePublicService'	CK.StObj.Engine.Tests(netcoreapp3.1)	C:\Dev\CK\CK-Database-Projects\CK-StObj\Tests\CK.StObj.Engine.Tests\Service\FullServiceTests.cs	557	Active
    //public interface IMorePublicService : IInternalInterface
    //{
    //}

    public class TheService : IInternalInterface
    {
    }

    [Test]
    public void internal_interfaces_are_ignored()
    {
        TestHelper.GetSuccessfulCollectorResult( [typeof( TheService )] );
    }


}
