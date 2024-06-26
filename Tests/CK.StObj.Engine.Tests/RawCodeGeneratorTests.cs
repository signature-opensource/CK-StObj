using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CK.Testing;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests
{
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
            configuration.FirstBinPath.Add( typeof( Holder ) );
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
            configuration.FirstBinPath.Add( typeof( StaticHolder ) );
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
            configuration.FirstBinPath.Add( typeof( RawInterface ) );
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
            configuration.FirstBinPath.Add( typeof( EvenOnAnEnumItWorks ) );
            configuration.Run().LoadMap();
            CGen.Called.Should().BeTrue();
        }


    }
}
