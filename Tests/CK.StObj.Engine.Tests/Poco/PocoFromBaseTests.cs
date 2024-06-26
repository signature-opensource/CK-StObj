using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    namespace DuckTypedAndInternal
    {
        class ExcludeCKTypeAttribute : System.Attribute { }
    }

    [DuckTypedAndInternal.ExcludeCKType]
    public interface IAmNotAPocoButIAmPocoCompliant
    {
        IList<(int Power, string Name)> Values { get; }

        [DefaultValue(3712)]
        int Power { get; set; }
    }

    public interface IPocoFromBase : IAmNotAPocoButIAmPocoCompliant, IPoco
    {
    }

    [TestFixture]
    public class PocoFromBaseTests
    {
        [Test]
        public void IPoco_fields_can_be_defined_above_but_with_ExcludeCKType_attribute()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( IPocoFromBase ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var fA = d.Find( "CK.StObj.Engine.Tests.Poco.IPocoFromBase" );
            Debug.Assert( fA != null );
            var a = d.Create<IPocoFromBase>();
            a.Values.Should().NotBeNull().And.BeEmpty();
            a.Power.Should().Be( 3712 );
        }
    }
}
