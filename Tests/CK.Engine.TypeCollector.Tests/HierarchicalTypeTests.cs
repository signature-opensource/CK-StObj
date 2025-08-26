using Shouldly;
using NUnit.Framework;
using CK.Core;
using System;

namespace CK.Engine.TypeCollector.Tests;

[TestFixture]
public class HierarchicalTypeTests
{
    [HierarchicalType<Invalid>]
    public interface Invalid { }

    [Test]
    public void self_cycle_throws_ArgumentException()
    {
        var cache = new GlobalTypeCache();
        var t = cache.Get( typeof( Invalid ) );
        Should.Throw<ArgumentException>( () => t.HierarchicalTypePath )
            .Message.ShouldBe( "Invalid recursive [HierarchicalType<Invalid>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.Invalid'. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalType )
            .Message.ShouldBe( "Invalid recursive [HierarchicalType<Invalid>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.Invalid'. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalTypeRoot )
            .Message.ShouldBe( "Invalid recursive [HierarchicalType<Invalid>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.Invalid'. (Parameter 'TParent')" );
    }

    [HierarchicalType<InvalidA>]
    public interface InvalidB { }

    [HierarchicalType<InvalidB>]
    public interface InvalidA { }

    [Test]
    public void cycles_throws_ArgumentException()
    {
        var cache = new GlobalTypeCache();
        var t = cache.Get( typeof( InvalidA ) );
        Should.Throw<ArgumentException>( () => t.HierarchicalTypePath )
            .Message.ShouldBe( "Invalid cycle in [HierarchicalType<T>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidA' <- 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidB' <- 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidA'. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalType )
            .Message.ShouldBe( "Invalid cycle in [HierarchicalType<T>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidA' <- 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidB' <- 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidA'. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalTypeRoot )
            .Message.ShouldBe( "Invalid cycle in [HierarchicalType<T>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidA' <- 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidB' <- 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.InvalidA'. (Parameter 'TParent')" );
    }

    [HierarchicalType<Action>]
    public interface BadParentType { }

    [Test]
    public void bad_parent_type_throws_ArgumentException()
    {
        var cache = new GlobalTypeCache();
        var t = cache.Get( typeof( BadParentType ) );
        var name = t.Name;
        Should.Throw<ArgumentException>( () => t.HierarchicalTypePath )
            .Message.ShouldBe( "Invalid type in [HierarchicalType<Action>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.BadParentType'. A hierachical type must be a struct or a class. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalType )
            .Message.ShouldBe( "Invalid type in [HierarchicalType<Action>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.BadParentType'. A hierachical type must be a struct or a class. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalTypeRoot )
            .Message.ShouldBe( "Invalid type in [HierarchicalType<Action>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.BadParentType'. A hierachical type must be a struct or a class. (Parameter 'TParent')" );
    }

    [HierarchicalType<HierarchicalTypeTests>]
    public interface UnmarkedParent { }

    [Test]
    public void unmarked_parent_throws_ArgumentException()
    {
        var cache = new GlobalTypeCache();
        var t = cache.Get( typeof( UnmarkedParent ) );
        var name = t.Name;
        Should.Throw<ArgumentException>( () => t.HierarchicalTypePath )
            .Message.ShouldBe( $"Invalid [HierarchicalType<HierarchicalTypeTests>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.UnmarkedParent': type 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests' is not marked as a hierarchical type. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalType )
            .Message.ShouldBe( $"Invalid [HierarchicalType<HierarchicalTypeTests>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.UnmarkedParent': type 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests' is not marked as a hierarchical type. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalTypeRoot )
            .Message.ShouldBe( $"Invalid [HierarchicalType<HierarchicalTypeTests>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.UnmarkedParent': type 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests' is not marked as a hierarchical type. (Parameter 'TParent')" );
    }

}
