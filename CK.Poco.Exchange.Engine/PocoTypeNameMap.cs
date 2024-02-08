using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Builds a type standardized name that is not the <see cref="IPocoType.CSharpName"/> for types in a <see cref="TypeSet"/>.
    /// By default nullable type names are suffixed by "?" but this may be changed by overriding the virtual name factories.
    /// <list type="bullet">
    ///   <item>
    ///   For <see cref="PocoTypeKind.Basic"/>, <see cref="PocoTypeKind.Any"/>, <see cref="PocoTypeKind.AbstractPoco"/> and
    ///   <see cref="PocoTypeKind.SecondaryPoco"/> it defaults to the <see cref="CSharpName"/> ("object, "int", "CK.Cris.ICommand", etc.).
    ///   This can be changed by overriding <see cref="MakeCSharpName"/>.
    ///   </item>
    ///   <item>
    ///   For <see cref="PocoTypeKind.AnonymousRecord"/> it is "(T1,T2,T3:Name,T4,...)". The ":Name" only appears when named field has a name.
    ///   This can be changed by overriding <see cref="MakeAnonymousRecord"/>.
    ///   </item>
    ///   <item>
    ///   For <see cref="IUnionPocoType"/> it is "T1|T2|...".
    ///   This can be changed by overriding <see cref="MakeUnionType"/>.
    ///   </item>
    ///   <item>
    ///   For <see cref="INamedPocoType"/> (but not anonymous records) it defaults to the <see cref="INamedPocoType.ExternalOrCSharpName"/>.
    ///   This can be changed by overriding <see cref="MakeNamedType"/>.
    ///   </item>
    ///   <item>
    ///   For <see cref="ICollectionPocoType"/> ts defaults to "A(T)" for array, "L(T)" for list, "S(T)" for set, "M(TKey,TValue)" for dictionary
    ///   or "O(TValue)" when the dictionary key is a string.
    ///   This can be changed by overriding <see cref="MakeCollection"/>.
    ///   </item>
    /// </list>
    /// <para>
    /// Names are created on demand. Names that are not in the <see cref="TypeSet"/> are never computed since an <see cref="ArgumentException"/>
    /// is raised if the type doesn't belong to the set.
    /// </para>
    /// </summary>
    public class PocoTypeNameMap : IPocoTypeNameMap
    {
        readonly string[] _names;
        readonly IPocoTypeSet _typeSet;

        /// <summary>
        /// Initializes a new name map.
        /// </summary>
        /// <param name="typeSet">The set of types to consider.</param>
        public PocoTypeNameMap( IPocoTypeSet typeSet )
        {
            Throw.CheckNotNullArgument( typeSet );
            _typeSet = typeSet;
            _names = new string[typeSet.TypeSystem.AllTypes.Count];
        }

        /// <inheritdoc />
        public IPocoTypeSystem TypeSystem => _typeSet.TypeSystem;

        /// <inheritdoc />
        public IPocoTypeSet TypeSet => _typeSet;

        /// <inheritdoc />
        public string GetName( IPocoType type )
        {
            ref var n = ref _names[type.Index];
            if( n == null )
            {
                if( !_typeSet.Contains( type ) )
                {
                    Throw.ArgumentException( $"Type '{type}' doesn't belong to the type set." );
                }
                Build( type.NonNullable, out var name, out var nullableName );
                if( String.IsNullOrWhiteSpace( name ) || String.IsNullOrWhiteSpace( nullableName ) )
                {
                    var badName = String.IsNullOrWhiteSpace( name ) ? "name" : "nullableName";
                    Throw.InvalidOperationException( $"Invalid MakeXXX override for '{type}': output '{badName}' is null or whitespace." );
                }
                _names[type.Index] = name;
                _names[type.Index + 1] = nullableName;
                Throw.DebugAssert( n != null );
            }
            return n;
        }

        /// <inheritdoc />
        public virtual IPocoTypeNameMap Clone( IPocoTypeSet other )
        {
            Throw.CheckNotNullArgument( other );
            return other != _typeSet ? new PocoTypeNameMap( other ) : this;
        }

        void Build( IPocoType t, out string name, out string nullableName )
        {
            switch( t.Kind )
            {
                case PocoTypeKind.Basic:
                case PocoTypeKind.Any:
                case PocoTypeKind.AbstractPoco:
                case PocoTypeKind.SecondaryPoco:
                    MakeCSharpName( t, out name, out nullableName );
                    return;
                case PocoTypeKind.AnonymousRecord:
                    {
                        var r = (IRecordPocoType)t;
                        var fields = r.Fields.Where( f => _typeSet.Contains( f.Type ) );
                        Throw.DebugAssert( fields.Any() );
                        MakeAnonymousRecord( r, fields, out name, out nullableName );
                        return;
                    }
                case PocoTypeKind.UnionType:
                    {
                        var u = (IUnionPocoType)t;
                        var allowed = u.AllowedTypes.Where( _typeSet.Contains );
                        Throw.DebugAssert( allowed.Any() );
                        MakeUnionType( u, allowed, out name, out nullableName );
                        return;
                    }
            }
            if( t is INamedPocoType namedType )
            {
                // For value tuples (anonymous records), this is the CSharpName that is considered:
                // this why we must handle PocoTypeKind.AnonymousRecord before.
                MakeNamedType( namedType, out name, out nullableName );
            }
            else
            {
                Throw.DebugAssert( t is ICollectionPocoType c && c.ItemTypes.All( _typeSet.Contains ) );
                MakeCollection( (ICollectionPocoType)t, out name, out nullableName );
            }
        }

        /// <summary>
        /// Makes a name for a <see cref="ICollectionPocoType"/>.
        /// </summary>
        /// <param name="t">The non nullable collection type.</param>
        /// <param name="name">Name for the non nullable type.</param>
        /// <param name="nullableName">Name for the nullable type.</param>
        protected virtual void MakeCollection( ICollectionPocoType t, out string name, out string nullableName )
        {
            // Shortcut for the IReadOnlyXXX: use their mutable type names.
            // They share the same ItemTypes and the IPocoTypeSet guaranties that if a IReadOnlyXXX is included then its
            // Mutable counter part is in the set (the reverse is not true).
            if( t.IsAbstractReadOnly )
            {
                Throw.DebugAssert( t.MutableCollection != t && t.ItemTypes == t.MutableCollection.ItemTypes && _typeSet.Contains( t.MutableCollection ) );
                name = GetName( t.MutableCollection );
                nullableName = _names[t.MutableCollection.Index + 1];
            }
            if( t.Kind == PocoTypeKind.Dictionary )
            {
                var k = t.ItemTypes[0];
                var vN = GetName( t.ItemTypes[1] );
                name = k.Type == typeof( string )
                                    ? $"O({vN})"
                                    : $"M({GetName( k )},{vN})";
            }
            else
            {
                name = $"{t.Kind switch { PocoTypeKind.Array => 'A', PocoTypeKind.List => 'L', _ => 'S' }}({GetName( t.ItemTypes[0] )})";
            }
            nullableName = name + '?';
        }

        /// <summary>
        /// Makes a name for a <see cref="INamedPocoType"/>.
        /// </summary>
        /// <param name="t">The non nullable named type.</param>
        /// <param name="name">Name for the non nullable type.</param>
        /// <param name="nullableName">Name for the nullable type.</param>
        protected virtual void MakeNamedType( INamedPocoType namedType, out string name, out string nullableName )
        {
            name = namedType.ExternalOrCSharpName;
            nullableName = namedType.Nullable.ExternalOrCSharpName;
        }

        /// <summary>
        /// Makes a name for a <see cref="IUnionPocoType"/>.
        /// </summary>
        /// <param name="t">The non nullable union type.</param>
        /// <param name="name">Name for the non nullable type.</param>
        /// <param name="nullableName">Name for the nullable type.</param>
        protected virtual void MakeUnionType( IUnionPocoType t, IEnumerable<IPocoType> allowedTypes, out string name, out string nullableName )
        {
            var b = new StringBuilder();
            foreach( var a in allowedTypes )
            {
                if( b.Length > 0 ) b.Append( '|' );
                b.Append( GetName( a ) );
            }
            name = b.ToString();
            b.Append( '?' );
            nullableName = b.ToString();
        }

        /// <summary>
        /// Makes a name for a value tuple: a <see cref="IRecordPocoType"/> where <see cref="IRecordPocoType.IsAnonymous"/> is true.
        /// </summary>
        /// <param name="t">The non nullable record type.</param>
        /// <param name="fields">Fields whose types belongs to the <see cref="TypeSet"/>.</param>
        /// <param name="name">Name for the non nullable type.</param>
        /// <param name="nullableName">Name for the nullable type.</param>
        protected virtual void MakeAnonymousRecord( IRecordPocoType t, IEnumerable<IRecordPocoField> fields, out string name, out string nullableName )
        {
            var b = new StringBuilder().Append( '(' );
            bool atLeastOne = false;
            foreach( var f in fields )
            {
                if( atLeastOne )
                {
                    b.Append( ',' );
                }
                else atLeastOne = true;
                b.Append( GetName( f.Type ) );
                if( !f.IsUnnamed )
                {
                    b.Append( ':' ).Append( f.Name );
                }
            }
            b.Append( ')' );
            name = b.ToString();
            b.Append( '?' );
            nullableName = b.ToString();
        }

        /// <summary>
        /// Makes a name for <see cref="PocoTypeKind.Basic"/>, <see cref="PocoTypeKind.Any"/>, <see cref="PocoTypeKind.AbstractPoco"/>
        /// and <see cref="PocoTypeKind.SecondaryPoco"/>.
        /// </summary>
        /// <param name="t">The non nullable type.</param>
        /// <param name="name">Name for the non nullable type.</param>
        /// <param name="nullableName">Name for the nullable type.</param>
        protected virtual void MakeCSharpName( IPocoType t, out string name, out string nullableName )
        {
            name = t.CSharpName;
            nullableName = t.Nullable.CSharpName;
        }
    }
}
