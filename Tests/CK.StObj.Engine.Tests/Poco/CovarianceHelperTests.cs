using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.StObj.Engine.Tests.Poco
{
    public class CovarianceHelperTests
    {
        [Test]
        public void CovNotNullValueHashSet_T_is_IReadOnlySet_object()
        {
            var set = new CovariantHelpers.CovNotNullValueHashSet<int>() { 1, 2, 3 };
            IReadOnlySet<object> nSet = set;

            var empty = new object[] {};
            var set2 = new object[] { 1, 2, 3 };
            var superSet2 = new object[] { 1, 2, 3, this };
            var subSet2 = new object[] { 1, 2 };
            var otherSet = new object[] { 0 };

            nSet.Contains( this ).Should().BeFalse();
            nSet.Contains( null! ).Should().BeFalse();

            nSet.SetEquals( set ).Should().BeTrue();
            nSet.SetEquals( empty ).Should().BeFalse();
            nSet.SetEquals( set2 ).Should().BeTrue();
            nSet.SetEquals( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( superSet2.Concat( superSet2 ) ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2.Concat( subSet2 ) ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();
            nSet.SetEquals( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2.Concat( set2 ) ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeTrue();
            nSet.IsSubsetOf( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( set2.Concat( set2 ) ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();
            nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeTrue();
            nSet.IsSupersetOf( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();
            nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( superSet2.Concat( superSet2 ) ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2.Concat( subSet2 ) ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
            nSet.Overlaps( otherSet.Concat( otherSet ) ).Should().BeFalse();
        }

        [Test]
        public void CovNotNullValueHashSet_T_is_IReadOnlySet_T_Nullable()
        {
            var set = new CovariantHelpers.CovNotNullValueHashSet<int>() { 1, 2, 3 };
            IReadOnlySet<int?> nSet = set;

            var empty = new int?[] {};
            var set2 = new int?[] { 1, 2, 3 };
            var superSet2 = new int?[] { 1, 2, 3, null };
            var subSet2 = new int?[] { 1, 2 };
            var otherSet = new int?[] { 0 };

            nSet.Contains( null ).Should().BeFalse();

            nSet.SetEquals( set ).Should().BeTrue();
            nSet.SetEquals( empty ).Should().BeFalse();
            nSet.SetEquals( set2 ).Should().BeTrue();
            nSet.SetEquals( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( superSet2.Concat( superSet2 ) ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2.Concat( subSet2 ) ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();
            nSet.SetEquals( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2.Concat( set2 ) ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeTrue();
            nSet.IsSubsetOf( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( set2.Concat( set2 ) ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();
            nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeTrue();
            nSet.IsSupersetOf( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();
            nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( set2.Concat( set2 ) ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( superSet2.Concat( superSet2 ) ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2.Concat( subSet2 ) ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
            nSet.Overlaps( otherSet.Concat( otherSet ) ).Should().BeFalse();
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

                nSet.Contains( null ).Should().BeFalse();

                nSet.SetEquals( set ).Should().BeTrue();
                nSet.SetEquals( empty ).Should().BeFalse();
                nSet.SetEquals( set2 ).Should().BeTrue();
                nSet.SetEquals( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.SetEquals( superSet2 ).Should().BeFalse();
                nSet.SetEquals( superSet2.Concat( superSet2 ) ).Should().BeFalse();
                nSet.SetEquals( subSet2 ).Should().BeFalse();
                nSet.SetEquals( subSet2.Concat( subSet2 ) ).Should().BeFalse();
                nSet.SetEquals( otherSet ).Should().BeFalse();
                nSet.SetEquals( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsProperSubsetOf( set ).Should().BeFalse();
                nSet.IsProperSubsetOf( empty ).Should().BeFalse();
                nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( set2.Concat( set2 ) ).Should().BeFalse();
                nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
                nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
                nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();
                nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsSubsetOf( set ).Should().BeTrue();
                nSet.IsSubsetOf( empty ).Should().BeFalse();
                nSet.IsSubsetOf( set2 ).Should().BeTrue();
                nSet.IsSubsetOf( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
                nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
                nSet.IsSubsetOf( otherSet ).Should().BeFalse();
                nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsProperSupersetOf( set ).Should().BeFalse();
                nSet.IsProperSupersetOf( empty ).Should().BeTrue();
                nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( set2.Concat( set2 ) ).Should().BeFalse();
                nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
                nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
                nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();
                nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsSupersetOf( set ).Should().BeTrue();
                nSet.IsSupersetOf( empty ).Should().BeTrue();
                nSet.IsSupersetOf( set2 ).Should().BeTrue();
                nSet.IsSupersetOf( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
                nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
                nSet.IsSupersetOf( otherSet ).Should().BeFalse();
                nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.Overlaps( set ).Should().BeTrue();
                nSet.Overlaps( empty ).Should().BeFalse();
                nSet.Overlaps( set2 ).Should().BeTrue();
                nSet.Overlaps( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.Overlaps( superSet2 ).Should().BeTrue();
                nSet.Overlaps( superSet2.Concat( superSet2 ) ).Should().BeTrue();
                nSet.Overlaps( subSet2 ).Should().BeTrue();
                nSet.Overlaps( subSet2.Concat( subSet2 ) ).Should().BeTrue();
                nSet.Overlaps( otherSet ).Should().BeFalse();
                nSet.Overlaps( otherSet.Concat( otherSet ) ).Should().BeFalse();
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

                nSet.Contains( 2 ).Should().BeFalse();

                nSet.SetEquals( set ).Should().BeTrue();
                nSet.SetEquals( empty ).Should().BeFalse();
                nSet.SetEquals( set2 ).Should().BeTrue();
                nSet.SetEquals( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.SetEquals( superSet2 ).Should().BeFalse();
                nSet.SetEquals( superSet2.Concat( superSet2 ) ).Should().BeFalse();
                nSet.SetEquals( subSet2 ).Should().BeFalse();
                nSet.SetEquals( subSet2.Concat( subSet2 ) ).Should().BeFalse();
                nSet.SetEquals( otherSet ).Should().BeFalse();
                nSet.SetEquals( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsProperSubsetOf( set ).Should().BeFalse();
                nSet.IsProperSubsetOf( empty ).Should().BeFalse();
                nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( set2.Concat( set2 ) ).Should().BeFalse();
                nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsProperSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
                nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
                nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();
                nSet.IsProperSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsSubsetOf( set ).Should().BeTrue();
                nSet.IsSubsetOf( empty ).Should().BeFalse();
                nSet.IsSubsetOf( set2 ).Should().BeTrue();
                nSet.IsSubsetOf( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsSubsetOf( superSet2.Concat( superSet2 ) ).Should().BeTrue();
                nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsSubsetOf( subSet2.Concat( subSet2 ) ).Should().BeFalse();
                nSet.IsSubsetOf( otherSet ).Should().BeFalse();
                nSet.IsSubsetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsProperSupersetOf( set ).Should().BeFalse();
                nSet.IsProperSupersetOf( empty ).Should().BeTrue();
                nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( set2.Concat( set2 ) ).Should().BeFalse();
                nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
                nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsProperSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
                nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();
                nSet.IsProperSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.IsSupersetOf( set ).Should().BeTrue();
                nSet.IsSupersetOf( empty ).Should().BeTrue();
                nSet.IsSupersetOf( set2 ).Should().BeTrue();
                nSet.IsSupersetOf( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsSupersetOf( superSet2.Concat( superSet2 ) ).Should().BeFalse();
                nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsSupersetOf( subSet2.Concat( subSet2 ) ).Should().BeTrue();
                nSet.IsSupersetOf( otherSet ).Should().BeFalse();
                nSet.IsSupersetOf( otherSet.Concat( otherSet ) ).Should().BeFalse();

                nSet.Overlaps( set ).Should().BeTrue();
                nSet.Overlaps( empty ).Should().BeFalse();
                nSet.Overlaps( set2 ).Should().BeTrue();
                nSet.Overlaps( set2.Concat( set2 ) ).Should().BeTrue();
                nSet.Overlaps( superSet2 ).Should().BeTrue();
                nSet.Overlaps( superSet2.Concat( superSet2 ) ).Should().BeTrue();
                nSet.Overlaps( subSet2 ).Should().BeTrue();
                nSet.Overlaps( subSet2.Concat( subSet2 ) ).Should().BeTrue();
                nSet.Overlaps( otherSet ).Should().BeFalse();
                nSet.Overlaps( otherSet.Concat( otherSet ) ).Should().BeFalse();
            }
        }

        [Test]
        public void CovNotNullValueDictionary_TValue_is_IReadOnlyDictionary_T_Nullable()
        {
            var d = new CovariantHelpers.CovNotNullValueDictionary<int, byte>() { { 0, 1 } };
            IReadOnlyDictionary<int, byte?> dN = d;
            dN[0].Should().Be( 1 );
            dN.Values.Should().ContainSingle().And.OnlyContain( b => b == 1 );
            dN.Contains( new KeyValuePair<int, byte?>( 0, null ) ).Should().BeFalse();
            dN.Contains( new KeyValuePair<int, byte?>( 0, 1 ) ).Should().BeTrue();
        }

        [Test]
        public void CovNotNullValueDictionary_TValue_is_IReadOnlyDictionary_object()
        {
            var d = new CovariantHelpers.CovNotNullValueDictionary<int, byte>() { { 0, 1 } };
            IReadOnlyDictionary<int, object> dN = d;
            dN[0].Should().Be( 1 );
            dN.Values.Should().ContainSingle().And.OnlyContain( b => b.Equals( (byte)1 ) );
            dN.Contains( new KeyValuePair<int, object>( 0, this ) ).Should().BeFalse();
            dN.Contains( new KeyValuePair<int, object>( 0, (byte)1 ) ).Should().BeTrue();
        }

        [Test]
        public void CovNullableValueDictionary_TValue_is_IReadOnlyDictionary_object_nullable()
        {
            var d = new CovariantHelpers.CovNullableValueDictionary<int, byte>() { { 0, 1 }, { 1, null } };
            IReadOnlyDictionary<int, object?> dN = d;
            dN[0].Should().Be( 1 );
            dN.Values.Should().HaveCount( 2 ).And.OnlyContain( b => b == null || b.Equals( (byte)1 ) );
            dN.Contains( new KeyValuePair<int, object?>( 0, this ) ).Should().BeFalse();
            dN.Contains( new KeyValuePair<int, object?>( 0, (byte)1 ) ).Should().BeTrue();
            dN.Contains( new KeyValuePair<int, object?>( 1, null ) ).Should().BeTrue();
        }


    }
}
