using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class RawCodeGeneratorTests
{
    public class CGen : ICSCodeGenerator
    {
        public static bool Called;

        public CGen()
        {
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            Called = true;
            return CSCodeGenerationResult.Success;
        }
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public class Holder
    {
    }

    [Test]
    public void ICSCodeGenerator_on_regular_class()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( Holder ) );
        configuration.Run().LoadMap();
        CGen.Called.Should().BeTrue();
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public static class StaticHolder
    {
    }

    [Test]
    public void ICodeGenerator_on_static_class()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( StaticHolder ) );
        configuration.Run().LoadMap();
        CGen.Called.Should().BeTrue();
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public interface RawInterface
    {
    }

    [Test]
    public void ICodeGenerator_on_raw_interface()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawInterface ) );
        configuration.Run().LoadMap();
        CGen.Called.Should().BeTrue();
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public enum EvenOnAnEnumItWorks
    {
    }

    [Test]
    public void ICodeGenerator_on_enum()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( EvenOnAnEnumItWorks ) );
        configuration.Run().LoadMap();
        CGen.Called.Should().BeTrue();
    }


}
