using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
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
        public void Globalization_types_serialization_are_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithGlobalization ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithGlobalization>>().Create();

            JsonTestHelper.Roundtrip( s.GetRequiredService<PocoDirectory>(), p );

            var name = "me";
            p.ExtendedCultureInfo = ExtendedCultureInfo.GetExtendedCultureInfo( "fr-CA, es" );
            p.NormalizedCultureInfo = NormalizedCultureInfo.GetNormalizedCultureInfo( "ar-SA" );
            p.ResultMessage = ResultMessage.Info( $"Hello {name}, today is {DateTime.UtcNow.Date}." );
            p.MCString = p.ResultMessage.Message;
            p.CodeString = new CodeString( ExtendedCultureInfo.GetExtendedCultureInfo( "ar-tn" ), $"Hello on {DateTime.UtcNow.Date}." );
            p.FormattedString = p.CodeString.FormattedString;

            var back = JsonTestHelper.Roundtrip( s.GetRequiredService<PocoDirectory>(), p );
            back.ExtendedCultureInfo.Name.Should().Be( "fr-ca,es" );
            back.NormalizedCultureInfo.Name.Should().Be( "ar-sa" );
            back.ResultMessage.Text.Should().Be( $"Hello {name}, today is {DateTime.UtcNow.Date}." );
            back.CodeString.ContentCulture.Name.Should().Be( "ar-tn" );
            back.CodeString.Text.Should().Be( $"Hello on {DateTime.UtcNow.Date}." );
        }
    }
}
