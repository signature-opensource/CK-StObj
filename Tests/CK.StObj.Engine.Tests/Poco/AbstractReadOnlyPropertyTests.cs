using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.StObj.Engine.Tests.Poco.AnonymousRecordTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class AbstractReadOnlyPropertyTests
    {
        [CKTypeDefiner]
        public interface ICommand : IPoco
        {
            [DefaultValue( "Yes!" )]
            string V { get; set; }
        }

        public interface IRealCommand : ICommand { }

        public interface IWithNonNullAbstract : IPoco
        {
            IPoco Some { get; }
        }

        public interface IWithNonNullAbstract2 : IPoco
        {
            ICommand Some { get; }
        }

        [Test]
        public void non_nullable_abstract_IPoco_field_without_writable_is_an_error()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( IWithNonNullAbstract ) );
                TestHelper.GetFailedResult( c, "Unable to obtain a default value for 'Some', on 'CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IWithNonNullAbstract' default value cannot be generated. Should this be nullable?" );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IWithNonNullAbstract2 ), typeof( ICommand ), typeof( IRealCommand ) );
                TestHelper.GetFailedResult( c, "Unable to obtain a default value for 'Some', on 'CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IWithNonNullAbstract2' default value cannot be generated. Should this be nullable?" );
            }
        }

        public interface IResolveSome : IWithNonNullAbstract
        {
            new IRealCommand Some { get; set; }
        }

        public interface IResolveSome2 : IWithNonNullAbstract2
        {
            new IRealCommand Some { get; set; }
        }

        [Test]
        public void non_nullable_abstract_IPoco_field_must_have_a_concrete_writable_field()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( IResolveSome ), typeof( ICommand ), typeof( IRealCommand ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<PocoDirectory>();
                var f = d.Create<IResolveSome>();
                f.Some.V.Should().Be( "Yes!" );
                var cmd = d.Create<IRealCommand>();
                f.Some = cmd;
                ((IWithNonNullAbstract)f).Some.Should().BeSameAs( cmd, "Implementations use the same backing field." );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IResolveSome2 ), typeof( ICommand ), typeof( IRealCommand ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<PocoDirectory>();
                var f = d.Create<IResolveSome2>();
                f.Some.V.Should().Be( "Yes!" );
                var cmd = d.Create<IRealCommand>( c => c.V = "Changed!" );
                f.Some = cmd;
                ((IWithNonNullAbstract2)f).Some.V.Should().Be( "Changed!", "Implementations use the same backing field." );
            }
        }

        public interface IWithNullAbstract : IPoco
        {
            IPoco? Some { get; }
        }

        public interface IWithNullAbstract2 : IPoco
        {
            ICommand? Some { get; }
        }

        [Test]
        public void nullable_abstract_IPoco_field_without_writable_keeps_a_default_null_value()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( IWithNullAbstract ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<PocoDirectory>();
                var f = d.Create<IWithNullAbstract>();
                f.Some.Should().Be( null );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IWithNullAbstract2 ), typeof( ICommand ), typeof( IRealCommand ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<PocoDirectory>();
                var f = d.Create<IWithNullAbstract2>();
                f.Some.Should().Be( null );
            }
        }

        [CKTypeDefiner]
        public interface IHaveAutoProperty : IPoco
        {
            object Auto { get; }
        }

        #region Reference type (IReadOnlyList)
        public interface IAutoListPrimary1 : IPoco, IHaveAutoProperty
        {
            new IList<string> Auto { get; }
        }
        public interface IAutoListExtension1 : IAutoListPrimary1
        {
            new IReadOnlyList<string>? Auto { get; }
        }

        public interface IAutoListPrimary2 : IPoco, IHaveAutoProperty
        {
            new IReadOnlyList<string>? Auto { get; }
        }
        public interface IAutoListExtension2 : IAutoListPrimary2
        {
            new IList<string> Auto { get; }
        }
        #endregion

        #region Value type (int)
        public interface IAutoIntPrimary1 : IPoco, IHaveAutoProperty
        {
            new int Auto { get; }
        }
        public interface IAutoIntExtension1 : IAutoIntPrimary1
        {
            new int? Auto { get; }
        }

        public interface IAutoIntPrimary2 : IPoco, IHaveAutoProperty
        {
            new int? Auto { get; }
        }
        public interface IAutoIntExtension2 : IAutoIntPrimary2
        {
            new int Auto { get; }
        }
        #endregion

        #region Value type (anonymous record)
        public interface IAutoAnonymousRecordPrimary1 : IPoco, IHaveAutoProperty
        {
            new ref (int A, string B) Auto { get; }
        }
        public interface IAutoAnonymousRecordExtension1 : IAutoAnonymousRecordPrimary1
        {
            // Record cannot be Abstract Read Only Property. Use object here.
            new object? Auto { get; }
        }

        public interface IAutoAnonymousRecordPrimary2 : IPoco, IHaveAutoProperty
        {
            // Record cannot be Abstract Read Only Property. Use object here.
            new object? Auto { get; }
        }
        public interface IAutoAnonymousRecordExtension2 : IAutoAnonymousRecordPrimary2
        {
            new ref (int A, string B) Auto { get; }
        }
        #endregion

        [TestCase( typeof( List<string> ), typeof( IAutoListPrimary1 ), typeof( IAutoListExtension1 ) )]
        [TestCase( typeof( List<string> ), typeof( IAutoListPrimary2 ), typeof( IAutoListExtension2 ) )]
        [TestCase( typeof( int ), typeof( IAutoIntPrimary1 ), typeof( IAutoIntExtension1 ) )]
        [TestCase( typeof( int ), typeof( IAutoIntPrimary2 ), typeof( IAutoIntExtension2 ) )]
        [TestCase( typeof( (int, string) ), typeof( IAutoAnonymousRecordPrimary1 ), typeof( IAutoAnonymousRecordExtension1 ) )]
        [TestCase( typeof( (int, string) ), typeof( IAutoAnonymousRecordPrimary2 ), typeof( IAutoAnonymousRecordExtension2 ) )]
        public void auto_initialized_property_can_be_exposed_as_nullable_properties( Type tAutoProperty, Type tPrimary, Type tExtension )
        {
            var c = TestHelper.CreateStObjCollector( tPrimary, tExtension );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var f = d.Find( tPrimary );
            Debug.Assert( f != null );
            f.Should().BeSameAs( d.Find( tExtension ) );
            var o = (IHaveAutoProperty)f.Create();
            o.Auto.Should().NotBeNull().And.BeOfType( tAutoProperty );
        }

        public interface IInvalidAnonymousRecord : IPoco
        {
            (int A, int B) NoWay { get; }
        }

        public interface IInvalidNamedRecord : IPoco
        {
            public record struct Rec( int A, int B );
            Rec NoWay { get; }
        }

        [Test]
        public void record_cannot_be_a_Abstract_Read_Only_Property()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidAnonymousRecord ) );
                TestHelper.GetFailedResult( c,
                    "Property 'CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IInvalidAnonymousRecord.NoWay' must be a ref property: 'ref (int A,int B) NoWay { get; }'." );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidNamedRecord ) );
                TestHelper.GetFailedResult( c, "Property 'CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IInvalidNamedRecord.NoWay' must be a ref property: 'ref CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IInvalidNamedRecord.Rec NoWay { get; }'." );
            }
        }

        [CKTypeDefiner]
        public interface IHaveLotOfAbstractProperties : IPoco
        {
            object Object { get; }
            uint ValueType { get; }
            string BasicRefType { get; }
            int[] Array { get; }
            IPoco Poco { get; }
            IReadOnlySet<IPoco> ReadOnlySet { get; }
        }

        // None of the properties are nullable.
        // This works because one can either instantiate a concrete type or find a default value for each of them.
        public interface IImplementThem : IHaveLotOfAbstractProperties
        {
            new int Object { get; set; }
            new uint ValueType { get; set; }
            new string BasicRefType { get; set; }
            new int[] Array { get; set; }
            new IRealCommand Poco { get; set; }
            new ISet<IRealCommand> ReadOnlySet { get; }
        }

        [Test]
        public void abstract_properties_samples()
        {
            var c = TestHelper.CreateStObjCollector( typeof(IHaveLotOfAbstractProperties), typeof( IImplementThem ), typeof( IRealCommand ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var impl = d.Create<IImplementThem>();

            impl.Object = 3712;
            impl.ValueType = 42;
            impl.BasicRefType = "foo";
            impl.Array = new int[] { 1, 2, 3 };
            impl.ReadOnlySet.Add( impl.Poco );
            impl.Poco = d.Create<IRealCommand>( c => c.V = "Changed!" );
            impl.ReadOnlySet.Add( impl.Poco );
            impl.ReadOnlySet.Should().HaveCount( 2 );


            var abs = (IHaveLotOfAbstractProperties)impl;
            abs.Object.Should().Be( 3712 );
            abs.ValueType.Should().Be( 42 );
            abs.BasicRefType.Should().Be( "foo" );
            abs.Array.Should().BeSameAs( impl.Array );
            abs.ReadOnlySet.Should().BeSameAs( impl.ReadOnlySet );
        }
    }
}
