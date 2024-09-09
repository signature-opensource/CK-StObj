using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Testing
{
    /// <summary>
    /// Helper that collects types.
    /// </summary>
    [Obsolete("Use the EngineConfiguration directly.")]
    public sealed class TypeCollector : ISet<Type>
    {
        readonly HashSet<Type> _types;
        [Obsolete("Use the EngineConfiguration directly")]
        internal TypeCollector()
        {
            _types = new HashSet<Type>();
        }

        /// <inheritdoc />
        public int Count => _types.Count;

        /// <summary>
        /// Adds a type.
        /// </summary>
        /// <param name="t">The type to add.</param>
        /// <returns>This collector (fluent syntax).</returns>
        public TypeCollector Add( Type t )
        {
            _types.Add( t );
            return this;
        }

        /// <summary>
        /// Adds a set of type.
        /// </summary>
        /// <param name="types">The types to add.</param>
        /// <returns>This collector (fluent syntax).</returns>
        public TypeCollector Add( IEnumerable<Type> types )
        {
            _types.AddRange( types );
            return this;
        }

        /// <inheritdoc cref="Add(IEnumerable{Type})"/>
        public TypeCollector Add( params Type[] types )
        {
            _types.AddRange( types );
            return this;
        }

        bool ISet<Type>.Add( Type item ) => _types.Add( item );

        /// <inheritdoc />
        public void ExceptWith( IEnumerable<Type> other ) => _types.ExceptWith( other );

        /// <inheritdoc />
        public void IntersectWith( IEnumerable<Type> other ) => _types.IntersectWith( other );

        /// <inheritdoc />
        public bool IsProperSubsetOf( IEnumerable<Type> other ) => _types.IsProperSubsetOf( other );

        /// <inheritdoc />
        public bool IsProperSupersetOf( IEnumerable<Type> other ) => _types.IsProperSupersetOf( other );

        /// <inheritdoc />
        public bool IsSubsetOf( IEnumerable<Type> other ) => _types.IsSubsetOf( other );

        /// <inheritdoc />
        public bool IsSupersetOf( IEnumerable<Type> other ) => _types.IsSupersetOf( other );

        /// <inheritdoc />
        public bool Overlaps( IEnumerable<Type> other ) => _types.Overlaps( other );

        /// <inheritdoc />
        public bool SetEquals( IEnumerable<Type> other ) => _types.SetEquals( other );

        /// <inheritdoc />
        public void SymmetricExceptWith( IEnumerable<Type> other ) => _types.SymmetricExceptWith( other );

        /// <inheritdoc />
        public void UnionWith( IEnumerable<Type> other ) => _types.UnionWith( other );

        void ICollection<Type>.Add( Type item ) => _types.Add( item );

        /// <inheritdoc />
        public void Clear() => _types.Clear();

        /// <inheritdoc />
        public bool Contains( Type item ) => _types.Contains( item );

        /// <inheritdoc />
        public void CopyTo( Type[] array, int arrayIndex ) => _types.CopyTo( array, arrayIndex );

        /// <inheritdoc />
        public bool Remove( Type item ) => _types.Remove( item );

        /// <inheritdoc />
        public IEnumerator<Type> GetEnumerator() => _types.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _types.GetEnumerator();

        bool ICollection<Type>.IsReadOnly => false;


    }

}