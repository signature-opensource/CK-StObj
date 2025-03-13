using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

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

        [DefaultValue( 3712 )]
        int Power { get; set; }
    }

    public interface IPocoFromBase : IAmNotAPocoButIAmPocoCompliant, IPoco
    {
    }

    [TestFixture]
    public class PocoFromBaseTests
    {
        [Test]
        public async Task IPoco_fields_can_be_defined_above_but_with_ExcludeCKType_attribute_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IPocoFromBase ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var fA = d.Find( "CK.StObj.Engine.Tests.Poco.IPocoFromBase" );
            Debug.Assert( fA != null );
            var a = d.Create<IPocoFromBase>();
            a.Values.ShouldBeEmpty();
            a.Power.ShouldBe( 3712 );
        }
    }
}
