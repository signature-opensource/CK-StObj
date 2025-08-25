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
    public void auto_recursive_throws_ArgumentException()
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

    [TestCase(typeof(InvalidA))]
    public void cycles_throws_ArgumentException( Type tInvalid )
    {
        var cache = new GlobalTypeCache();
        var t = cache.Get( tInvalid );
        Should.Throw<ArgumentException>( () => t.HierarchicalTypePath )
            .Message.ShouldBe( "Invalid recursive [HierarchicalType<Invalid>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.Invalid'. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalType )
            .Message.ShouldBe( "Invalid recursive [HierarchicalType<Invalid>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.Invalid'. (Parameter 'TParent')" );
        Should.Throw<ArgumentException>( () => t.IsHierarchicalTypeRoot )
            .Message.ShouldBe( "Invalid recursive [HierarchicalType<Invalid>] on 'CK.Engine.TypeCollector.Tests.HierarchicalTypeTests.Invalid'. (Parameter 'TParent')" );
    }

}
