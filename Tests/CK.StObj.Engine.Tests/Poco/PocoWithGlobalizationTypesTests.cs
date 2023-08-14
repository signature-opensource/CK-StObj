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

            // Default is a ResultMessage.IsValid == false.
            ResultMessage ResultMessage { get; set; }
            // Default must be MCString.Empty.
            MCString MCString { get; set; }
            // Default must be CodeString.Empty.
            CodeString CodeString { get; set; }
            // Default must be FormattedString.Empty.
            FormattedString FormattedString { get; set; }

            ExtendedCultureInfo? NExtendedCultureInfo { get; set; }
            NormalizedCultureInfo? NNormalizedCultureInfo { get; set; }
            ResultMessage? NResultMessage { get; set; }
            MCString? NMCString { get; set; }
            CodeString? NCodeString { get; set; }
            FormattedString? NFormattedString { get; set; }
        }

        [Test]
        public void default_for_Globalization_types_are_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithGlobalization ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithGlobalization>>().Create();

            p.ExtendedCultureInfo.Should().BeSameAs( NormalizedCultureInfo.CodeDefault );
            p.NormalizedCultureInfo.Should().BeSameAs( NormalizedCultureInfo.CodeDefault );
            p.ResultMessage.IsValid.Should().BeFalse();
            p.MCString.Should().BeSameAs( MCString.Empty );
            p.CodeString.Should().BeSameAs( CodeString.Empty );
            p.FormattedString.Should().BeEquivalentTo( FormattedString.Empty );

            p.NExtendedCultureInfo.Should().BeNull();
            p.NNormalizedCultureInfo.Should().BeNull();
            p.NResultMessage.Should().BeNull();
            p.NMCString.Should().BeNull();
            p.NCodeString.Should().BeNull();
            p.NFormattedString.Should().BeNull();
            p.NResultMessage.HasValue.Should().BeFalse();
        }
    }
}
