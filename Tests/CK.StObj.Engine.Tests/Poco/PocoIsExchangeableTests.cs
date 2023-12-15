using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;


namespace CK.StObj.Engine.Tests.Poco
{

    [TestFixture]
    public class PocoIsExchangeableTests
    {
        public interface IEmptyPoco : IPoco
        {
        }

        [Test]
        public void empty_IPoco_is_not_exchangeable()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEmptyPoco ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;
            var e = ts.FindByType( typeof( IEmptyPoco ) );
            Debug.Assert( e != null );
            e.IsExchangeable.Should().BeFalse();
        }

        [Test]
        public void when_no_IPoco_are_exchangeable_IPoco_itself_is_not_exchangeable()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEmptyPoco ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;
            var e = ts.FindByType( typeof( IPoco ) );
            Debug.Assert( e != null );
            e.IsExchangeable.Should().BeFalse();
        }

        public interface IRefEmptyPoco : IPoco
        {
            IEmptyPoco Empty { get; set; }

            string Data { get; set; }
        }

        [Test]
        public void not_exchangeable_type_leads_to_unexchangeable_fields()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEmptyPoco ), typeof( IRefEmptyPoco ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;
            var e = ts.FindByType( typeof( IRefEmptyPoco ) ) as IPrimaryPocoType;
            Debug.Assert( e != null );
            e.IsExchangeable.Should().BeTrue( "The string Data is still exchangeable." );
            e.Fields.Single( f => f.Name == "Empty" ).IsExchangeable.Should().BeFalse();
            e.Fields.Single( f => f.Name == "Data" ).IsExchangeable.Should().BeTrue();

            // Condemn the string! (this is rather stupid :)).
            var stringType = ts.FindByType( typeof( string ) );
            Debug.Assert( stringType != null );

            ts.SetNotExchangeable( TestHelper.Monitor, stringType );
            e.Fields.Single( f => f.Name == "Empty" ).IsExchangeable.Should().BeFalse();
            e.Fields.Single( f => f.Name == "Data" ).IsExchangeable.Should().BeFalse();
            e.IsExchangeable.Should().BeFalse( "Nothing is exchangeable any more." );
        }

        public interface IPocoWithCollection : IPoco
        {
            IEmptyPoco[] Empty { get; set; }

            string ROFieldIsNotExchangeable1 { get; }

            IReadOnlyList<string> ROFieldIsNotExchangeable2 { get; }

            IList<Dictionary<IEmptyPoco, string>> NotExchangeableBecauseOfKey { get; }

            IList<Dictionary<string, IEmptyPoco>> NotExchangeableBecauseOfValue { get; }

            int JustToBeSureThePocoIsExchangeable { get; set; }
        }

        [Test]
        public void IsExchangeable_through_collections()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEmptyPoco ), typeof( IPocoWithCollection ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;

            var poco = ts.FindByType( typeof( IPoco ) ) as IAbstractPocoType;
            Debug.Assert( poco != null );
            poco.IsExchangeable.Should().BeTrue();

            var e = ts.FindByType( typeof( IPocoWithCollection ) ) as IPrimaryPocoType;
            Debug.Assert( e != null );
            e.IsExchangeable.Should().BeTrue();
            e.Fields.Single( f => f.Name == "Empty" ).IsExchangeable.Should().BeFalse( "An array of non exchangeable." );
            e.Fields.Single( f => f.Name == "Empty" ).Type.IsExchangeable.Should().BeFalse( "An array of non exchangeable." );

            e.Fields.Single( f => f.Name == "ROFieldIsNotExchangeable1" ).IsExchangeable.Should().BeFalse();
            e.Fields.Single( f => f.Name == "ROFieldIsNotExchangeable2" ).IsExchangeable.Should().BeFalse();

            e.Fields.Single( f => f.Name == "NotExchangeableBecauseOfKey" ).IsExchangeable.Should().BeFalse();
            e.Fields.Single( f => f.Name == "NotExchangeableBecauseOfValue" ).IsExchangeable.Should().BeFalse();

            e.Fields.Single( f => f.Name == "JustToBeSureThePocoIsExchangeable" ).IsExchangeable.Should().BeTrue();

            // Condemn the int! (this is rather stupid :)).
            var intType = ts.FindByType( typeof( int ) );
            Debug.Assert( intType != null );

            ts.SetNotExchangeable( TestHelper.Monitor, intType );
            e.Fields.Single( f => f.Name == "JustToBeSureThePocoIsExchangeable" ).IsExchangeable.Should().BeFalse();
            e.IsExchangeable.Should().BeFalse();

            poco.IsExchangeable.Should().BeFalse();
        }

    }
}

