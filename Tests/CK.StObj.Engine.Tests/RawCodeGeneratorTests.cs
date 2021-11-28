using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
            var c = TestHelper.CreateStObjCollector( typeof( Holder ) );
            var r = TestHelper.GetSuccessfulResult( c );
            Debug.Assert( r.EngineMap != null );
            r.EngineMap.AllTypesAttributesCache.Values.Select( a => a.Type ).Should().Contain( typeof( Holder ) );
            TestHelper.GenerateCode( r ).CodeGen.Success.Should().BeTrue();
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
            var c = TestHelper.CreateStObjCollector( typeof( StaticHolder ) );
            TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeTrue();
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
            var c = TestHelper.CreateStObjCollector( typeof( RawInterface ) );
            TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeTrue();
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
            var c = TestHelper.CreateStObjCollector( typeof( EvenOnAnEnumItWorks ) );
            TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeTrue();
            CGen.Called.Should().BeTrue();
        }


    }
}
