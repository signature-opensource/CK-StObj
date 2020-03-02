using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
            s1.MultipleMappingTypes.Should().BeEquivalentTo( typeof( IHostedService ) );
            s2.MultipleMappingTypes.Should().BeEquivalentTo( typeof( IHostedService ) );
            s1.IsScoped.Should().BeFalse();
            s2.IsScoped.Should().BeFalse();
        }
    }
}
