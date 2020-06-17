using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using CK.StObj.Engine.Tests.Poco;
using System.Linq;

using static CK.Testing.StObjEngineTestHelper;
using FluentAssertions;
using System.Diagnostics;
using System.Reflection;

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public class PocoTests
    {
        [Test]
        public void simple_poco_resolution_and_injection()
        {
            StObjCollectorResult result = BuildPocoSample();
            Debug.Assert( result.EngineMap != null );

            IStObjResult p = result.EngineMap.StObjs.ToHead( typeof( PackageWithBasicPoco ) );
            var package = (PackageWithBasicPoco)p.FinalImplementation.Implementation;
            IBasicPoco poco = package.Factory.Create();
            Assert.That( poco is IEAlternateBasicPoco );
            Assert.That( poco is IEBasicPoco );
            Assert.That( poco is IECombineBasicPoco );
            Assert.That( poco is IEIndependentBasicPoco );

            var fEI = result.EngineMap.StObjs.Obtain<IPocoFactory<IEIndependentBasicPoco>>();
            IEIndependentBasicPoco ei = fEI.Create();
            ei.BasicProperty = 3;
            ei.IndependentProperty = 9;
        }

        static StObjCollectorResult BuildPocoSample()
        {
            var types = typeof(PocoTests).Assembly.GetTypes()
                            .Where( t => t.Namespace == "CK.StObj.Engine.Tests.Poco" );

            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );

            var result = collector.GetResult();
            Assert.That( result.HasFatalError, Is.False );
            return result;
        }

        [CKTypeDefiner]
        public interface IThingBase : IPoco
        {
            int BaseId { get; set; }
        }

        public interface IThing : IThingBase
        {
            int SpecId { get; set; }
        }

        [Test]
        public void poco_marked_with_CKTypeDefiner_are_not_registered()
        {
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterType( typeof( IThing ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var poco = collector.GetResult().CKTypeResult.PocoSupport;
            Debug.Assert( poco != null, "Since there has been no error." );
            poco.Roots.Should().HaveCount( 1 );
            poco.AllInterfaces.Should().HaveCount( 1 );
            poco.Find( typeof( IThingBase ) ).Should().BeNull();
            var factory = Activator.CreateInstance( poco.FinalFactory );
            Debug.Assert( factory != null );
            var thing = ((IPocoFactory<IThing>)factory).Create();
            thing.BaseId = 37;
            thing.SpecId = 12;
        }

        [Test]
        public void poco_factory_exposes_the_final_type()
        {
            StObjCollectorResult result = BuildPocoSample();
            Debug.Assert( result.EngineMap != null, "No error." );

            var p = result.EngineMap.StObjs.Obtain<IPocoFactory<IBasicPoco>>();

            Type pocoType = p.PocoClassType;
            Assert.That( typeof( IBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEAlternateBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IECombineBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEIndependentBasicPoco ).IsAssignableFrom( pocoType ) );

        }

        [Test]
        public void poco_support_read_only_properties()
        {
            StObjCollectorResult result = BuildPocoSample();
            Debug.Assert( result.EngineMap != null, "No error." );
            var p = result.EngineMap.StObjs.Obtain<IPocoFactory<IEBasicPocoWithReadOnly>>();
            var o = p.Create();

            Assert.That( o.ReadOnlyProperty, Is.EqualTo( 0 ) );
            PropertyInfo? prop = p.PocoClassType.GetProperty( nameof( IEBasicPocoWithReadOnly.ReadOnlyProperty ) );
            Debug.Assert( prop != null ); 
            prop.SetValue( o, 3712 );
            Assert.That( o.ReadOnlyProperty, Is.EqualTo( 3712 ) );
        }


    }
}
