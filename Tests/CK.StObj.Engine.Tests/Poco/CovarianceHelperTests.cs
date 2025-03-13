using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.StObj.Engine.Tests.Poco;

public class CovarianceHelperTests
{
    [Test]
    public void Covariant_List_GetEnumerator()
    {
        {
            var l = new CovariantHelpers.CovNotNullValueList<int>() { 1, 2 };
            foreach( var e in (IEnumerable)l )
            {
                ((int)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 3 );
            }
            foreach( var e in (IEnumerable<object>)l )
            {
                ((int)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 3 );
            }
            foreach( var e in (IEnumerable<int?>)l )
            {
                ((int?)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 3 );
            }
        }
        {
            var l = new CovariantHelpers.CovNullableValueList<int>() { 1, 2 };
            foreach( var e in (IEnumerable)l )
            {
                ((int?)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 3 );
            }
            foreach( var e in (IEnumerable<object?>)l )
            {
                ((int?)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 3 );
            }
        }
    }

    [Test]
    public void CovNotNullValueHashSet_T_is_IReadOnlySet_object()
    {
        var set = new CovariantHelpers.CovNotNullValueHashSet<int>() { 1, 2, 3 };
        IReadOnlySet<object> nSet = set;

        var empty = new object[] { };
        var set2 = new object[] { 1, 2, 3 };
        var superSet2 = new object[] { 1, 2, 3, this };
        var subSet2 = new object[] { 1, 2 };
        var otherSet = new object[] { 0 };

        nSet.Contains( this ).ShouldBeFalse();
        nSet.Contains( null! ).ShouldBeFalse();

        nSet.SetEquals( set ).ShouldBeTrue();
        nSet.SetEquals( empty ).ShouldBeFalse();
        nSet.SetEquals( set2 ).ShouldBeTrue();
        nSet.SetEquals( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.SetEquals( superSet2 ).ShouldBeFalse();
        nSet.SetEquals( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
        nSet.SetEquals( subSet2 ).ShouldBeFalse();
        nSet.SetEquals( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
        nSet.SetEquals( otherSet ).ShouldBeFalse();
        nSet.SetEquals( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsProperSubsetOf( set ).ShouldBeFalse();
        nSet.IsProperSubsetOf( empty ).ShouldBeFalse();
        nSet.IsProperSubsetOf( set2 ).ShouldBeFalse();
        nSet.IsProperSubsetOf( set2.Concat( set2 ) ).ShouldBeFalse();
        nSet.IsProperSubsetOf( superSet2 ).ShouldBeTrue();
        nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
        nSet.IsProperSubsetOf( subSet2 ).ShouldBeFalse();
        nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
        nSet.IsProperSubsetOf( otherSet ).ShouldBeFalse();
        nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsSubsetOf( set ).ShouldBeTrue();
        nSet.IsSubsetOf( empty ).ShouldBeFalse();
        nSet.IsSubsetOf( set2 ).ShouldBeTrue();
        nSet.IsSubsetOf( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.IsSubsetOf( superSet2 ).ShouldBeTrue();
        nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
        nSet.IsSubsetOf( subSet2 ).ShouldBeFalse();
        nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
        nSet.IsSubsetOf( otherSet ).ShouldBeFalse();
        nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsProperSupersetOf( set ).ShouldBeFalse();
        nSet.IsProperSupersetOf( empty ).ShouldBeTrue();
        nSet.IsProperSupersetOf( set2 ).ShouldBeFalse();
        nSet.IsProperSupersetOf( set2.Concat( set2 ) ).ShouldBeFalse();
        nSet.IsProperSupersetOf( superSet2 ).ShouldBeFalse();
        nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
        nSet.IsProperSupersetOf( subSet2 ).ShouldBeTrue();
        nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
        nSet.IsProperSupersetOf( otherSet ).ShouldBeFalse();
        nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsSupersetOf( set ).ShouldBeTrue();
        nSet.IsSupersetOf( empty ).ShouldBeTrue();
        nSet.IsSupersetOf( set2 ).ShouldBeTrue();
        nSet.IsSupersetOf( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.IsSupersetOf( superSet2 ).ShouldBeFalse();
        nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
        nSet.IsSupersetOf( subSet2 ).ShouldBeTrue();
        nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
        nSet.IsSupersetOf( otherSet ).ShouldBeFalse();
        nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.Overlaps( set ).ShouldBeTrue();
        nSet.Overlaps( empty ).ShouldBeFalse();
        nSet.Overlaps( set2 ).ShouldBeTrue();
        nSet.Overlaps( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.Overlaps( superSet2 ).ShouldBeTrue();
        nSet.Overlaps( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
        nSet.Overlaps( subSet2 ).ShouldBeTrue();
        nSet.Overlaps( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
        nSet.Overlaps( otherSet ).ShouldBeFalse();
        nSet.Overlaps( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        foreach( var e in nSet )
        {
            e.ShouldBeOfType<int>();
            ((int)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 4 );
        }

        foreach( var e in (IEnumerable<int?>)nSet )
        {
            e.HasValue.ShouldBeTrue();
            ((int?)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 4 );
        }
    }

    [Test]
    public void CovNotNullValueHashSet_T_is_IReadOnlySet_T_Nullable()
    {
        var set = new CovariantHelpers.CovNotNullValueHashSet<int>() { 1, 2, 3 };
        IReadOnlySet<int?> nSet = set;

        var empty = new int?[] { };
        var set2 = new int?[] { 1, 2, 3 };
        var superSet2 = new int?[] { 1, 2, 3, null };
        var subSet2 = new int?[] { 1, 2 };
        var otherSet = new int?[] { 0 };

        nSet.Contains( null ).ShouldBeFalse();

        nSet.SetEquals( set ).ShouldBeTrue();
        nSet.SetEquals( empty ).ShouldBeFalse();
        nSet.SetEquals( set2 ).ShouldBeTrue();
        nSet.SetEquals( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.SetEquals( superSet2 ).ShouldBeFalse();
        nSet.SetEquals( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
        nSet.SetEquals( subSet2 ).ShouldBeFalse();
        nSet.SetEquals( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
        nSet.SetEquals( otherSet ).ShouldBeFalse();
        nSet.SetEquals( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsProperSubsetOf( set ).ShouldBeFalse();
        nSet.IsProperSubsetOf( empty ).ShouldBeFalse();
        nSet.IsProperSubsetOf( set2 ).ShouldBeFalse();
        nSet.IsProperSubsetOf( set2.Concat( set2 ) ).ShouldBeFalse();
        nSet.IsProperSubsetOf( superSet2 ).ShouldBeTrue();
        nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
        nSet.IsProperSubsetOf( subSet2 ).ShouldBeFalse();
        nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
        nSet.IsProperSubsetOf( otherSet ).ShouldBeFalse();
        nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsSubsetOf( set ).ShouldBeTrue();
        nSet.IsSubsetOf( empty ).ShouldBeFalse();
        nSet.IsSubsetOf( set2 ).ShouldBeTrue();
        nSet.IsSubsetOf( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.IsSubsetOf( superSet2 ).ShouldBeTrue();
        nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
        nSet.IsSubsetOf( subSet2 ).ShouldBeFalse();
        nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
        nSet.IsSubsetOf( otherSet ).ShouldBeFalse();
        nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsProperSupersetOf( set ).ShouldBeFalse();
        nSet.IsProperSupersetOf( empty ).ShouldBeTrue();
        nSet.IsProperSupersetOf( set2 ).ShouldBeFalse();
        nSet.IsProperSupersetOf( set2.Concat( set2 ) ).ShouldBeFalse();
        nSet.IsProperSupersetOf( superSet2 ).ShouldBeFalse();
        nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
        nSet.IsProperSupersetOf( subSet2 ).ShouldBeTrue();
        nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
        nSet.IsProperSupersetOf( otherSet ).ShouldBeFalse();
        nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.IsSupersetOf( set ).ShouldBeTrue();
        nSet.IsSupersetOf( empty ).ShouldBeTrue();
        nSet.IsSupersetOf( set2 ).ShouldBeTrue();
        nSet.IsSupersetOf( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.IsSupersetOf( superSet2 ).ShouldBeFalse();
        nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
        nSet.IsSupersetOf( subSet2 ).ShouldBeTrue();
        nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
        nSet.IsSupersetOf( otherSet ).ShouldBeFalse();
        nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

        nSet.Overlaps( set ).ShouldBeTrue();
        nSet.Overlaps( empty ).ShouldBeFalse();
        nSet.Overlaps( set2 ).ShouldBeTrue();
        nSet.Overlaps( set2.Concat( set2 ) ).ShouldBeTrue();
        nSet.Overlaps( superSet2 ).ShouldBeTrue();
        nSet.Overlaps( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
        nSet.Overlaps( subSet2 ).ShouldBeTrue();
        nSet.Overlaps( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
        nSet.Overlaps( otherSet ).ShouldBeFalse();
        nSet.Overlaps( otherSet.Concat( otherSet ) ).ShouldBeFalse();
    }

    [Test]
    public void CovNullableValueHashSet_T_is_IReadOnlySet_object_Nullable()
    {
        // Without null inside.
        {
            var set = new CovariantHelpers.CovNullableValueHashSet<int>() { 1, 2, 3 };
            IReadOnlySet<object?> nSet = set;

            var empty = new object?[] { };
            var set2 = new object?[] { 1, 2, 3 };
            var superSet2 = new object?[] { 1, 2, 3, this };
            var subSet2 = new object?[] { 1, 2 };
            var otherSet = new object?[] { 0 };

            nSet.Contains( null ).ShouldBeFalse();

            nSet.SetEquals( set ).ShouldBeTrue();
            nSet.SetEquals( empty ).ShouldBeFalse();
            nSet.SetEquals( set2 ).ShouldBeTrue();
            nSet.SetEquals( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.SetEquals( superSet2 ).ShouldBeFalse();
            nSet.SetEquals( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
            nSet.SetEquals( subSet2 ).ShouldBeFalse();
            nSet.SetEquals( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
            nSet.SetEquals( otherSet ).ShouldBeFalse();
            nSet.SetEquals( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsProperSubsetOf( set ).ShouldBeFalse();
            nSet.IsProperSubsetOf( empty ).ShouldBeFalse();
            nSet.IsProperSubsetOf( set2 ).ShouldBeFalse();
            nSet.IsProperSubsetOf( set2.Concat( set2 ) ).ShouldBeFalse();
            nSet.IsProperSubsetOf( superSet2 ).ShouldBeTrue();
            nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
            nSet.IsProperSubsetOf( subSet2 ).ShouldBeFalse();
            nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
            nSet.IsProperSubsetOf( otherSet ).ShouldBeFalse();
            nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsSubsetOf( set ).ShouldBeTrue();
            nSet.IsSubsetOf( empty ).ShouldBeFalse();
            nSet.IsSubsetOf( set2 ).ShouldBeTrue();
            nSet.IsSubsetOf( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.IsSubsetOf( superSet2 ).ShouldBeTrue();
            nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
            nSet.IsSubsetOf( subSet2 ).ShouldBeFalse();
            nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
            nSet.IsSubsetOf( otherSet ).ShouldBeFalse();
            nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsProperSupersetOf( set ).ShouldBeFalse();
            nSet.IsProperSupersetOf( empty ).ShouldBeTrue();
            nSet.IsProperSupersetOf( set2 ).ShouldBeFalse();
            nSet.IsProperSupersetOf( set2.Concat( set2 ) ).ShouldBeFalse();
            nSet.IsProperSupersetOf( superSet2 ).ShouldBeFalse();
            nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
            nSet.IsProperSupersetOf( subSet2 ).ShouldBeTrue();
            nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
            nSet.IsProperSupersetOf( otherSet ).ShouldBeFalse();
            nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsSupersetOf( set ).ShouldBeTrue();
            nSet.IsSupersetOf( empty ).ShouldBeTrue();
            nSet.IsSupersetOf( set2 ).ShouldBeTrue();
            nSet.IsSupersetOf( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.IsSupersetOf( superSet2 ).ShouldBeFalse();
            nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
            nSet.IsSupersetOf( subSet2 ).ShouldBeTrue();
            nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
            nSet.IsSupersetOf( otherSet ).ShouldBeFalse();
            nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.Overlaps( set ).ShouldBeTrue();
            nSet.Overlaps( empty ).ShouldBeFalse();
            nSet.Overlaps( set2 ).ShouldBeTrue();
            nSet.Overlaps( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.Overlaps( superSet2 ).ShouldBeTrue();
            nSet.Overlaps( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
            nSet.Overlaps( subSet2 ).ShouldBeTrue();
            nSet.Overlaps( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
            nSet.Overlaps( otherSet ).ShouldBeFalse();
            nSet.Overlaps( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            foreach( var e in nSet )
            {
                ((int?)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 4 );
            }

            foreach( var e in (IEnumerable<int?>)nSet )
            {
                e.HasValue.ShouldBeTrue();
                ((int?)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 4 );
            }

        }
        // With null inside.
        {
            var set = new CovariantHelpers.CovNullableValueHashSet<int>() { 1, null, 3 };
            IReadOnlySet<object?> nSet = set;

            var empty = new object?[] { };
            var set2 = new object?[] { 1, null, 3 };
            var superSet2 = new object?[] { 1, null, 3, this };
            var subSet2 = new object?[] { 1, null };
            var otherSet = new object?[] { 0 };

            nSet.Contains( 2 ).ShouldBeFalse();

            nSet.SetEquals( set ).ShouldBeTrue();
            nSet.SetEquals( empty ).ShouldBeFalse();
            nSet.SetEquals( set2 ).ShouldBeTrue();
            nSet.SetEquals( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.SetEquals( superSet2 ).ShouldBeFalse();
            nSet.SetEquals( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
            nSet.SetEquals( subSet2 ).ShouldBeFalse();
            nSet.SetEquals( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
            nSet.SetEquals( otherSet ).ShouldBeFalse();
            nSet.SetEquals( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsProperSubsetOf( set ).ShouldBeFalse();
            nSet.IsProperSubsetOf( empty ).ShouldBeFalse();
            nSet.IsProperSubsetOf( set2 ).ShouldBeFalse();
            nSet.IsProperSubsetOf( set2.Concat( set2 ) ).ShouldBeFalse();
            nSet.IsProperSubsetOf( superSet2 ).ShouldBeTrue();
            nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
            nSet.IsProperSubsetOf( subSet2 ).ShouldBeFalse();
            nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
            nSet.IsProperSubsetOf( otherSet ).ShouldBeFalse();
            nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsSubsetOf( set ).ShouldBeTrue();
            nSet.IsSubsetOf( empty ).ShouldBeFalse();
            nSet.IsSubsetOf( set2 ).ShouldBeTrue();
            nSet.IsSubsetOf( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.IsSubsetOf( superSet2 ).ShouldBeTrue();
            nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
            nSet.IsSubsetOf( subSet2 ).ShouldBeFalse();
            nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).ShouldBeFalse();
            nSet.IsSubsetOf( otherSet ).ShouldBeFalse();
            nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsProperSupersetOf( set ).ShouldBeFalse();
            nSet.IsProperSupersetOf( empty ).ShouldBeTrue();
            nSet.IsProperSupersetOf( set2 ).ShouldBeFalse();
            nSet.IsProperSupersetOf( set2.Concat( set2 ) ).ShouldBeFalse();
            nSet.IsProperSupersetOf( superSet2 ).ShouldBeFalse();
            nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
            nSet.IsProperSupersetOf( subSet2 ).ShouldBeTrue();
            nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
            nSet.IsProperSupersetOf( otherSet ).ShouldBeFalse();
            nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.IsSupersetOf( set ).ShouldBeTrue();
            nSet.IsSupersetOf( empty ).ShouldBeTrue();
            nSet.IsSupersetOf( set2 ).ShouldBeTrue();
            nSet.IsSupersetOf( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.IsSupersetOf( superSet2 ).ShouldBeFalse();
            nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).ShouldBeFalse();
            nSet.IsSupersetOf( subSet2 ).ShouldBeTrue();
            nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
            nSet.IsSupersetOf( otherSet ).ShouldBeFalse();
            nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            nSet.Overlaps( set ).ShouldBeTrue();
            nSet.Overlaps( empty ).ShouldBeFalse();
            nSet.Overlaps( set2 ).ShouldBeTrue();
            nSet.Overlaps( set2.Concat( set2 ) ).ShouldBeTrue();
            nSet.Overlaps( superSet2 ).ShouldBeTrue();
            nSet.Overlaps( superSet2.Concat( superSet2 ) ).ShouldBeTrue();
            nSet.Overlaps( subSet2 ).ShouldBeTrue();
            nSet.Overlaps( subSet2.Concat( subSet2 ) ).ShouldBeTrue();
            nSet.Overlaps( otherSet ).ShouldBeFalse();
            nSet.Overlaps( otherSet.Concat( otherSet ) ).ShouldBeFalse();

            foreach( var e in nSet )
            {
                if( e != null ) ((int)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 4 );
            }

            foreach( var e in (IEnumerable<int?>)nSet )
            {
                if( e.HasValue ) ((int)e).ShouldBeGreaterThan( 0 ).And.BeLessThan( 4 );
            }
        }
    }

    [Test]
    public void CovNotNullValueDictionary_TValue_is_IReadOnlyDictionary_T_Nullable()
    {
        var d = new CovariantHelpers.CovNotNullValueDictionary<int, byte>() { { 0, 1 } };
        IReadOnlyDictionary<int, byte?> dN = d;
        dN[0].ShouldBe( 1 );
        dN.Values.ShouldHaveSingleItem().ShouldBe( 1 );
        dN.Contains( new KeyValuePair<int, byte?>( 0, null ) ).ShouldBeFalse();
        dN.Contains( new KeyValuePair<int, byte?>( 0, 1 ) ).ShouldBeTrue();
    }

    [Test]
    public void CovNotNullValueDictionary_TValue_is_IReadOnlyDictionary_object()
    {
        var d = new CovariantHelpers.CovNotNullValueDictionary<int, byte>() { { 0, 1 } };
        IReadOnlyDictionary<int, object> dN = d;
        dN[0].ShouldBe( 1 );
        dN.Values.ShouldHaveSingleItem().ShouldBe( 1 );
        dN.Contains( new KeyValuePair<int, object>( 0, this ) ).ShouldBeFalse();
        dN.Contains( new KeyValuePair<int, object>( 0, (byte)1 ) ).ShouldBeTrue();
    }

    [Test]
    public void CovNullableValueDictionary_TValue_is_IReadOnlyDictionary_object_nullable()
    {
        var d = new CovariantHelpers.CovNullableValueDictionary<int, byte>() { { 0, 1 }, { 1, null } };
        IReadOnlyDictionary<int, object?> dN = d;
        dN[0].ShouldBe( 1 );
        dN.Values.Count.ShouldBe( 2 ).And.OnlyContain( b => b == null || b.Equals( (byte)1 ) );
        dN.Contains( new KeyValuePair<int, object?>( 0, this ) ).ShouldBeFalse();
        dN.Contains( new KeyValuePair<int, object?>( 0, (byte)1 ) ).ShouldBeTrue();
        dN.Contains( new KeyValuePair<int, object?>( 1, null ) ).ShouldBeTrue();
    }


}
