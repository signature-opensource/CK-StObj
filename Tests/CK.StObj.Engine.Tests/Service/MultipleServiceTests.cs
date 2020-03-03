using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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

            var result = TestHelper.GetSuccessfulResult( collector );
            result.Services.SimpleMappings[typeof( IHostedService )].Should().BeNull();
            IStObjServiceClassDescriptor s1 = result.Services.SimpleMappings[typeof( S1 )];
            IStObjServiceClassDescriptor s2 = result.Services.SimpleMappings[typeof( S2 )];
            s1.MultipleMappings.Should().BeEquivalentTo( typeof( IHostedService ) );
            s2.MultipleMappings.Should().BeEquivalentTo( typeof( IHostedService ) );
            s1.IsScoped.Should().BeFalse();
            s2.IsScoped.Should().BeFalse();
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

        [Test]
        public void real_objects_can_support_multiple_objects_but_interfaces_cannot_be_both()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( UserGoogle ) );
            collector.RegisterType( typeof( UserOffice ) );

            var result = TestHelper.GetSuccessfulResult( collector );
            result.Services.SimpleMappings[typeof( IAuthProvider )].Should().BeNull();
            IStObjFinalImplementation g = result.StObjs.Mappings.Single( kv => kv.Key == typeof( IUserGoogle ) ).Value;
            IStObjFinalImplementation o = result.StObjs.Mappings.Single( kv => kv.Key == typeof( UserOffice ) ).Value;
            g.MultipleMappings.Should().BeEquivalentTo( typeof( IAuthProvider ) );
            o.MultipleMappings.Should().BeEquivalentTo( typeof( IAuthProvider ) );
        }

    }
}
