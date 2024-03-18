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
        public void default_values_on_simple_field()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IThing ), typeof( IThingHolder ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var h = s.GetRequiredService<IPocoFactory<IThingHolder>>().Create();
            h.Value.Should().NotBeNull();
            h.Value.Power.Should().Be( 3712 );
        }

        public enum TypeDefaultIsUnsignedMinimalValue
        {
            NotTheDefault = -1,
            NotTheDefault2 = -89794,
            NotTheDefault3 = 43,
            NotTheDefault4 = 6546,
            ThisIsTheDefault = 42,
        }

        [Test]
        public void default_values_on_enum_is_the_Unsigned_minimal_value()
        {
            var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );
            var t = ts.RegisterOblivious( TestHelper.Monitor, typeof( TypeDefaultIsUnsignedMinimalValue ) );

            Debug.Assert( t != null && t.DefaultValueInfo.DefaultValue != null );
            t.DefaultValueInfo.DefaultValue.SimpleValue.Should().Be( TypeDefaultIsUnsignedMinimalValue.ThisIsTheDefault );
            t.DefaultValueInfo.DefaultValue.ValueCSharpSource.Should().Be( "CK.StObj.Engine.Tests.Poco.DefaultValueTests.TypeDefaultIsUnsignedMinimalValue.ThisIsTheDefault" );

            var tE = (IEnumPocoType)t;
            tE.DefaultValueName.Should().Be( "ThisIsTheDefault" );
        }

    }
}
