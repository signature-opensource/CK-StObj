using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    public static class CovariantHelpers
    {
        public static bool IsProperSubsetOf<T>( HashSet<T> set, IEnumerable<object> other ) where T : notnull
        {
            Throw.CheckNotNullArgument( other );
            // No set is a proper subset of itself.
            if( other == set ) return false;
            if( other is IEnumerable<T> same ) return set.IsProperSubsetOf( same );
            int otherCount = 0;
            // We use a HashSet here (we could have used a List instead). There is
            // no absolute better choice, this depends on the content.
            HashSet<T>? typed = null;
            foreach( var e in other )
            {
                // The empty set is a proper subset of anything but the empty set.
                if( set.Count == 0 ) return true;
                ++otherCount;
                if( e is not T item ) continue;
                typed ??= new HashSet<T>( set.Comparer );
                typed.Add( item );
            }
            // No set is a proper subset of an empty set.
            if( otherCount == 0 ) return false;
            Debug.Assert( set.Count > 0 );
            if( typed == null ) return false;
            Debug.Assert( typed.Count <= otherCount );
            return otherCount > typed.Count
                    ? set.IsSubsetOf( typed )
                    : set.IsProperSubsetOf( typed );
        }

        public static bool IsSubsetOf<T>( HashSet<T> set, IEnumerable<object> other ) where T : notnull 
        {
            Throw.CheckNotNullArgument( other );
            // The empty set is a subset of any set, and a set is a subset of itself.
            if( other == set || set.Count == 0 ) return true;
            if( other is IEnumerable<T> same ) return set.IsSubsetOf( same );
            HashSet<T>? typed = null;
            foreach( var e in other )
            {
                if( e is not T item ) continue;
                typed ??= new HashSet<T>( set.Comparer );
                typed.Add( item );
            }
            return typed != null && typed.Count >= set.Count && set.IsSubsetOf( typed );
        }

        public static bool IsProperSupersetOf<T>( HashSet<T> set, IEnumerable<object> other ) where T : notnull
        {
            Throw.CheckNotNullArgument( other );
            // The empty set isn't a proper superset of any set, and a set is never a strict superset of itself.
            if( set.Count == 0 || other == set ) return false;
            if( other is IEnumerable<T> same ) return set.IsProperSupersetOf( same );
            HashSet<T>? unique = null;
            foreach( var e in other )
            {
                if( e is not T item || !set.Contains( item ) ) return false;
                unique ??= new HashSet<T>();
                unique.Add( item );
            }
            return unique == null || unique.Count < set.Count;
        }

        public static bool IsSupersetOf<T>( HashSet<T> set, IEnumerable<object> other ) where T : notnull
        {
            Throw.CheckNotNullArgument( other );
            // A set is always a superset of itself.
            if( other == set ) return true;
            if( other is IEnumerable<T> same ) return set.IsSupersetOf( same );
            foreach( var e in other )
            {
                if( e is not T item || !set.Contains( item ) ) return false;
            }
            return true;
        }

        public static bool Overlaps<T>( HashSet<T> set, IEnumerable<object> other ) where T : notnull
        {
            Throw.CheckNotNullArgument( other );
            if( set.Count == 0 ) return false;
            if( other == set ) return true;
            if( other is IEnumerable<T> same ) return set.Overlaps( same );
            foreach( var e in other )
            {
                if( e is T item && set.Contains( item ) ) return true;
            }
            return false;
        }

        public static bool SetEquals<T>( HashSet<T> set, IEnumerable<object> other ) where T : notnull
        {
            Throw.CheckNotNullArgument( other );
            if( other == set ) return true;
            if( other is IEnumerable<T> same ) return set.SetEquals( same );
            int count = 0;
            foreach( var e in other )
            {
                if( e is not T item || !set.Contains( item ) ) return false;
                ++count;
            }
            return count == set.Count;
        }

        public static bool NullableIsProperSubsetOf<T,TA>( HashSet<T?> set, IEnumerable<TA?> other )
        {
            Throw.CheckNotNullArgument( other );
            if( other == set ) return false;
            if( other is IEnumerable<T?> same ) return set.IsProperSubsetOf( same );
            int otherCount = 0;
            HashSet<T?>? typed = null;
            foreach( var e in other )
            {
                if( set.Count == 0 ) return true;
                ++otherCount;
                switch( e )
                {
                    case T item:
                        typed ??= new HashSet<T?>( set.Comparer );
                        typed.Add( item );
                        break;
                    case null:
                        typed ??= new HashSet<T?>( set.Comparer );
                        typed.Add( default );
                        break;
                }
            }
            if( otherCount == 0 ) return false;
            Debug.Assert( set.Count > 0 );
            if( typed == null ) return false;
            Debug.Assert( typed.Count <= otherCount );
            return otherCount > typed.Count
                    ? set.IsSubsetOf( typed )
                    : set.IsProperSubsetOf( typed );
        }

        public static bool NullableIsProperSupersetOf<T,TA>( HashSet<T?> set, IEnumerable<TA?> other )
        {
            Throw.CheckNotNullArgument( other );
            if( set.Count == 0 || other == set ) return false;
            if( other is IEnumerable<T?> same ) return set.IsProperSupersetOf( same );
            HashSet<T?>? unique = null;
            foreach( var e in other )
            {
                if( e == null )
                {
                    if( !set.Contains( default ) ) return false;
                    unique ??= new HashSet<T?>();
                    unique.Add( default );
                }
                else
                {
                    if( e is not T item || !set.Contains( item ) ) return false;
                    unique ??= new HashSet<T?>();
                    unique.Add( item );
                }
            }
            return unique == null || unique.Count < set.Count;
        }

        public static bool NullableIsSubsetOf<T, TA>( HashSet<T?> set, IEnumerable<TA?> other )
        {
            Throw.CheckNotNullArgument( other );
            // The empty set is a subset of any set, and a set is a subset of itself.
            if( other == set || set.Count == 0 ) return true;
            if( other is IEnumerable<T?> same ) return set.IsSubsetOf( same );
            HashSet<T?>? typed = null;
            foreach( var e in other )
            {
                switch( e )
                {
                    case T item:
                        typed ??= new HashSet<T?>( set.Comparer );
                        typed.Add( item );
                        break;
                    case null:
                        typed ??= new HashSet<T?>( set.Comparer );
                        typed.Add( default );
                        break;
                }
            }
            return typed != null && typed.Count >= set.Count && set.IsSubsetOf( typed );
        }

    }
}
