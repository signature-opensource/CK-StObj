using CK.Core;
using CK.Setup;
using CK.StObj.Engine.Tests.Poco.Sample;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0051 // Remove unused private members

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class DefaultValueTests
    {
        public interface IThing : IPoco
        {
            [DefaultValue(3712)]
            int Power { get; set; }
        }

        public interface IThingHolder : IPoco
        {
            IThing Value { get; }
        }

        [Test]
        public void default_values()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IThing ), typeof( IThingHolder ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var h = s.GetRequiredService<IPocoFactory<IThingHolder>>().Create();
            h.Value.Should().NotBeNull();
            h.Value.Power.Should().Be( 3712 );
        }
    }
}
