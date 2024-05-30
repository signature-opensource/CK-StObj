using CK.Core;
using FluentAssertions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Testing
{
    /// <summary>
    /// Helper that collects types.
    /// </summary>
    public sealed class TypeCollector : ISet<Type>
    {
        readonly HashSet<Type> _types;
        readonly Dictionary<Assembly, AssemblyType> _all;

        enum AssemblyType
        {
            None,
            Model,
            ModelDependent
        }

        internal TypeCollector()
        {
            _all = new Dictionary<Assembly, AssemblyType>();
            _types = new HashSet<Type>();
        }

        /// <inheritdoc />
        public int Count => _types.Count;

        /// <summary>
        /// Adds all public types from <paramref name="root"/> and from its referenced assemblies
        /// only for assembly that are "model dependent".
        /// </summary>
        /// <param name="root">The root assembly from which public types must be collected.</param>
        /// <returns>True if the root is a model dependent assembly, false otherwise (no types have been added).</returns>
        public bool AddRootAssembly( Assembly root )
        {
            return DoAdd( root ) == AssemblyType.ModelDependent;
        }

        /// <summary>
        /// Gets the model dependent assemblies discovered so far.
        /// </summary>
        public IEnumerable<Assembly> ModelDependentAssemblies => _all.Where( kv => kv.Value == AssemblyType.ModelDependent ).Select( kv => kv.Key );

        AssemblyType DoAdd( Assembly assembly )
        {
            if( !_all.TryGetValue( assembly, out var t ) )
            {
                t = GetType( assembly );
                // Trust the fact that there is no cycle in assembly references.
                foreach( var d in assembly.GetReferencedAssemblies() )
                {
                    var dT = DoAdd( Assembly.Load( d ) );
                    if( t != AssemblyType.ModelDependent && dT != AssemblyType.None )
                    {
                        t = AssemblyType.ModelDependent;
                    }
                }
                _all.Add( assembly, t );
                if( t == AssemblyType.ModelDependent ) _types.AddRange( assembly.ExportedTypes );
            }
            return t;

            static AssemblyType GetType( Assembly a )
            {
                bool isModel = false;
                foreach( var d in a.GetCustomAttributesData() )
                {
                    if( d.AttributeType.Name == "IsModelDependentAttribute" ) return AssemblyType.ModelDependent;
                    if( d.AttributeType.Name == "IsModelAttribute" ) isModel = true;
                }
                return isModel ? AssemblyType.Model : AssemblyType.None;
            }

        }

        /// <inheritdoc />
        public bool Add( Type item ) => _types.Add( item );

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

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_types).GetEnumerator();

        bool ICollection<Type>.IsReadOnly => false;


    }
}
