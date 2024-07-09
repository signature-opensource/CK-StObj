using CK.Core;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

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
                TestHelper.GetSuccessfulCollectorResult( [typeof( NotALinkBetweenFamilies )] );
            }
            {
                // OnlyForS, that covers NotALinkBetweenFamilies, is the final best for IS1 and IS2.
                var map = TestHelper.GetSuccessfulCollectorResult( [typeof( NotALinkBetweenFamilies ), typeof( OnlyForS )] ).EngineMap;
                Throw.DebugAssert( map != null );
                map.Services.Mappings[typeof( IS1 )].ClassType.Should().BeSameAs( typeof( OnlyForS ) );
                map.Services.Mappings[typeof( IS2 )].ClassType.Should().BeSameAs( typeof( OnlyForS ) );
                map.Services.Mappings[typeof( IOtherServiceBase )].ClassType.Should().BeSameAs( typeof( NotALinkBetweenFamilies ) );
            }
        }

    }
}
