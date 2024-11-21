using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;
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
    public async Task ICSCodeGenerator_on_regular_class_Async()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( Holder ) );
        (await configuration.RunAsync().ConfigureAwait( false )).LoadMap();
        CGen.Called.Should().BeTrue();
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public static class StaticHolder
    {
    }

    [Test]
    public async Task ICodeGenerator_on_static_class_Async()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( StaticHolder ) );
        (await configuration.RunAsync().ConfigureAwait(false)).LoadMap();
        CGen.Called.Should().BeTrue();
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public interface RawInterface
    {
    }

    [Test]
    public async Task ICodeGenerator_on_raw_interface_Async()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawInterface ) );
        (await configuration.RunAsync().ConfigureAwait(false)).LoadMap();
        CGen.Called.Should().BeTrue();
    }

    [ContextBoundDelegation( "CK.StObj.Engine.Tests.RawCodeGeneratorTests+CGen, CK.StObj.Engine.Tests" )]
    public enum EvenOnAnEnumItWorks
    {
    }

    [Test]
    public async Task ICodeGenerator_on_enum_Async()
    {
        CGen.Called = false;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( EvenOnAnEnumItWorks ) );
        (await configuration.RunAsync().ConfigureAwait(false)).LoadMap();
        CGen.Called.Should().BeTrue();
    }


}
