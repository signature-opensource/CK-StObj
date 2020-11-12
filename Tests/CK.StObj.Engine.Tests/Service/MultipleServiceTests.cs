using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class MultipleServiceTests
    {
        [IsMultiple]
        public interface IHostedService : ISingletonAutoService { }

        public class S1 : IHostedService { }
        public class S2 : IHostedService { }

        [Test]
        public void simple_Multiple_services_discovery()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( S1 ) );
            collector.RegisterType( typeof( S2 ) );

            var result = TestHelper.GetAutomaticServices( collector );
            result.Map.Services.SimpleMappings.ContainsKey( typeof( IHostedService ) ).Should().BeFalse();
            IStObjServiceClassDescriptor s1 = result.Map.Services.SimpleMappings[typeof( S1 )];
            IStObjServiceClassDescriptor s2 = result.Map.Services.SimpleMappings[typeof( S2 )];
            s1.MultipleMappings.Should().BeEquivalentTo( typeof( IHostedService ) );
            s2.MultipleMappings.Should().BeEquivalentTo( typeof( IHostedService ) );
            s1.IsScoped.Should().BeFalse( "Nothing prevents S1 to be singleton." );
            s2.IsScoped.Should().BeFalse( "Nothing prevents S2 to be singleton." );

            var hosts = result.Services.GetRequiredService<IEnumerable<IHostedService>>();
            hosts.Should().HaveCount( 2 );
        }

        [IsMultiple]
        public interface IAuthProvider { }

        public interface IUserGoogle : IRealObject, IAuthProvider { }

        public class UserGoogle : IUserGoogle
        {
        }

        public class UserOffice : IRealObject, IAuthProvider
        {
        }

        [IsMultiple]
        public interface IAmAMultipleRealObject : IRealObject { }

        [IsMultiple]
        public interface IAmAMultipleRealObject2 : IUserGoogle { }

        public class ThatIsNotPossible : IAmAMultipleRealObject { }
        public class ThatIsNotPossible2 : IAmAMultipleRealObject2 { }

        [Test]
        public void real_objects_can_support_IsMultiple_interfaces_but_interfaces_cannot_be_IRealObjects_and_IsMultiple()
        {
            {
                var c = TestHelper.CreateStObjCollector();
                c.RegisterType( typeof( ThatIsNotPossible ) );
                TestHelper.GetFailedResult( c );

                var c2 = TestHelper.CreateStObjCollector();
                c2.RegisterType( typeof( ThatIsNotPossible2 ) );
                TestHelper.GetFailedResult( c2 );
            }

            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( UserGoogle ) );
            collector.RegisterType( typeof( UserOffice ) );

            var result = TestHelper.GetAutomaticServices( collector );
            Debug.Assert( result.Result.EngineMap != null, "No initialization error." );

            result.Map.Services.SimpleMappings.ContainsKey( typeof( IAuthProvider ) ).Should().BeFalse();
            IStObjFinalImplementation g = result.Result.EngineMap.StObjs.ToHead( typeof( IUserGoogle ) )!.FinalImplementation;
            IStObjFinalImplementation o = result.Result.EngineMap.StObjs.ToHead( typeof( UserOffice ) )!.FinalImplementation;
            g.MultipleMappings.Should().BeEquivalentTo( typeof( IAuthProvider ) );
            o.MultipleMappings.Should().BeEquivalentTo( typeof( IAuthProvider ) );

            var authProviders = result.Services.GetRequiredService<IEnumerable<IAuthProvider>>();
            authProviders.Should().HaveCount( 2 );

        }


        public class MulipleConsumer : IAutoService
        {
            public MulipleConsumer( IEnumerable<IAuthProvider> providers )
            {
                providers.Should().HaveCount( 2 );
                Providers = providers.ToArray();
            }

            public IAuthProvider[] Providers { get; }
        }

        [Test]
        public void IAutoServices_can_depend_on_IEnumerable_of_IsMultiple_interfaces()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( UserGoogle ) );
            collector.RegisterType( typeof( UserOffice ) );
            collector.RegisterType( typeof( MulipleConsumer ) );

            var result = TestHelper.GetAutomaticServices( collector );
            result.Map.Services.SimpleMappings.ContainsKey( typeof( IAuthProvider ) ).Should().BeFalse();
            var c = result.Services.GetRequiredService<MulipleConsumer>();
            IStObjFinalImplementation g = result.Result.StObjs.ToStObj( typeof( IUserGoogle ) ).FinalImplementation;
            IStObjFinalImplementation o = result.Result.StObjs.ToStObj( typeof( UserOffice ) ).FinalImplementation;

            c.Providers.Should().BeEquivalentTo( g, o );
        }

        public interface IOfficialHostedService { }

        public class H1 : IOfficialHostedService, IAutoService { }
        public class H2 : IOfficialHostedService, IScopedAutoService { }
        public class HNot : IOfficialHostedService { }

        [Test]
        public void IsMutiple_works_on_external_interfaces_and_this_is_the_magic_for_IHosteService_auto_registering()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService );
            collector.RegisterType( typeof( H1 ) );
            collector.RegisterType( typeof( H2 ) );
            collector.RegisterType( typeof( HNot ) );

            var result = TestHelper.GetAutomaticServices( collector );
            result.Map.Services.SimpleMappings.ContainsKey( typeof( IOfficialHostedService ) ).Should().BeFalse( "A Multiple interface IS NOT mapped." );
            IStObjServiceClassDescriptor s1 = result.Map.Services.SimpleMappings[typeof( H1 )];
            IStObjServiceClassDescriptor s2 = result.Map.Services.SimpleMappings[typeof( H2 )];
            result.Map.Services.SimpleMappings.ContainsKey( typeof( HNot ) ).Should().BeFalse( "HNot is not an AutoService!" );
            s1.MultipleMappings.Should().BeEquivalentTo( typeof( IOfficialHostedService ) );
            s2.MultipleMappings.Should().BeEquivalentTo( typeof( IOfficialHostedService ) );
            s1.IsScoped.Should().BeFalse( "Nothing prevents H1 to be singleton." );
            s2.IsScoped.Should().BeTrue( "H2 is IScopedAutoService." );

            var hosts = result.Services.GetRequiredService<IEnumerable<IOfficialHostedService>>();
            hosts.Should().HaveCount( 2 );
        }

        [Test]
        public void IsMutiple_AND_IsSingleton_on_external_IHosteService_ensures_that_it_is_Singleton()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
                collector.RegisterType( typeof( H1 ) );
                var result = TestHelper.GetAutomaticServices( collector );
                result.Map.Services.SimpleMappings[typeof( H1 )].IsScoped.Should().BeFalse( "IOfficialHostedService makes it Singleton." );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
                collector.RegisterType( typeof( H2 ) );
                TestHelper.GetFailedResult( collector );
            }
        }
    }
}
