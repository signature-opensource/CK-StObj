using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core;

public static partial class CovariantHelpers
{
    /// <summary>
    /// Implements <see cref="ISet{T}.IsProperSubsetOf(IEnumerable{T})"/> but against any other type of items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a proper subset of other; otherwise, false.</returns>
    public static bool IsProperSubsetOf<T, TA>( HashSet<T> set, IEnumerable<TA> other ) where T : notnull
    {
        Throw.CheckNotNullArgument( other );
        // No set is a proper subset of itself.
        if( other == set ) return false;
        if( other is IEnumerable<T> same ) return set.IsProperSubsetOf( same );
        bool hasAlien = false;
        var unique = new HashSet<T>( set.Comparer );
        foreach( var e in other )
        {
            // The empty set is a proper subset of anything but the empty set.
            if( set.Count == 0 ) return true;
            if( e is T item )
            {
                unique.Add( item );
            }
            else
            {
                hasAlien = true;
            }
        }
        // No set is a proper subset of an empty set.
        if( unique.Count == 0 ) return hasAlien;
        Debug.Assert( set.Count > 0 );
        return hasAlien
                ? set.IsSubsetOf( unique )
                : set.IsProperSubsetOf( unique );
    }

    /// <summary>
    /// Implements <see cref="ISet{T}.IsSubsetOf(IEnumerable{T})"/> but against any other type of items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a subset of other; otherwise, false.</returns>
    public static bool IsSubsetOf<T, TA>( HashSet<T> set, IEnumerable<TA> other ) where T : notnull
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

    /// <summary>
    /// Implements <see cref="ISet{T}.IsProperSupersetOf(IEnumerable{T})"/> but against any other type of items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a proper superset of other; otherwise, false.</returns>
    public static bool IsProperSupersetOf<T, TA>( HashSet<T> set, IEnumerable<TA> other ) where T : notnull
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

    /// <summary>
    /// Implements <see cref="ISet{T}.IsSupersetOf(IEnumerable{T})"/> but against any other type of items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a superset of other; otherwise, false.</returns>
    public static bool IsSupersetOf<T, TA>( HashSet<T> set, IEnumerable<TA> other ) where T : notnull
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

    /// <summary>
    /// Implements <see cref="ISet{T}.Overlaps(IEnumerable{T})"/> but against any other type of items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set overlaps other; otherwise, false.</returns>
    public static bool Overlaps<T, TA>( HashSet<T> set, IEnumerable<TA> other ) where T : notnull
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

    /// <summary>
    /// Implements <see cref="ISet{T}.SetEquals(IEnumerable{T})"/> but against any other type of items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set has the same unique elements as other; otherwise, false.</returns>
    public static bool SetEquals<T, TA>( HashSet<T> set, IEnumerable<TA> other ) where T : notnull
    {
        Throw.CheckNotNullArgument( other );
        if( other == set ) return true;
        if( other is IEnumerable<T> same ) return set.SetEquals( same );
        var unique = new HashSet<T>( set.Comparer );
        foreach( var e in other )
        {
            if( e is not T item || !set.Contains( item ) ) return false;
            unique.Add( item );
        }
        return unique.Count == set.Count;
    }

    /// <summary>
    /// Implements <see cref="ISet{T}.IsProperSubsetOf(IEnumerable{T})"/> where null
    /// can appear in the set, against any other type of nullable items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a proper subset of other; otherwise, false.</returns>
    public static bool NullableIsProperSubsetOf<T, TA>( HashSet<T?> set, IEnumerable<TA?> other )
    {
        Throw.CheckNotNullArgument( other );
        if( other == set ) return false;
        if( other is IEnumerable<T?> same ) return set.IsProperSubsetOf( same );
        bool hasAlien = false;
        var unique = new HashSet<T?>( set.Comparer );
        foreach( var e in other )
        {
            // The empty set is a proper subset of anything but the empty set.
            if( set.Count == 0 ) return true;
            if( e is T item )
            {
                unique.Add( item );
            }
            else
            {
                if( e == null ) unique.Add( default );
                else hasAlien = true;
            }
        }
        // No set is a proper subset of an empty set.
        if( unique.Count == 0 ) return hasAlien;
        Debug.Assert( set.Count > 0 );
        return hasAlien
                ? set.IsSubsetOf( unique )
                : set.IsProperSubsetOf( unique );
    }

    /// <summary>
    /// Implements <see cref="ISet{T}.IsProperSupersetOf(IEnumerable{T})"/> where null
    /// can appear in the set, against any other type of nullable items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a proper superset of other; otherwise, false.</returns>
    public static bool NullableIsProperSupersetOf<T, TA>( HashSet<T?> set, IEnumerable<TA?> other )
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

    /// <summary>
    /// Implements <see cref="ISet{T}.IsSubsetOf(IEnumerable{T})"/> where null
    /// can appear in the set, against any other type of nullable items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a subset of other; otherwise, false.</returns>
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

    /// <summary>
    /// Implements <see cref="ISet{T}.IsSupersetOf(IEnumerable{T})"/> where null
    /// can appear in the set, against any other type of nullable items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set is a superset of other; otherwise, false.</returns>
    public static bool NullableIsSupersetOf<T, TA>( HashSet<T?> set, IEnumerable<TA?> other )
    {
        Throw.CheckNotNullArgument( other );
        if( other == set ) return true;
        if( other is IEnumerable<T?> same ) return set.IsSupersetOf( same );
        foreach( var e in other )
        {
            bool found = (e is T item && set.Contains( item )) || (e == null && set.Contains( default ));
            if( !found ) return false;
        }
        return true;
    }

    /// <summary>
    /// Implements <see cref="ISet{T}.Overlaps(IEnumerable{T})"/> where null
    /// can appear in the set, against any other type of nullable items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set overlaps other; otherwise, false.</returns>
    public static bool NullableOverlaps<T, TA>( HashSet<T?> set, IEnumerable<TA?> other )
    {
        Throw.CheckNotNullArgument( other );
        if( set.Count == 0 ) return false;
        if( other == set ) return true;
        if( other is IEnumerable<T?> same ) return set.Overlaps( same );
        foreach( var e in other )
        {
            if( (e is T item && set.Contains( item )) || (e == null && set.Contains( default )) ) return true;
        }
        return false;
    }

    /// <summary>
    /// Implements <see cref="ISet{T}.SetEquals(IEnumerable{T})"/> where null
    /// can appear in the set, against any other type of nullable items.
    /// </summary>
    /// <typeparam name="T">The type of the HashSet item.</typeparam>
    /// <typeparam name="TA">The type of the other collection.</typeparam>
    /// <param name="set">The set.</param>
    /// <param name="other">The collection to challenge.</param>
    /// <returns>true if the set has the same unique elements as other; otherwise, false.</returns>
    public static bool NullableSetEquals<T, TA>( HashSet<T?> set, IEnumerable<TA?> other )
    {
        Throw.CheckNotNullArgument( other );
        if( other == set ) return true;
        if( other is IEnumerable<T?> same ) return set.SetEquals( same );
        var unique = new HashSet<T?>( set.Comparer );
        foreach( var e in other )
        {
            if( e is T item )
            {
                if( unique.Add( item ) && !set.Contains( item ) ) return false;
            }
            else
            {
                if( e != null ) return false;
                if( unique.Add( default ) && !set.Contains( default ) ) return false;
            }
        }
        return unique.Count == set.Count;
    }

}
