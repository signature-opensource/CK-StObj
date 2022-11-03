using CK.Core;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace CK.StObj.Engine.Tests.Poco
{
    public class LabForCovariance
    {
        [CKTypeDefiner]
        public interface ICommand : IPoco
        {
        }

        public interface IThing : ICommand
        {
            int Power { get; set; }
        }

        public interface IOther : IThing
        {
            int OtherPower { get; set; }
        }

        public interface IMoreOther : IOther
        {
        }

        class Thing_CK : IThing, IOther, IMoreOther
        {
            public int Power { get; set; }
            public int OtherPower { get; set; }
        }

        public sealed class CovPocoList_CK<TImpl> : List<TImpl>, IList<IThing>, IList<IOther>, IList<IMoreOther>
            where TImpl : class, IThing, IOther, IMoreOther, ICommand
        {
            public bool IsReadOnly => false;

            #region Repeat for each concrete interface (IThing).
            IThing IList<IThing>.this[int index] { get => this[index]; set => this[index] = (TImpl)value; }

            void ICollection<IThing>.Add( IThing item ) => Add( (TImpl)item );

            bool ICollection<IThing>.Contains( IThing item ) => Contains( (TImpl)item );

            void ICollection<IThing>.CopyTo( IThing[] array, int arrayIndex ) => CopyTo( (TImpl[])array, arrayIndex );

            int IList<IThing>.IndexOf( IThing item ) => IndexOf( (TImpl)item );

            void IList<IThing>.Insert( int index, IThing item ) => Insert( index, (TImpl)item );

            bool ICollection<IThing>.Remove( IThing item ) => Remove( (TImpl)item );

            IEnumerator<IThing> IEnumerable<IThing>.GetEnumerator() => GetEnumerator();
            #endregion

            #region Repeat for each concrete interface (IOther).
            IOther IList<IOther>.this[int index] { get => this[index]; set => this[index] = (TImpl)value; }

            void ICollection<IOther>.Add( IOther item ) => Add( (TImpl)item );

            bool ICollection<IOther>.Contains( IOther item ) => Contains( (TImpl)item );

            void ICollection<IOther>.CopyTo( IOther[] array, int arrayIndex ) => CopyTo( (TImpl[])array, arrayIndex );

            int IList<IOther>.IndexOf( IOther item ) => IndexOf( (TImpl)item );

            void IList<IOther>.Insert( int index, IOther item ) => Insert( index, (TImpl)item );

            bool ICollection<IOther>.Remove( IOther item ) => Remove( (TImpl)item );

            IEnumerator<IOther> IEnumerable<IOther>.GetEnumerator() => GetEnumerator();
            #endregion

            #region Repeat for each concrete interface (IMoreOther).
            IMoreOther IList<IMoreOther>.this[int index] { get => this[index]; set => this[index] = (TImpl)value; }

            void ICollection<IMoreOther>.Add( IMoreOther item ) => Add( (TImpl)item );

            bool ICollection<IMoreOther>.Contains( IMoreOther item ) => Contains( (TImpl)item );

            void ICollection<IMoreOther>.CopyTo( IMoreOther[] array, int arrayIndex ) => CopyTo( (TImpl[])array, arrayIndex );

            int IList<IMoreOther>.IndexOf( IMoreOther item ) => IndexOf( (TImpl)item );

            void IList<IMoreOther>.Insert( int index, IMoreOther item ) => Insert( index, (TImpl)item );

            bool ICollection<IMoreOther>.Remove( IMoreOther item ) => Remove( (TImpl)item );

            IEnumerator<IMoreOther> IEnumerable<IMoreOther>.GetEnumerator() => GetEnumerator();
            #endregion
        }

        [Test]
        public void readonly_list_interfaces_for_abstractions_are_not_required_on_list()
        {
            var list = new CovPocoList_CK<Thing_CK>();
            list.Add( new Thing_CK() { Power = 42 } );
            list.Add( new Thing_CK() { OtherPower = 3712 } );
            IReadOnlyList<IThing> roThing = list;
            IReadOnlyList<ICommand> roCmd = list;
            IReadOnlyList<object> roObject = list;
            roThing.Where( t => t.Power == 0 ).Should().HaveCount( 1 );
            roThing.First( x => x.Power == 42 ).Should().BeSameAs( list[0] );
            roCmd.Where( x => x is IThing ).Should().HaveCount( 2 );

            IList<IMoreOther> moreOthers = list;
            moreOthers.Add( new Thing_CK { OtherPower = 5 } );
            roThing.Where( t => t.Power == 0 ).Should().HaveCount( 2 );
        }

        public sealed class CovPocoHashSet_CK<TImpl> : HashSet<TImpl>,
                                                       IReadOnlySet<object>,
                                                       ISet<IThing>, IReadOnlySet<IThing>,
                                                       ISet<IOther>, IReadOnlySet<IOther>,
                                                       ISet<IMoreOther>, IReadOnlySet<IMoreOther>,
                                                       IReadOnlySet<ICommand>
            where TImpl : class, IThing, IOther, IMoreOther, ICommand
        {
            public bool IsReadOnly => false;

            #region Repeat for each concrete interface (IThing).

            IEnumerator<IThing> IEnumerable<IThing>.GetEnumerator() => GetEnumerator();

            void ICollection<IThing>.Add( IThing item ) => Add( (TImpl)item );

            void ICollection<IThing>.CopyTo( IThing[] array, int arrayIndex ) => CopyTo( (TImpl[])array, arrayIndex );

            bool ICollection<IThing>.Remove( IThing item ) => Remove( (TImpl)item );

            void ISet<IThing>.ExceptWith( IEnumerable<IThing> other ) => ExceptWith( (IEnumerable<TImpl>)other );

            bool ISet<IThing>.Add( IThing item ) => Add( (TImpl)item );

            void ISet<IThing>.IntersectWith( IEnumerable<IThing> other ) => IntersectWith( (IEnumerable<TImpl>)other );

            void ISet<IThing>.SymmetricExceptWith( IEnumerable<IThing> other ) => SymmetricExceptWith( (IEnumerable<TImpl>)other );

            void ISet<IThing>.UnionWith( IEnumerable<IThing> other ) => UnionWith( (IEnumerable<TImpl>)other );

            public bool Contains( IThing item ) => base.Contains( (TImpl)item );

            public bool IsProperSubsetOf( IEnumerable<IThing> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsProperSupersetOf( IEnumerable<IThing> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsSubsetOf( IEnumerable<IThing> other ) => base.IsSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsSupersetOf( IEnumerable<IThing> other ) => base.IsSupersetOf( (IEnumerable<TImpl>)other );

            public bool Overlaps( IEnumerable<IThing> other ) => base.Overlaps( (IEnumerable<TImpl>)other );

            public bool SetEquals( IEnumerable<IThing> other ) => base.SetEquals( (IEnumerable<TImpl>)other );
            #endregion

            #region Repeat for each concrete interface (IOther).
            IEnumerator<IOther> IEnumerable<IOther>.GetEnumerator() => GetEnumerator();

            void ICollection<IOther>.Add( IOther item ) => Add( (TImpl)item );

            void ICollection<IOther>.CopyTo( IOther[] array, int arrayIndex ) => CopyTo( (TImpl[])array, arrayIndex );

            bool ICollection<IOther>.Remove( IOther item ) => Remove( (TImpl)item );

            void ISet<IOther>.ExceptWith( IEnumerable<IOther> other ) => ExceptWith( (IEnumerable<TImpl>)other );

            bool ISet<IOther>.Add( IOther item ) => Add( (TImpl)item );

            void ISet<IOther>.IntersectWith( IEnumerable<IOther> other ) => IntersectWith( (IEnumerable<TImpl>)other );

            void ISet<IOther>.SymmetricExceptWith( IEnumerable<IOther> other ) => SymmetricExceptWith( (IEnumerable<TImpl>)other );

            void ISet<IOther>.UnionWith( IEnumerable<IOther> other ) => UnionWith( (IEnumerable<TImpl>)other );

            public bool Contains( IOther item ) => base.Contains( (TImpl)item );

            public bool IsProperSubsetOf( IEnumerable<IOther> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsProperSupersetOf( IEnumerable<IOther> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsSubsetOf( IEnumerable<IOther> other ) => base.IsSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsSupersetOf( IEnumerable<IOther> other ) => base.IsSupersetOf( (IEnumerable<TImpl>)other );

            public bool Overlaps( IEnumerable<IOther> other ) => base.Overlaps( (IEnumerable<TImpl>)other );

            public bool SetEquals( IEnumerable<IOther> other ) => base.SetEquals( (IEnumerable<TImpl>)other );
            #endregion

            #region Repeat for each concrete interface (IMoreOther).
            IEnumerator<IMoreOther> IEnumerable<IMoreOther>.GetEnumerator() => GetEnumerator();

            void ICollection<IMoreOther>.Add( IMoreOther item ) => Add( (TImpl)item );

            void ICollection<IMoreOther>.CopyTo( IMoreOther[] array, int arrayIndex ) => CopyTo( (TImpl[])array, arrayIndex );

            bool ICollection<IMoreOther>.Remove( IMoreOther item ) => Remove( (TImpl)item );

            void ISet<IMoreOther>.ExceptWith( IEnumerable<IMoreOther> other ) => ExceptWith( (IEnumerable<TImpl>)other );

            bool ISet<IMoreOther>.Add( IMoreOther item ) => Add( (TImpl)item );

            void ISet<IMoreOther>.IntersectWith( IEnumerable<IMoreOther> other ) => IntersectWith( (IEnumerable<TImpl>)other );

            void ISet<IMoreOther>.SymmetricExceptWith( IEnumerable<IMoreOther> other ) => SymmetricExceptWith( (IEnumerable<TImpl>)other );

            void ISet<IMoreOther>.UnionWith( IEnumerable<IMoreOther> other ) => UnionWith( (IEnumerable<TImpl>)other );

            public bool Contains( IMoreOther item ) => base.Contains( (TImpl)item );

            public bool IsProperSubsetOf( IEnumerable<IMoreOther> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsProperSupersetOf( IEnumerable<IMoreOther> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsSubsetOf( IEnumerable<IMoreOther> other ) => base.IsSubsetOf( (IEnumerable<TImpl>)other );

            public bool IsSupersetOf( IEnumerable<IMoreOther> other ) => base.IsSupersetOf( (IEnumerable<TImpl>)other );

            public bool Overlaps( IEnumerable<IMoreOther> other ) => base.Overlaps( (IEnumerable<TImpl>)other );

            public bool SetEquals( IEnumerable<IMoreOther> other ) => base.SetEquals( (IEnumerable<TImpl>)other );

            #endregion

            bool IReadOnlySet<object>.Contains( object item ) => item is TImpl v && Contains( v );

            bool IReadOnlySet<object>.IsProperSubsetOf( IEnumerable<object> other ) => CovariantHelpers.IsProperSubsetOf( this, other );

            bool IReadOnlySet<object>.IsProperSupersetOf( IEnumerable<object> other ) => CovariantHelpers.IsProperSupersetOf( this, other );

            bool IReadOnlySet<object>.IsSubsetOf( IEnumerable<object> other ) => CovariantHelpers.IsSubsetOf( this, other );

            bool IReadOnlySet<object>.IsSupersetOf( IEnumerable<object> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<object>.Overlaps( IEnumerable<object> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<object>.SetEquals( IEnumerable<object> other ) => CovariantHelpers.SetEquals( this, other );

            IEnumerator<object> IEnumerable<object>.GetEnumerator() => GetEnumerator();

            #region Repeat for each abstract interface (ICommand)
            bool IReadOnlySet<ICommand>.Contains( ICommand item ) => item is TImpl v && Contains( v );

            bool IReadOnlySet<ICommand>.IsProperSubsetOf( IEnumerable<ICommand> other ) => CovariantHelpers.IsProperSubsetOf( this, other );

            bool IReadOnlySet<ICommand>.IsProperSupersetOf( IEnumerable<ICommand> other ) => CovariantHelpers.IsProperSupersetOf( this, other );

            bool IReadOnlySet<ICommand>.IsSubsetOf( IEnumerable<ICommand> other ) => CovariantHelpers.IsSubsetOf( this, other );

            bool IReadOnlySet<ICommand>.IsSupersetOf( IEnumerable<ICommand> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<ICommand>.Overlaps( IEnumerable<ICommand> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<ICommand>.SetEquals( IEnumerable<ICommand> other ) => CovariantHelpers.SetEquals( this, other );

            IEnumerator<ICommand> IEnumerable<ICommand>.GetEnumerator() => GetEnumerator();
            #endregion

        }

        [Test]
        public void CovPocoHashSet_is_IReadOnlySet_object()
        {
            var t1 = new Thing_CK() { Power = 1 };
            var t2 = new Thing_CK() { Power = 2 };
            var t3 = new Thing_CK() { Power = 3 };
            var set = new CovPocoHashSet_CK<Thing_CK>() { t1, t2, t3 };
            IReadOnlySet<object> nSet = set;

            var empty = new object[] { };
            var set2 = new object[] { t1, t2, t3 };
            var superSet2 = new object[] { t1, t2, t3, this };
            var subSet2 = new object[] { t1, t2 };
            var otherSet = new object[] { new Thing_CK() };

            nSet.Contains( this ).Should().BeFalse();

            nSet.SetEquals( set ).Should().BeTrue();
            nSet.SetEquals( empty ).Should().BeFalse();
            nSet.SetEquals( set2 ).Should().BeTrue();
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeTrue();
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
        }


        [Test]
        public void CovPocoHashSet_CANNOT_be_used_for_Nullable()
        {
            var t1 = new Thing_CK() { Power = 1 };
            var t2 = new Thing_CK() { Power = 2 };
            var t3 = new Thing_CK() { Power = 3 };
            // Forgive null warnings here.
            var set = new CovPocoHashSet_CK<Thing_CK>() { t1, null!, t3 };
            IReadOnlySet<object?> nSet = set!;

            var empty = new object?[] { };
            var set2 = new object?[] { t1, null, t3 };
            var superSet2 = new object?[] { t1, null, t3, this };
            var subSet2 = new object?[] { t1, null };
            var otherSet = new object?[] { new Thing_CK() };

            nSet.Contains( null ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );

            nSet.SetEquals( set ).Should().BeTrue();
            nSet.SetEquals( empty ).Should().BeFalse();
            nSet.SetEquals( set2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsSubsetOf( superSet2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
        }

        public sealed class CovPocoNullableHashSet_CK<TImpl> : HashSet<TImpl?>,
                                                               IReadOnlySet<object?>,
                                                               ISet<IThing?>, IReadOnlySet<IThing?>,
                                                               IReadOnlySet<ICommand?>
                where TImpl : class, IThing, ICommand
        {
            public bool IsReadOnly => false;

            #region Repeat for each concrete interface (IThing).

            IEnumerator<IThing> IEnumerable<IThing?>.GetEnumerator() => GetEnumerator();

            void ICollection<IThing?>.Add( IThing? item ) => Add( (TImpl?)item );

            void ICollection<IThing?>.CopyTo( IThing?[] array, int arrayIndex ) => CopyTo( (TImpl?[])array, arrayIndex );

            bool ICollection<IThing?>.Remove( IThing? item ) => Remove( (TImpl?)item );

            void ISet<IThing?>.ExceptWith( IEnumerable<IThing?> other ) => ExceptWith( (IEnumerable<TImpl?>)other );

            bool ISet<IThing?>.Add( IThing? item ) => Add( (TImpl?)item );

            void ISet<IThing?>.IntersectWith( IEnumerable<IThing?> other ) => IntersectWith( (IEnumerable<TImpl?>)other );

            void ISet<IThing?>.SymmetricExceptWith( IEnumerable<IThing?> other ) => SymmetricExceptWith( (IEnumerable<TImpl?>)other );

            void ISet<IThing?>.UnionWith( IEnumerable<IThing?> other ) => UnionWith( (IEnumerable<TImpl?>)other );

            public bool Contains( IThing? item ) => base.Contains( (TImpl?)item );

            public bool IsProperSubsetOf( IEnumerable<IThing?> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl?>)other );

            public bool IsProperSupersetOf( IEnumerable<IThing?> other ) => base.IsProperSubsetOf( (IEnumerable<TImpl?>)other );

            public bool IsSubsetOf( IEnumerable<IThing?> other ) => base.IsSubsetOf( (IEnumerable<TImpl?>)other );

            public bool IsSupersetOf( IEnumerable<IThing?> other ) => base.IsSupersetOf( (IEnumerable<TImpl?>)other );

            public bool Overlaps( IEnumerable<IThing?> other ) => base.Overlaps( (IEnumerable<TImpl?>)other );

            public bool SetEquals( IEnumerable<IThing?> other ) => base.SetEquals( (IEnumerable<TImpl?>)other );
            #endregion


            bool IReadOnlySet<object?>.Contains( object? item ) => (item is TImpl v && Contains( v )) || (item == null && Contains( null ));

            bool IReadOnlySet<object?>.IsProperSubsetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsProperSubsetOf( this, other );

            bool IReadOnlySet<object?>.IsProperSupersetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsProperSupersetOf( this, other );

            bool IReadOnlySet<object?>.IsSubsetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsSubsetOf( this, other );

            bool IReadOnlySet<object?>.IsSupersetOf( IEnumerable<object?> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<object?>.Overlaps( IEnumerable<object?> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<object?>.SetEquals( IEnumerable<object?> other ) => CovariantHelpers.SetEquals( this, other );

            IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => GetEnumerator();

            #region Repeat for each abstract interface (ICommand)
            bool IReadOnlySet<ICommand?>.Contains( ICommand? item ) => (item is TImpl v && Contains( v )) || (item == null && Contains( null ));

            bool IReadOnlySet<ICommand?>.IsProperSubsetOf( IEnumerable<ICommand?> other ) => CovariantHelpers.NullableIsProperSubsetOf( this, other );

            bool IReadOnlySet<ICommand?>.IsProperSupersetOf( IEnumerable<ICommand?> other ) => CovariantHelpers.NullableIsProperSupersetOf( this, other );

            bool IReadOnlySet<ICommand?>.IsSubsetOf( IEnumerable<ICommand?> other ) => CovariantHelpers.NullableIsSubsetOf( this, other );

            bool IReadOnlySet<ICommand?>.IsSupersetOf( IEnumerable<ICommand?> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<ICommand?>.Overlaps( IEnumerable<ICommand?> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<ICommand?>.SetEquals( IEnumerable<ICommand?> other ) => CovariantHelpers.SetEquals( this, other );

            IEnumerator<ICommand?> IEnumerable<ICommand?>.GetEnumerator() => GetEnumerator();
            #endregion

        }

        [Test]
        public void CovPocoNullableHashSet_handles_Nullable_abstractions()
        {
            var t1 = new Thing_CK() { Power = 1 };
            var t2 = new Thing_CK() { Power = 2 };
            var t3 = new Thing_CK() { Power = 3 };
            var set = new CovPocoNullableHashSet_CK<Thing_CK>() { t1, null, t3 };
            IReadOnlySet<object?> nSet = set;

            var empty = new object?[] { };
            var set2 = new object?[] { t1, null, t3 };
            var superSet2 = new object?[] { t1, null, t3, this };
            var subSet2 = new object?[] { t1, null };
            var otherSet = new object?[] { new Thing_CK() };

            nSet.Contains( null ).Should().BeTrue( "Fixed." );

            nSet.SetEquals( set ).Should().BeTrue();
            nSet.SetEquals( empty ).Should().BeFalse();
            nSet.SetEquals( set2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue( "Fixed." );
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeTrue( "Fixed." );
            nSet.IsSubsetOf( superSet2 ).Should().BeTrue( "Fixed." );
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue( "Fixed." );
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeFalse( "BUG! :( The null must be explicitly handled." );
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
        }



        public sealed class CovPocoDictionary_CK<TKey, TImpl> : Dictionary<TKey, TImpl>,
                                                                IReadOnlyDictionary<TKey, object>,
                                                                IDictionary<TKey, IThing>, IReadOnlyDictionary<TKey, IThing>,
                                                                IReadOnlyDictionary<TKey, ICommand>
            where TKey : notnull
            where TImpl : class, IThing, IOther, IMoreOther, ICommand
        {

            public bool IsReadOnly => false;

            object IReadOnlyDictionary<TKey, object>.this[TKey key] => this[key];

            IEnumerable<TKey> IReadOnlyDictionary<TKey, object>.Keys => Keys;

            IEnumerable<object> IReadOnlyDictionary<TKey, object>.Values => Values;

            bool IReadOnlyDictionary<TKey, object>.TryGetValue( TKey key, [MaybeNullWhen(false)]out object value )
            {
                if( base.TryGetValue( key, out var v ) )
                {
                    value = v;
                    return true;
                }
                value = null;
                return false;
            }

            IEnumerator<KeyValuePair<TKey, object>> IEnumerable<KeyValuePair<TKey, object>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TImpl>>)this).Select( kv => KeyValuePair.Create( kv.Key, (object)kv.Value ) ).GetEnumerator();
            }

            #region Repeat for each interface (Concrete or abstract).
            IThing IReadOnlyDictionary<TKey, IThing>.this[TKey key] => this[key];

            IEnumerable<TKey> IReadOnlyDictionary<TKey, IThing>.Keys => Keys;

            IEnumerable<IThing> IReadOnlyDictionary<TKey, IThing>.Values => Values;

            public bool TryGetValue( TKey key, [MaybeNullWhen( false )] out IThing value )
            {
                if( base.TryGetValue( key, out var v ) )
                {
                    value = v;
                    return true;
                }
                value = null;
                return false;
            }

            IEnumerator<KeyValuePair<TKey, IThing>> IEnumerable<KeyValuePair<TKey, IThing>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TImpl>>)this).Select( kv => KeyValuePair.Create( kv.Key, (IThing)kv.Value ) ).GetEnumerator();
            }

            #endregion

            #region Repeat for each Concrete interface.

            ICollection<TKey> IDictionary<TKey, IThing>.Keys => Keys;

            ICollection<IThing> IDictionary<TKey, IThing>.Values => Unsafe.As<ICollection<IThing>>( Values );

            IThing IDictionary<TKey, IThing>.this[TKey key] { get => this[key]; set => this[key] = (TImpl)value; }

            void IDictionary<TKey, IThing>.Add( TKey key, IThing value ) => Add( key, (TImpl)value );

            void ICollection<KeyValuePair<TKey, IThing>>.Add( KeyValuePair<TKey, IThing> item ) => Add( item.Key, (TImpl)item.Value );

            bool ICollection<KeyValuePair<TKey, IThing>>.Contains( KeyValuePair<TKey, IThing> item ) => base.TryGetValue( item.Key, out var v ) && v == item.Value;

            void ICollection<KeyValuePair<TKey, IThing>>.CopyTo( KeyValuePair<TKey, IThing>[] array, int arrayIndex )
            {
                ((ICollection<KeyValuePair<TKey, TImpl>>)this).CopyTo( Unsafe.As<KeyValuePair<TKey, TImpl>[]>( array ), arrayIndex );
            }

            bool ICollection<KeyValuePair<TKey, IThing>>.Remove( KeyValuePair<TKey, IThing> item )
            {
                return ((ICollection<KeyValuePair<TKey, TImpl>>)this).Remove( new KeyValuePair<TKey, TImpl>( item.Key, (TImpl)item.Value ) );
            }

            #endregion

            // ICommand (abstract interface)
            ICommand IReadOnlyDictionary<TKey, ICommand>.this[TKey key] => this[key];

            IEnumerable<TKey> IReadOnlyDictionary<TKey, ICommand>.Keys => Keys;

            IEnumerable<ICommand> IReadOnlyDictionary<TKey, ICommand>.Values => Values;

            public bool TryGetValue( TKey key, [MaybeNullWhen( false )] out ICommand value )
            {
                if( base.TryGetValue( key, out var v ) )
                {
                    value = v;
                    return true;
                }
                value = null;
                return false;
            }

            IEnumerator<KeyValuePair<TKey, ICommand>> IEnumerable<KeyValuePair<TKey, ICommand>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TImpl>>)this).Select( kv => KeyValuePair.Create( kv.Key, (ICommand)kv.Value ) ).GetEnumerator();
            }


        }

        [Test]
        public void Dictionary_support()
        {
            var d = new CovPocoDictionary_CK<int,Thing_CK>();
            IDictionary<int,IThing> dThing = d;
            IReadOnlyDictionary<int,ICommand> dCmd = d;
            for( int i = 0; i < 10; ++i )
            {
                dThing.Add( i, new Thing_CK() { Power = i } );
            }
            dCmd.Count.Should().Be( 10 );
            dThing[0].Power.Should().Be( 0 );
            // This uses an Unsafe.As<ICollection<IThing>>...
            dThing.Values.Should().OnlyContain( v => v.Power >= 0 && v.Power < 10 );
            // This uses an Unsafe.As<KeyValuePair<TKey, TImpl>[]> to adapt the target...
            var target = new KeyValuePair<int,IThing>[20];
            dThing.CopyTo( target, 0 );
            dThing.CopyTo( target, 10 );
            target.Should().OnlyContain( x => x.Value != null && dThing.Contains( x ) );
        }

        public sealed class CovNotNullValueList<T> : List<T>, IReadOnlyList<T?>, IReadOnlyList<object> where T : struct
        {
            public CovNotNullValueList() { }

            public CovNotNullValueList( IEnumerable<T> collection ) : base( collection ) { }

            public CovNotNullValueList( int capacity ) : base( capacity ) { }

            object IReadOnlyList<object>.this[int index] => this[index];

            T? IReadOnlyList<T?>.this[int index] => this[index];

            IEnumerator<object> IEnumerable<object>.GetEnumerator() => this.Cast<object>().GetEnumerator();

            IEnumerator<T?> IEnumerable<T?>.GetEnumerator() => this.Cast<T?>().GetEnumerator();
        }

        public sealed class CovNullableValueList<T> : List<T?>, IReadOnlyList<object?> where T : struct
        {
            public CovNullableValueList() { }

            public CovNullableValueList( IEnumerable<T?> collection ) : base( collection ) { }

            public CovNullableValueList( int capacity ) : base( capacity ) { }

            object? IReadOnlyList<object?>.this[int index] => this[index];

            IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => this.Cast<object?>().GetEnumerator();
        }

        public sealed class CovNotNullValueHashSet<T> : HashSet<T>, IReadOnlySet<T?>, IReadOnlySet<object> where T : struct
        {
            public CovNotNullValueHashSet() { }

            public CovNotNullValueHashSet( IEnumerable<T> collection ) : base( collection ) { }

            public CovNotNullValueHashSet( IEqualityComparer<T>? comparer ) : base( comparer ) { }

            public CovNotNullValueHashSet( int capacity ) : base( capacity ) { }

            public CovNotNullValueHashSet( IEnumerable<T> collection, IEqualityComparer<T>? comparer ) : base( collection, comparer ) { }

            public CovNotNullValueHashSet( int capacity, IEqualityComparer<T>? comparer ) : base( capacity, comparer ) { }

            bool IReadOnlySet<object>.Contains( object item ) => item is T v && Contains( v );

            bool IReadOnlySet<object>.IsProperSubsetOf( IEnumerable<object> other ) => CovariantHelpers.IsProperSubsetOf( this, other );

            bool IReadOnlySet<object>.IsSubsetOf( IEnumerable<object> other ) => CovariantHelpers.IsSubsetOf( this, other );

            bool IReadOnlySet<object>.IsProperSupersetOf( IEnumerable<object> other ) => CovariantHelpers.IsProperSupersetOf( this, other );

            bool IReadOnlySet<object>.IsSupersetOf( IEnumerable<object> other ) => CovariantHelpers.IsSupersetOf( this, other );

            bool IReadOnlySet<object>.Overlaps( IEnumerable<object> other ) => CovariantHelpers.Overlaps( this, other );

            bool IReadOnlySet<object>.SetEquals( IEnumerable<object> other ) => CovariantHelpers.SetEquals( this, other );

            IEnumerator<object> IEnumerable<object>.GetEnumerator() => this.Cast<object>().GetEnumerator();

            #region Nullable item support.

            bool IReadOnlySet<T?>.Contains( T? item ) => item.HasValue && base.Contains( item.Value );

            bool IReadOnlySet<T?>.IsProperSubsetOf( IEnumerable<T?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( other == this ) return false;
                int otherCount = 0;
                // We use a HashSet here (we could have used a List instead). There is
                // no absolute better choice, this depends on the content.
                HashSet<T>? typed = null;
                foreach( var e in other )
                {
                    // The empty set is a proper subset of anything but the empty set.
                    if( Count == 0 ) return true;
                    ++otherCount;
                    if( !e.HasValue ) continue;
                    typed ??= new HashSet<T>( Comparer );
                    typed.Add( e.Value );
                }
                // No set is a proper subset of an empty set.
                if( otherCount == 0 ) return false;
                Debug.Assert( Count > 0 );
                if( typed == null ) return false;
                Debug.Assert( typed.Count <= otherCount );
                return otherCount > typed.Count
                        ? IsSubsetOf( typed )
                        : IsProperSubsetOf( typed );
            }

            bool IReadOnlySet<T?>.IsProperSupersetOf( IEnumerable<T?> other )
            {
                Throw.CheckNotNullArgument( other );
                // The empty set isn't a proper superset of any set, and a set is never a strict superset of itself.
                if( Count == 0 || other == this ) return false;
                HashSet<T>? unique = null;
                foreach( var e in other )
                {
                    if( !e.HasValue || !Contains( e.Value ) ) return false;
                    unique ??= new HashSet<T>();
                    unique.Add( e.Value );
                }
                return unique == null || unique.Count < Count;
            }

            bool IReadOnlySet<T?>.IsSubsetOf( IEnumerable<T?> other )
            {
                Throw.CheckNotNullArgument( other );
                // The empty set is a subset of any set, and a set is a subset of itself.
                if( other == this || Count == 0 ) return true;
                HashSet<T>? typed = null;
                foreach( var e in other )
                {
                    if( !e.HasValue ) continue;
                    typed ??= new HashSet<T>( Comparer );
                    typed.Add( e.Value );
                }
                return typed != null && typed.Count >= Count && IsSubsetOf( typed );
            }

            bool IReadOnlySet<T?>.IsSupersetOf( IEnumerable<T?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( other == this ) return true;
                foreach( var e in other )
                {
                    if( !e.HasValue || !Contains( e.Value ) ) return false;
                }
                return true;
            }

            bool IReadOnlySet<T?>.Overlaps( IEnumerable<T?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( Count == 0 ) return false;
                if( other == this ) return true;
                foreach( var e in other )
                {
                    if( e.HasValue && Contains( e.Value ) ) return true;
                }
                return false;
            }

            bool IReadOnlySet<T?>.SetEquals( IEnumerable<T?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( other == this ) return true;
                int count = 0;
                foreach( var e in other )
                {
                    if( !e.HasValue || !Contains( e.Value ) ) return false;
                    ++count;
                }
                return count == Count;
            }

            sealed class NullEnumerator : IEnumerator<T?>
            {
                readonly IEnumerator<T> _inner;

                public NullEnumerator( IEnumerator<T> inner )
                {
                    _inner = inner;
                }

                public T? Current => _inner.Current;

                object IEnumerator.Current => _inner.Current;

                public void Dispose() => _inner.Dispose();

                public bool MoveNext() => _inner.MoveNext();

                public void Reset() => _inner.Reset();
            }

            IEnumerator<T?> IEnumerable<T?>.GetEnumerator() => new NullEnumerator( GetEnumerator() );

            #endregion
        }

        [Test]
        public void CovNotNullValueHashSet_T_is_IReadOnlySet_object()
        {
            var set = new CovNotNullValueHashSet<int>() { 1, 2, 3 };
            IReadOnlySet<object> nSet = set;

            var empty = new object[] {};
            var set2 = new object[] { 1, 2, 3 };
            var superSet2 = new object[] { 1, 2, 3, this };
            var subSet2 = new object[] { 1, 2 };
            var otherSet = new object[] { 0 };

            nSet.Contains( this ).Should().BeFalse();

            nSet.SetEquals( set ).Should().BeTrue();
            nSet.SetEquals( empty ).Should().BeFalse();
            nSet.SetEquals( set2 ).Should().BeTrue();
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeTrue();
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
        }

        [Test]
        public void CovNotNullValueHashSet_T_is_IReadOnlySet_T_Nullable()
        {
            var set = new CovNotNullValueHashSet<int>() { 1, 2, 3 };
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
            nSet.SetEquals( superSet2 ).Should().BeFalse();
            nSet.SetEquals( subSet2 ).Should().BeFalse();
            nSet.SetEquals( otherSet ).Should().BeFalse();

            nSet.IsProperSubsetOf( set ).Should().BeFalse();
            nSet.IsProperSubsetOf( empty ).Should().BeFalse();
            nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsSubsetOf( set ).Should().BeTrue();
            nSet.IsSubsetOf( empty ).Should().BeFalse();
            nSet.IsSubsetOf( set2 ).Should().BeTrue();
            nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
            nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
            nSet.IsSubsetOf( otherSet ).Should().BeFalse();

            nSet.IsProperSupersetOf( set ).Should().BeFalse();
            nSet.IsProperSupersetOf( empty ).Should().BeTrue();
            nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

            nSet.IsSupersetOf( set ).Should().BeTrue();
            nSet.IsSupersetOf( empty ).Should().BeTrue();
            nSet.IsSupersetOf( set2 ).Should().BeTrue();
            nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
            nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
            nSet.IsSupersetOf( otherSet ).Should().BeFalse();

            nSet.Overlaps( set ).Should().BeTrue();
            nSet.Overlaps( empty ).Should().BeFalse();
            nSet.Overlaps( set2 ).Should().BeTrue();
            nSet.Overlaps( superSet2 ).Should().BeTrue();
            nSet.Overlaps( subSet2 ).Should().BeTrue();
            nSet.Overlaps( otherSet ).Should().BeFalse();
        }

        public sealed class CovNullableValueHashSet<T> : HashSet<T?>, IReadOnlySet<object?> where T : struct
        {
            public CovNullableValueHashSet() { }

            public CovNullableValueHashSet( IEnumerable<T?> collection ) : base( collection ) { }

            public CovNullableValueHashSet( IEqualityComparer<T?>? comparer ) : base( comparer ) { }

            public CovNullableValueHashSet( int capacity ) : base( capacity ) { }

            public CovNullableValueHashSet( IEnumerable<T?> collection, IEqualityComparer<T?>? comparer ) : base( collection, comparer ) { }

            public CovNullableValueHashSet( int capacity, IEqualityComparer<T?>? comparer ) : base( capacity, comparer ) { }

            bool IReadOnlySet<object?>.Contains( object? item ) => (item is T e && Contains( e )) || (item == null && Contains( default ));

            bool IReadOnlySet<object?>.IsProperSubsetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsProperSubsetOf( this, other );

            bool IReadOnlySet<object?>.IsProperSupersetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsProperSupersetOf( this, other );

            bool IReadOnlySet<object?>.IsSubsetOf( IEnumerable<object?> other ) => CovariantHelpers.NullableIsSubsetOf( this, other );


            bool IReadOnlySet<object?>.IsSupersetOf( IEnumerable<object?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( other == this ) return true;
                if( other is IEnumerable<T?> same ) return IsSupersetOf( same );
                foreach( var e in other )
                {
                    bool found = (e is T item && Contains( item )) || (e == null && Contains( default ));
                    if( !found ) return false;
                }
                return true;
            }

            bool IReadOnlySet<object?>.Overlaps( IEnumerable<object?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( Count == 0 ) return false;
                if( other == this ) return true;
                if( other is IEnumerable<T?> same ) return Overlaps( same );
                foreach( var e in other )
                {
                    if( (e is T item && Contains( item )) || (e == null && Contains( default )) ) return true;
                }
                return false;
            }

            bool IReadOnlySet<object?>.SetEquals( IEnumerable<object?> other )
            {
                Throw.CheckNotNullArgument( other );
                if( other == this ) return true;
                if( other is IEnumerable<T?> same ) return SetEquals( same );
                int count = 0;
                foreach( var e in other )
                {
                    bool found = (e is T item && Contains( item )) || (e == null && Contains( default ));
                    if( !found ) return false;
                    ++count;
                }
                return count == Count;
            }

            IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => this.Cast<object?>().GetEnumerator();
        }

        [Test]
        public void CovNullableValueHashSet_T_is_IReadOnlySet_object_Nullable()
        {
            {
                var set = new CovNullableValueHashSet<int>() { 1, 2, 3 };
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
                nSet.SetEquals( superSet2 ).Should().BeFalse();
                nSet.SetEquals( subSet2 ).Should().BeFalse();
                nSet.SetEquals( otherSet ).Should().BeFalse();

                nSet.IsProperSubsetOf( set ).Should().BeFalse();
                nSet.IsProperSubsetOf( empty ).Should().BeFalse();
                nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

                nSet.IsSubsetOf( set ).Should().BeTrue();
                nSet.IsSubsetOf( empty ).Should().BeFalse();
                nSet.IsSubsetOf( set2 ).Should().BeTrue();
                nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsSubsetOf( otherSet ).Should().BeFalse();

                nSet.IsProperSupersetOf( set ).Should().BeFalse();
                nSet.IsProperSupersetOf( empty ).Should().BeTrue();
                nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

                nSet.IsSupersetOf( set ).Should().BeTrue();
                nSet.IsSupersetOf( empty ).Should().BeTrue();
                nSet.IsSupersetOf( set2 ).Should().BeTrue();
                nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsSupersetOf( otherSet ).Should().BeFalse();

                nSet.Overlaps( set ).Should().BeTrue();
                nSet.Overlaps( empty ).Should().BeFalse();
                nSet.Overlaps( set2 ).Should().BeTrue();
                nSet.Overlaps( superSet2 ).Should().BeTrue();
                nSet.Overlaps( subSet2 ).Should().BeTrue();
                nSet.Overlaps( otherSet ).Should().BeFalse();
            }
            {
                var set = new CovNullableValueHashSet<int>() { 1, null, 3 };
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
                nSet.SetEquals( superSet2 ).Should().BeFalse();
                nSet.SetEquals( subSet2 ).Should().BeFalse();
                nSet.SetEquals( otherSet ).Should().BeFalse();

                nSet.IsProperSubsetOf( set ).Should().BeFalse();
                nSet.IsProperSubsetOf( empty ).Should().BeFalse();
                nSet.IsProperSubsetOf( set2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsProperSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsProperSubsetOf( otherSet ).Should().BeFalse();

                nSet.IsSubsetOf( set ).Should().BeTrue();
                nSet.IsSubsetOf( empty ).Should().BeFalse();
                nSet.IsSubsetOf( set2 ).Should().BeTrue();
                nSet.IsSubsetOf( superSet2 ).Should().BeTrue();
                nSet.IsSubsetOf( subSet2 ).Should().BeFalse();
                nSet.IsSubsetOf( otherSet ).Should().BeFalse();

                nSet.IsProperSupersetOf( set ).Should().BeFalse();
                nSet.IsProperSupersetOf( empty ).Should().BeTrue();
                nSet.IsProperSupersetOf( set2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsProperSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsProperSupersetOf( otherSet ).Should().BeFalse();

                nSet.IsSupersetOf( set ).Should().BeTrue();
                nSet.IsSupersetOf( empty ).Should().BeTrue();
                nSet.IsSupersetOf( set2 ).Should().BeTrue();
                nSet.IsSupersetOf( superSet2 ).Should().BeFalse();
                nSet.IsSupersetOf( subSet2 ).Should().BeTrue();
                nSet.IsSupersetOf( otherSet ).Should().BeFalse();

                nSet.Overlaps( set ).Should().BeTrue();
                nSet.Overlaps( empty ).Should().BeFalse();
                nSet.Overlaps( set2 ).Should().BeTrue();
                nSet.Overlaps( superSet2 ).Should().BeTrue();
                nSet.Overlaps( subSet2 ).Should().BeTrue();
                nSet.Overlaps( otherSet ).Should().BeFalse();
            }
        }

        public sealed class CovNotNullValueDictionary<TKey, TValue> : Dictionary<TKey, TValue>,
                                                                      IReadOnlyDictionary<TKey, object>,
                                                                      IReadOnlyDictionary<TKey, TValue?>
            where TKey : notnull
            where TValue : struct
        {
            object IReadOnlyDictionary<TKey, object>.this[TKey key] => this[key];

            IEnumerable<object> IReadOnlyDictionary<TKey, object>.Values => Values.Cast<object>();

            IEnumerable<TKey> IReadOnlyDictionary<TKey, object>.Keys => Keys;

            bool IReadOnlyDictionary<TKey, object>.TryGetValue( TKey key, out object value )
            {
                if( base.TryGetValue( key, out var v ) )
                {
                    value = v;
                    return true;
                }
                value = null!;
                return false;
            }

            IEnumerator<KeyValuePair<TKey, object>> IEnumerable<KeyValuePair<TKey, object>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TValue>>)this).Select( kv => KeyValuePair.Create( kv.Key, (object)kv.Value ) ).GetEnumerator();
            }

            TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

            IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

            IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values.Cast<TValue?>();

            public bool TryGetValue( TKey key, [MaybeNullWhen( false )] out TValue? value ) => TryGetValue( key, out value );

            IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TValue>>)this).Select( kv => KeyValuePair.Create( kv.Key, (TValue?)kv.Value ) ).GetEnumerator();
            }
        }

        [Test]
        public void CovNotNullValueDictionary_TValue_is_IReadOnlyDictionary_T_Nullable()
        {
            var d = new CovNotNullValueDictionary<int, byte>() { { 0, 1 } };
            IReadOnlyDictionary<int, byte?> dN = d;
            dN[0].Should().Be( 1 );
            dN.Values.Should().ContainSingle().And.OnlyContain( b => b == 1 );
            dN.Contains( new KeyValuePair<int, byte?>( 0, null ) ).Should().BeFalse();
            dN.Contains( new KeyValuePair<int, byte?>( 0, 1 ) ).Should().BeTrue();
        }

        [Test]
        public void CovNotNullValueDictionary_TValue_is_IReadOnlyDictionary_object()
        {
            var d = new CovNotNullValueDictionary<int, byte>() { { 0, 1 } };
            IReadOnlyDictionary<int, object> dN = d;
            dN[0].Should().Be( 1 );
            dN.Values.Should().ContainSingle().And.OnlyContain( b => b.Equals( (byte)1 ) );
            dN.Contains( new KeyValuePair<int, object>( 0, this ) ).Should().BeFalse();
            dN.Contains( new KeyValuePair<int, object>( 0, (byte)1 ) ).Should().BeTrue();
        }

        public sealed class CovNullableValueDictionary<TKey, TValue> : Dictionary<TKey, TValue?>,
                                                                       IReadOnlyDictionary<TKey, object?>
            where TKey : notnull
            where TValue : struct
        {
            object? IReadOnlyDictionary<TKey, object?>.this[TKey key] => this[key];

            IEnumerable<object?> IReadOnlyDictionary<TKey, object?>.Values => Values.Cast<object>();

            IEnumerable<TKey> IReadOnlyDictionary<TKey, object?>.Keys => Keys;

            bool IReadOnlyDictionary<TKey, object?>.TryGetValue( TKey key, out object? value )
            {
                if( TryGetValue( key, out var v ) )
                {
                    value = v;
                    return true;
                }
                value = null;
                return false;
            }

            IEnumerator<KeyValuePair<TKey, object?>> IEnumerable<KeyValuePair<TKey, object?>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<TKey, TValue?>>)this).Select( kv => KeyValuePair.Create( kv.Key, (object?)kv.Value ) ).GetEnumerator();
            }
        }

        [Test]
        public void CovNullableValueDictionary_TValue_is_IReadOnlyDictionary_object_nullable()
        {
            var d = new CovNullableValueDictionary<int, byte>() { { 0, 1 }, { 1, null } };
            IReadOnlyDictionary<int, object?> dN = d;
            dN[0].Should().Be( 1 );
            dN.Values.Should().HaveCount( 2 ).And.OnlyContain( b => b == null || b.Equals( (byte)1 ) );
            dN.Contains( new KeyValuePair<int, object?>( 0, this ) ).Should().BeFalse();
            dN.Contains( new KeyValuePair<int, object?>( 0, (byte)1 ) ).Should().BeTrue();
            dN.Contains( new KeyValuePair<int, object?>( 1, null ) ).Should().BeTrue();
        }


    }
}
