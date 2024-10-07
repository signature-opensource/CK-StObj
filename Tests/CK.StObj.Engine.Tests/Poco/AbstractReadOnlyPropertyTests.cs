using CK.Core;
using CK.StObj.Engine.Tests.CrisLike;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

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
            TestHelper.GetFailedCollectorResult( [typeof( IWithNonNullAbstract )], """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IWithNonNullAbstract', field: 'Some' has no default value.
                No default can be synthesized for non nullable '[AbstractPoco]CK.Core.IPoco'.
                """ );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( IWithNonNullAbstract2 ), typeof( ICommand ), typeof( IRealCommand )], """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IWithNonNullAbstract2', field: 'Some' has no default value.
                No default can be synthesized for non nullable '[AbstractPoco]CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.ICommand'.
                """ );
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IResolveSome ), typeof( ICommand ), typeof( IRealCommand ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var f = d.Create<IResolveSome>();
            f.Some.V.Should().Be( "Yes!" );
            var cmd = d.Create<IRealCommand>();
            f.Some = cmd;
            ((IWithNonNullAbstract)f).Some.Should().BeSameAs( cmd, "Implementations use the same backing field." );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IResolveSome2 ), typeof( ICommand ), typeof( IRealCommand ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IWithNullAbstract ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var f = d.Create<IWithNullAbstract>();
            f.Some.Should().Be( null );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IWithNullAbstract2 ), typeof( ICommand ), typeof( IRealCommand ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
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
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( tPrimary, tExtension );
        using var auto = configuration.Run().CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
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

    [CKTypeDefiner]
    public interface IHaveNullableAutoProperty
    {
        object? Auto { get; }
    }
    public interface IAutoIAbstract1 : IPoco, IHaveNullableAutoProperty
    {
        new IAbstractCommand? Auto { get; }
    }
    public interface IAutoIAbstract2 : IPoco, IHaveNullableAutoProperty
    {
        new IPoco? Auto { get; }
    }

    [TestCase( typeof( IAutoIAbstract1 ) )]
    [TestCase( typeof( IAutoIAbstract2 ) )]
    public void object_abstract_readonly_property_can_be_nullable_AbstractPoco( Type tPrimary )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( tPrimary );
        using var auto = configuration.Run().CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var f = d.Find( tPrimary );
        Debug.Assert( f != null );
        var o = (IHaveNullableAutoProperty)f.Create();
        o.Auto.Should().BeNull();
    }

    [Test]
    public void record_cannot_be_a_Abstract_Read_Only_Property()
    {
        {
            TestHelper.GetFailedCollectorResult( [typeof( IInvalidAnonymousRecord )],
                "Property 'CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IInvalidAnonymousRecord.NoWay' must be a ref property: 'ref (int A,int B) NoWay { get; }'." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( IInvalidNamedRecord )], "Property 'CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IInvalidNamedRecord.NoWay' must be a ref property: 'ref CK.StObj.Engine.Tests.Poco.AbstractReadOnlyPropertyTests.IInvalidNamedRecord.Rec NoWay { get; }'." );
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
        IReadOnlyList<IPoco> ReadOnlyList { get; }
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
        new IList<IRealCommand> ReadOnlyList { get; }
    }

    [Test]
    public void abstract_properties_samples()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IHaveLotOfAbstractProperties ), typeof( IImplementThem ), typeof( IRealCommand ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var impl = d.Create<IImplementThem>();

        impl.Object = 3712;
        impl.ValueType = 42;
        impl.BasicRefType = "foo";
        impl.Array = [1, 2, 3];
        impl.ReadOnlyList.Add( impl.Poco );
        impl.Poco = d.Create<IRealCommand>( c => c.V = "Changed!" );
        impl.ReadOnlyList.Add( impl.Poco );
        impl.ReadOnlyList.Should().HaveCount( 2 );


        var abs = (IHaveLotOfAbstractProperties)impl;
        abs.Object.Should().Be( 3712 );
        abs.ValueType.Should().Be( 42 );
        abs.BasicRefType.Should().Be( "foo" );
        abs.Array.Should().BeSameAs( impl.Array );
        abs.ReadOnlyList.Should().BeSameAs( impl.ReadOnlyList );
    }
}
