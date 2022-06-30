using CK.CodeGen;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using static CK.Testing.StObjEngineTestHelper;
using FluentAssertions;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoExternalPropertyImplementationTests
    {

        public interface IPocoWithSpecialProperty : IPoco
        {
            [AutoImplementationClaim]
            int GlobalSequence { get; }

            int NormalOne { get; set; }
        }

        public class GlobalSequenceGeneratorImpl : ICSCodeGenerator
        {
            public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
            {
                foreach( var poco in c.Assembly.GetPocoSupportResult().Roots )
                {
                    foreach( var p in poco.ExternallyImplementedPropertyList )
                    {
                        if( p.Name == "GlobalSequence" )
                        {
                            ITypeScope? tB = c.Assembly.FindOrCreateAutoImplementedClass( monitor, poco.PocoClass );
                            tB.Append( "public int GlobalSequence => 45343;" );
                        }
                    }
                }
                return CSCodeGenerationResult.Success;
            }
        }

        [ContextBoundDelegation( "CK.StObj.Engine.Tests.Poco.PocoExternalPropertyImplementationTests+GlobalSequenceGeneratorImpl, CK.StObj.Engine.Tests" )]
        public class GlobalSequenceGenerator : IRealObject
        {
        }

        [Test]
        public void some_poco_properties_can_be_handled_by_independent_CodeGenerator()
        {
            var c = TestHelper.CreateStObjCollector( typeof( GlobalSequenceGenerator ), typeof( IPocoWithSpecialProperty ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IPocoWithSpecialProperty>>();
            var o = f.Create();
            o.GlobalSequence.Should().Be( 45343 );
        }

    }
}
