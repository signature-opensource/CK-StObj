using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoWithGlobalizationTypesTests
    {
        public interface IWithGlobalization : IPoco
        {
            // Default must be NormalizedCultureInfo.CodeDefault.
            ExtendedCultureInfo ExtendedCultureInfo { get; set; }
            // Default must be NormalizedCultureInfo.CodeDefault.
            NormalizedCultureInfo NormalizedCultureInfo { get; set; }
            // Default must be CodeString.Empty.
            CodeString CodeString { get; set; }
            // Default must be MCString.Empty.
            MCString MCString { get; set; }
            // Default is a ResultMessage.IsValid == false.
            ResultMessage ResultMessage { get; set; }

            ExtendedCultureInfo? NExtendedCultureInfo { get; set; }
            // Default must be NormalizedCultureInfo.CodeDefault.
            NormalizedCultureInfo? NNormalizedCultureInfo { get; set; }
            // Default must be CodeString.Empty.
            CodeString? NCodeString { get; set; }
            // Default must be MCString.Empty.
            MCString? NMCString { get; set; }
            // Default is a ResultMessage.IsValid == false.
            ResultMessage? NResultMessage { get; set; }

        }

        [Test]
        public void default_for_Globalization_types_are_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithGlobalization ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithGlobalization>>().Create();

            p.ExtendedCultureInfo.Should().BeSameAs( NormalizedCultureInfo.CodeDefault );
            p.NormalizedCultureInfo.Should().BeSameAs( NormalizedCultureInfo.CodeDefault );
            p.CodeString.Should().BeSameAs( CodeString.Empty );
            p.MCString.Should().BeSameAs( MCString.Empty );
            p.ResultMessage.IsValid.Should().BeFalse();

            p.NExtendedCultureInfo.Should().BeNull();
            p.NNormalizedCultureInfo.Should().BeNull();
            p.NCodeString.Should().BeNull();
            p.NMCString.Should().BeNull();
            p.NResultMessage.HasValue.Should().BeFalse();
        }
    }
}
