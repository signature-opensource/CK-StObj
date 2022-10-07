using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        /// <summary>
        /// Encapsulates a list of variants.
        /// </summary>
        sealed class UnionType
        {
            readonly List<NullableTypeTree> _types;

            public UnionType( bool canBeExtended, List<NullableTypeTree> types )
            {
                _types = types;
                CanBeExtended = canBeExtended;
            }

            /// <summary>
            /// Gets the types, sorted by their FullName. This is used by <see cref="HasSameVariantsAs(UnionType)"/>:
            /// SequenceEquals can be used.
            /// </summary>
            public IReadOnlyList<NullableTypeTree> Types => _types;

            public bool CanBeExtended { get; set; }

            public UnionType Clone()
            {
                Debug.Assert( CanBeExtended, "Useless to clone otherwise." );
                return new UnionType(CanBeExtended, new List<NullableTypeTree>( _types ) );
            }

            public bool HasSameVariantsAs( UnionType y )
            {
                return y == this && y != null && _types.SequenceEqual( y._types );
            }

            public void AddExtended( IActivityMonitor monitor, PocoPropertyImpl best, PocoPropertyImpl other )
            {
                Debug.Assert( !other.IsReadOnly && other.UnionTypes != null );
                var logName = $"Final union from '{best}'";
                using( monitor.OpenInfo( $"extending {logName} with variants from {other}." ) )
                {
                    foreach( var t in other.UnionTypes.Types )
                    {
                        AddWithoutDuplicate( monitor, logName, _types, t );
                    }
                }
            }

            public static bool TryCreate( IActivityMonitor monitor,
                                          PropertyInfo info,
                                          ref PropertyInfo[]? cacheUnionTypesDef,
                                          bool isPropertyNullable,
                                          out UnionType? unionType )
            {
                unionType = null;
                var unionAttr = info.GetCustomAttributes<UnionTypeAttribute>().FirstOrDefault();
                if( unionAttr != null )
                {
                    Type declaringType = info.DeclaringType!;

                    if( !TryGetUnionDefinition( monitor,
                                                declaringType,
                                                info,
                                                ref cacheUnionTypesDef,
                                                isPropertyNullable,
                                                out NullableTypeTree rootDefinition ) )
                    {
                        return false;
                    }
                    var types = TryCreateTypes( monitor, declaringType, info, info.PropertyType, rootDefinition );
                    if( types == null )
                    {
                        return false;
                    }
                    if( types.Count == 1 )
                    {
                        monitor.Warn( $"UnionType {declaringType}.{info.Name} contains only one type. This is weird (but ignored)." );
                    }
                    else
                    {
                        types.Sort( (x,y) => String.CompareOrdinal( x.Type.FullName, y.Type.FullName ) );
                    }
                    unionType = new UnionType( unionAttr.CanBeExtended, types );
                }
                return true;
            }

            static bool TryGetUnionDefinition( IActivityMonitor monitor,
                                               Type declaringType,
                                               PropertyInfo info,
                                               ref PropertyInfo[]? cacheUnionTypesDef,
                                               bool isPropertyNullable,
                                               out NullableTypeTree definition )
            {
                definition = default;
                if( cacheUnionTypesDef == null )
                {
                    Type? u = declaringType.GetNestedType( "UnionTypes", BindingFlags.Public | BindingFlags.NonPublic );
                    if( u == null )
                    {
                        monitor.Error( $"[UnionType] attribute on '{declaringType.FullName}.{info.Name}' requires a nested 'class UnionTypes {{ public (int?,string) {info.Name} {{ get; }} }}' with the types (here, (int?,string) is just an example of course)." );
                        return false;
                    }
                    cacheUnionTypesDef = u.GetProperties();
                }
                var fieldDef = cacheUnionTypesDef.FirstOrDefault( f => f.Name == info.Name );
                if( fieldDef == null )
                {
                    monitor.Error( $"The nested class UnionTypes requires a public value tuple '{info.Name}' property." );
                    return false;
                }
                definition = fieldDef.GetNullableTypeTree();
                if( (definition.Kind & NullabilityTypeKind.IsTupleType) == 0 )
                {
                    monitor.Error( $"The property of the nested 'class {declaringType}.UnionTypes.{info.Name}' must be a value tuple (current type is {definition})." );
                    return false;
                }
                if( definition.Kind.IsNullable() != isPropertyNullable )
                {
                    monitor.Error( $"'{declaringType}.UnionTypes.{info.Name}' must{(isPropertyNullable ? "" : "NOT")} BE nullable since the property itself is {(isPropertyNullable ? "" : "NOT ")}nullable." );
                    return false;
                }
                return true;
            }

            static List<NullableTypeTree>? TryCreateTypes( IActivityMonitor monitor,
                                                           Type declaringType,
                                                           PropertyInfo info,
                                                           Type propertyType,
                                                           NullableTypeTree definition )
            {
                List<NullableTypeTree> types = new List<NullableTypeTree>();
                bool valid = true;
                List<string>? typeDeviants = null;
                List<string>? nullableDef = null;
                List<string>? interfaceCollections = null;
                bool isWritableProperty = info.CanWrite;
                var logName = $"{declaringType}.{info.Name}";
                foreach( var sub in definition.SubTypes )
                {
                    if( sub.Type == typeof( object ) )
                    {
                        monitor.Error( $"'{declaringType}.UnionTypes.{info.Name}' cannot define the type 'object' since this would erase all possible types." );
                        valid = false;
                    }
                    else
                    {
                        if( !propertyType.IsAssignableFrom( sub.Type ) )
                        {
                            if( typeDeviants == null ) typeDeviants = new List<string>();
                            typeDeviants.Add( sub.ToString() );
                            valid = false;
                        }
                        else if( isWritableProperty )
                        {
                            valid &= CheckOneWritableType( out valid, ref interfaceCollections, sub );
                        }
                        if( sub.Kind.IsNullable() )
                        {
                            if( nullableDef == null ) nullableDef = new List<string>();
                            nullableDef.Add( sub.ToString() );
                            valid = false;
                        }
                    }
                    valid &= AddWithoutDuplicate( monitor, logName, types, sub );
                }
                if( typeDeviants != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{logName}'. Union type{(typeDeviants.Count > 1 ? "s" : "")} '{typeDeviants.Concatenate( "' ,'" )}' {(typeDeviants.Count > 1 ? "are" : "is")} incompatible with the property type '{propertyType.Name}'." );
                }
                if( nullableDef != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{logName}'. Union type definitions must not be nullable: please change '{nullableDef.Concatenate( "' ,'" )}' to be not nullable." );
                }
                if( interfaceCollections != null )
                {
                    monitor.Error( $"Invalid [UnionType] attribute on '{logName}'. Collection types must be concrete: {interfaceCollections.Concatenate()}." );
                }
                if( valid )
                {
                    if( types.Count == 1 )
                    {
                        monitor.Warn( $"{logName}: UnionType contains only one type. This is weird (but ignored)." );
                    }
                    return types;
                }
                return null;
            }

            static bool AddWithoutDuplicate( IActivityMonitor monitor, string logName, List<NullableTypeTree> unionTypes, NullableTypeTree newType )
            {
                Debug.Assert( !newType.Kind.IsNullable() );
                for( int i = 0; i < unionTypes.Count; ++i )
                {
                    var tN = unionTypes[i];
                    var t = tN.Type;
                    var newOne = newType.Type;
                    if( t.IsAssignableFrom( newOne ) )
                    {
                        monitor.Warn( newOne == t
                                        ? $"{logName}: UnionType '{t.ToCSharpName()}' duplicated. Removing one."
                                        : $"{logName}: UnionType '{t.ToCSharpName()}' is assignable from (is more general than) '{newOne.ToCSharpName()}'. Removing the second one." );
                        return true;
                    }
                    else if( newOne.IsAssignableFrom( t ) )
                    {
                        monitor.Warn( $"{logName}: UnionType '{newOne.ToCSharpName()}' is assignable from (is more general than) '{t.ToCSharpName()}'. Removing the second one." );
                        unionTypes.RemoveAt( i-- );
                    }
                }
                unionTypes.Add( newType );
                return true;
            }

            static bool CheckOneWritableType( out bool valid, ref List<string>? interfaceCollections, NullableTypeTree sub )
            {
                valid = true;
                if( sub.Type.IsGenericType )
                {
                    var tGen = sub.Type.GetGenericTypeDefinition();
                    if( tGen == typeof( IList<> )
                        || tGen == typeof( IReadOnlyList<> ) )
                    {
                        if( interfaceCollections == null ) interfaceCollections = new List<string>();
                        interfaceCollections.Add( $"{sub} should be a List<{sub.RawSubTypes[0]}>" );
                        valid = false;
                    }
                    else if( tGen == typeof( IDictionary<,> )
                             || tGen == typeof( IReadOnlyDictionary<,> ) )
                    {
                        if( interfaceCollections == null ) interfaceCollections = new List<string>();
                        interfaceCollections.Add( $"{sub} should be a Dictionary<{sub.RawSubTypes[0]},{sub.RawSubTypes[1]}>" );
                        valid = false;
                    }
                    else if( tGen == typeof( ISet<> )
                              || tGen == typeof( IReadOnlySet<> ) )
                    {
                        if( interfaceCollections == null ) interfaceCollections = new List<string>();
                        interfaceCollections.Add( $"{sub} should be a HashSet<{sub.RawSubTypes[0]}>" );
                        valid = false;
                    }
                }
                return valid;
            }

            public override string ToString()
            {
                return _types.Select( t => t.ToString() ).Concatenate();
            }

        }
    }
}
