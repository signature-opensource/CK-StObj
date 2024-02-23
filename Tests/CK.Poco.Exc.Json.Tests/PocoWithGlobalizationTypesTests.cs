using CK.Core;
using CK.Poco.Exc.Json.Tests;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
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

            // Default is a SimpleUserMessage.IsValid == false.
            SimpleUserMessage SimpleUserMessage { get; set; }
            // Default is a UserMessage.IsValid == false.
            UserMessage UserMessage { get; set; }
            // Default must be MCString.Empty.
            MCString MCString { get; set; }
            // Default must be CodeString.Empty.
            CodeString CodeString { get; set; }
            // Default must be FormattedString.Empty.
            FormattedString FormattedString { get; set; }

            ExtendedCultureInfo? NExtendedCultureInfo { get; set; }
            NormalizedCultureInfo? NNormalizedCultureInfo { get; set; }
            SimpleUserMessage? NSimpleUserMessage { get; set; }
            UserMessage? NUserMessage { get; set; }
            MCString? NMCString { get; set; }
            CodeString? NCodeString { get; set; }
            FormattedString? NFormattedString { get; set; }
        }

        [Test]
        public void Globalization_types_serialization_are_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithGlobalization ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithGlobalization>>().Create();

            JsonTestHelper.Roundtrip( s.GetRequiredService<PocoDirectory>(), p, text: t => TestHelper.Monitor.Info( $"IWithGlobalization (default) serialization: " + t ) );
            ExtendedCultureInfo someCulture = ExtendedCultureInfo.GetExtendedCultureInfo( "fr-CA, es" );

            var name = "me";
            p.ExtendedCultureInfo = someCulture;
            p.NormalizedCultureInfo = NormalizedCultureInfo.GetNormalizedCultureInfo( "ar-SA" );
            p.SimpleUserMessage = new SimpleUserMessage( UserMessageLevel.Warn, "A simple message.", 42 );
            p.UserMessage = UserMessage.Info( someCulture, $"Hello {name}, today is {DateTime.UtcNow.Date}." );
            p.MCString = p.UserMessage.Message;
            p.CodeString = new CodeString( ExtendedCultureInfo.GetExtendedCultureInfo( "ar-tn" ), $"Hello on {DateTime.UtcNow.Date}." );
            p.FormattedString = p.CodeString.FormattedString;

            var back = JsonTestHelper.Roundtrip( s.GetRequiredService<PocoDirectory>(), p, text: t => TestHelper.Monitor.Info( $"IWithGlobalization (with values) serialization: " + t ) );
            back.ExtendedCultureInfo.Name.Should().Be( "fr-ca,es" );
            back.NormalizedCultureInfo.Name.Should().Be( "ar-sa" );

            back.SimpleUserMessage.Message.Should().Be( "A simple message." );
            back.SimpleUserMessage.Depth.Should().Be( 42 );
            back.SimpleUserMessage.Level.Should().Be( UserMessageLevel.Warn );

            back.UserMessage.Text.Should().Be( string.Create( someCulture, $"Hello {name}, today is {DateTime.UtcNow.Date}." ) );
            back.CodeString.TargetCulture.Name.Should().Be( "ar-tn" );
            back.CodeString.Text.Should().StartWith( $"Hello on " );
        }
    }
}
