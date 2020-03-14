using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{

    [TestFixture]
    public class FamilyServiceTests
    {
        public interface IServiceBase : IAutoService { }

        public interface IS1 : IServiceBase { }

        public interface IS2 : IServiceBase { }

        public interface IOtherServiceBase : IAutoService { }

        public class NotALinkBetweenFamilies : IS1, IS2, IOtherServiceBase { }

        public class OnlyForS : IS1, IS2
        {
            public OnlyForS( NotALinkBetweenFamilies covered )
            {
            }
        }

        [Test]
        public void Class_does_not_bind_families_together()
        {
            {
                // NotALinkBetweenFamilies supports all the services (IS1, IS2 and IOtherServiceBase).
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( NotALinkBetweenFamilies ) );
                TestHelper.GetSuccessfulResult( collector );
            }
            {
                // OnlyForS, that covers NotALinkBetweenFamilies, is the final best for IS1 and IS2.
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( NotALinkBetweenFamilies ) );
                collector.RegisterType( typeof( OnlyForS ) );
                var r = TestHelper.GetSuccessfulResult( collector );
                r.Services.SimpleMappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( OnlyForS ) );
                r.Services.SimpleMappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( OnlyForS ) );
                r.Services.SimpleMappings[typeof( IOtherServiceBase )].ClassType.Should().BeSameAs( typeof( NotALinkBetweenFamilies ) );
            }
        }

    }
}
