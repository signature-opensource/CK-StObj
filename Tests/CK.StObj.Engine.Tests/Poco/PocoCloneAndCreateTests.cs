using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class PocoCloneAndCreateTests
{
    public interface IItem : IPoco
    {
        string Name { get; set; }
        IList<int> Values { get; }
    }

    public interface IBigOne : IPoco
    {
        int Power { get; set; }
        string? Text { get; set; }
        int[] Array { get; set; }
        IList<IItem> Items { get; }
        IDictionary<string, IItem> Map { get; }
        ISet<string> Tags { get; }
        IItem Item { get; }
        IItem? OptionalItem { get; set; }
        object? Any { get; set; }
        ref (int Count, string Name) Record { get; }
        List<(int Count, List<string> Names)> RecordList { get; set; }
        IList<IItem?> NullableItems { get; }
        IItem[] ItemArray { get; set; }
    }

    [Test]
    public async Task Clone_is_a_deep_copy_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IItem ), typeof( IBigOne ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<PocoDirectory>();

        var o = d.Create<IBigOne>( b =>
        {
            b.Power = 42;
            b.Text = "Text";
            b.Array = [1, 2, 3];
            b.Items.Add( d.Create<IItem>( i => { i.Name = "Item1"; i.Values.Add( 1 ); } ) );
            b.Map.Add( "Key", d.Create<IItem>( i => i.Name = "InMap" ) );
            b.Tags.Add( "Tag" );
            b.Item.Name = "Item";
            b.Item.Values.Add( 3712 );
            b.OptionalItem = d.Create<IItem>( i => i.Name = "Optional" );
            b.Any = d.Create<IItem>( i => i.Name = "Any" );
            b.Record.Count = 1;
            b.Record.Name = "Name";
            b.RecordList = [(1, ["InRecord"])];
            b.NullableItems.Add( null );
            b.NullableItems.Add( d.Create<IItem>( i => i.Name = "NotNull" ) );
            b.ItemArray = [d.Create<IItem>( i => i.Name = "InArray" )];
        } );

        IBigOne c = o.Clone();
        c.ShouldNotBeSameAs( o );

        c.Power.ShouldBe( 42 );
        c.Text.ShouldBe( "Text" );
        c.Array.ShouldBe( [1, 2, 3] );
        c.Array.ShouldNotBeSameAs( o.Array );

        c.Items.Single().Name.ShouldBe( "Item1" );
        c.Items.Single().ShouldNotBeSameAs( o.Items.Single() );
        c.Items.Single().Values.ShouldBe( [1] );
        c.Items.Single().Values.ShouldNotBeSameAs( o.Items.Single().Values );

        c.Map["Key"].Name.ShouldBe( "InMap" );
        c.Map["Key"].ShouldNotBeSameAs( o.Map["Key"] );
        c.Tags.ShouldBe( ["Tag"] );
        c.Tags.ShouldNotBeSameAs( o.Tags );

        c.Item.Name.ShouldBe( "Item" );
        c.Item.ShouldNotBeSameAs( o.Item );
        c.Item.Values.ShouldBe( [3712] );

        c.OptionalItem.ShouldNotBeNull().Name.ShouldBe( "Optional" );
        c.OptionalItem.ShouldNotBeSameAs( o.OptionalItem );

        c.Any.ShouldBeAssignableTo<IItem>().ShouldNotBeNull().Name.ShouldBe( "Any" );
        c.Any.ShouldNotBeSameAs( o.Any );

        c.Record.Count.ShouldBe( 1 );
        c.Record.Name.ShouldBe( "Name" );
        c.RecordList.ShouldNotBeSameAs( o.RecordList );
        c.RecordList[0].Names.ShouldBe( ["InRecord"] );
        c.RecordList[0].Names.ShouldNotBeSameAs( o.RecordList[0].Names );

        c.NullableItems.Count.ShouldBe( 2 );
        c.NullableItems[0].ShouldBeNull();
        c.NullableItems[1].ShouldNotBeNull().Name.ShouldBe( "NotNull" );
        c.NullableItems[1].ShouldNotBeSameAs( o.NullableItems[1] );

        c.ItemArray.Single().Name.ShouldBe( "InArray" );
        c.ItemArray.ShouldNotBeSameAs( o.ItemArray );
        c.ItemArray[0].ShouldNotBeSameAs( o.ItemArray[0] );

        // Changing the clone doesn't change the source.
        c.Items[0].Values.Add( 2 );
        c.Item.Values.Clear();
        c.RecordList[0].Names.Add( "Other" );
        ((IItem)c.Any).Name = "Changed";
        c.Map["Key"].Name = "Changed";
        c.ItemArray[0].Name = "Changed";
        o.Items[0].Values.ShouldBe( [1] );
        o.Item.Values.ShouldBe( [3712] );
        o.RecordList[0].Names.ShouldBe( ["InRecord"] );
        ((IItem)o.Any!).Name.ShouldBe( "Any" );
        o.Map["Key"].Name.ShouldBe( "InMap" );
        o.ItemArray[0].Name.ShouldBe( "InArray" );

        // The non generic Clone and the Create of the same type are clones.
        var c2 = (IBigOne)((IPocoGeneratedClass)o).Clone();
        c2.ShouldNotBeSameAs( o );
        c2.Item.Values.ShouldBe( [3712] );
        var c3 = o.Create<IBigOne>();
        c3.ShouldNotBeSameAs( o );
        c3.Map["Key"].Name.ShouldBe( "InMap" );
    }

    [CKTypeDefiner]
    public interface IOwnerPart : IPoco
    {
        string Name { get; set; }
    }

    public interface IOwnerA : IOwnerPart
    {
        int A { get; set; }
    }

    public interface IOwnerB : IOwnerPart
    {
        int B { get; set; }
    }

    [CKTypeDefiner]
    public interface IDocumentPart : IPoco
    {
        string Title { get; set; }
        IList<string> Lines { get; }
        IOwnerPart Owner { get; }
        IReadOnlyList<IOwnerPart> Readers { get; }
        ref (int Version, string Tag) Info { get; }
    }

    public interface IDocumentA : IDocumentPart
    {
        new IOwnerA Owner { get; }
        new IList<IOwnerA> Readers { get; }
        string OnlyA { get; set; }
        // Not in a shared part: never copied to another type.
        int SameName { get; set; }
    }

    public interface IDocumentB : IDocumentPart
    {
        new IOwnerB Owner { get; }
        new IList<IOwnerB> Readers { get; }
        string OnlyB { get; set; }
        int SameName { get; set; }
    }

    [Test]
    public async Task Create_copies_the_properties_of_the_shared_parts_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IOwnerA ), typeof( IOwnerB ), typeof( IDocumentA ), typeof( IDocumentB ), typeof( IItem ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<PocoDirectory>();

        var a = d.Create<IDocumentA>( a =>
        {
            a.Title = "Title";
            a.Lines.Add( "Line" );
            a.Owner.Name = "Owner";
            a.Owner.A = 1;
            a.Readers.Add( d.Create<IOwnerA>( r => { r.Name = "Reader"; r.A = 2; } ) );
            a.Info.Version = 3;
            a.Info.Tag = "Tag";
            a.OnlyA = "OnlyA";
            a.SameName = 3712;
        } );

        var b = a.Create<IDocumentB>();
        b.Title.ShouldBe( "Title" );
        b.Lines.ShouldBe( ["Line"] );
        b.Lines.ShouldNotBeSameAs( a.Lines );
        // The nested Pocos are created from the source ones.
        b.Owner.Name.ShouldBe( "Owner" );
        b.Owner.B.ShouldBe( 0 );
        b.Readers.Single().Name.ShouldBe( "Reader" );
        b.Info.Version.ShouldBe( 3 );
        b.Info.Tag.ShouldBe( "Tag" );
        // Properties that are not declared by a shared part keep their default.
        b.OnlyB.ShouldBe( "" );
        b.SameName.ShouldBe( 0 );

        // Back to a IDocumentA: the A specific properties are lost.
        var back = b.Create<IDocumentA>( x => x.OnlyA = "Configured" );
        back.Title.ShouldBe( "Title" );
        back.Owner.Name.ShouldBe( "Owner" );
        back.Owner.A.ShouldBe( 0 );
        back.Readers.Single().Name.ShouldBe( "Reader" );
        back.Readers.Single().A.ShouldBe( 0 );
        back.OnlyA.ShouldBe( "Configured" );

        // The same type is a clone.
        var clone = a.Create<IDocumentA>();
        clone.ShouldNotBeSameAs( a );
        clone.OnlyA.ShouldBe( "OnlyA" );
        clone.Readers.Single().A.ShouldBe( 2 );
        clone.Owner.A.ShouldBe( 1 );
        clone.SameName.ShouldBe( 3712 );

        // A type that shares nothing is a new default one.
        var item = a.Create<IItem>();
        item.Name.ShouldBe( "" );

        // An abstract Poco cannot be created.
        Util.Invokable( () => a.Create<IDocumentPart>() ).ShouldThrow<InvalidOperationException>();

        // The factory can be used directly.
        var f = auto.Services.GetRequiredService<IPocoFactory<IDocumentB>>();
        f.CreateFrom( a ).Title.ShouldBe( "Title" );
        ((IPocoFactory)f).CreateFrom( a ).ShouldBeAssignableTo<IDocumentB>();
    }
}
