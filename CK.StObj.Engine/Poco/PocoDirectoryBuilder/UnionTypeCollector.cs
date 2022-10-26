using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static CK.Setup.PocoType;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates a list of PropertyInfo that must be ValueTuples. Each of them defines
    /// the possible types of the union type. No check is done at this level except the fact
    /// that all [UnionType] attribute must CanBeExtended or not, it is the PocoTypeSystem
    /// that checks the types and nullabilities.
    /// </summary>
    sealed class UnionTypeCollector
    {
        readonly List<PropertyInfo> _types;

        public UnionTypeCollector( bool canBeExtended, PropertyInfo firstDef )
        {
            _types = new List<PropertyInfo> { firstDef };
            CanBeExtended = canBeExtended;
        }

        public List<PropertyInfo> Types => _types;

        public bool CanBeExtended { get; }

        //public static bool TryGetUnionDefinition( IActivityMonitor monitor,
        //                                          Type declaringType,
        //                                          PropertyInfo info,
        //                                          ref PropertyInfo[]? cacheUnionTypesDef,
        //                                          out NullableTypeTree definition )
        //{
        //    definition = default;
        //    if( cacheUnionTypesDef == null )
        //    {
        //        Type? u = declaringType.GetNestedType( "UnionTypes", BindingFlags.Public | BindingFlags.NonPublic );
        //        if( u == null )
        //        {
        //            monitor.Error( $"[UnionType] attribute on '{declaringType.FullName}.{info.Name}' requires a nested 'class UnionTypes {{ public (int?,string) {info.Name} {{ get; }} }}' with the types (here, (int?,string) is just an example of course)." );
        //            return false;
        //        }
        //        cacheUnionTypesDef = u.GetProperties();
        //    }
        //    var fieldDef = cacheUnionTypesDef.FirstOrDefault( f => f.Name == info.Name );
        //    if( fieldDef == null )
        //    {
        //        monitor.Error( $"The nested class UnionTypes requires a public value tuple '{info.Name}' property." );
        //        return false;
        //    }
        //    definition = fieldDef.GetNullableTypeTree();
        //    if( (definition.Kind & NullabilityTypeKind.IsTupleType) == 0 )
        //    {
        //        monitor.Error( $"The property of the nested 'class {declaringType}.UnionTypes.{info.Name}' must be a value tuple (current type is {definition})." );
        //        return false;
        //    }
        //    if( definition.Kind.IsNullable() != isPropertyNullable )
        //    {
        //        monitor.Error( $"'{declaringType}.UnionTypes.{info.Name}' must{(isPropertyNullable ? "" : "NOT")} BE nullable since the property itself is {(isPropertyNullable ? "" : "NOT ")}nullable." );
        //        return false;
        //    }
        //    return true;
        //}

        //static List<NullableTypeTree>? TryCreateTypes( IActivityMonitor monitor,
        //                                               Type declaringType,
        //                                               PropertyInfo info,
        //                                               Type propertyType,
        //                                               NullableTypeTree definition )
        //{
        //    List<NullableTypeTree> types = new List<NullableTypeTree>();
        //    bool valid = true;
        //    List<string>? typeDeviants = null;
        //    List<string>? nullableDef = null;
        //    List<string>? interfaceCollections = null;
        //    bool isWritableProperty = info.CanWrite;
        //    var logName = $"{declaringType}.{info.Name}";
        //    foreach( var sub in definition.SubTypes )
        //    {
        //        if( sub.Type == typeof( object ) )
        //        {
        //            monitor.Error( $"'{declaringType}.UnionTypes.{info.Name}' cannot define the type 'object' since this would erase all possible types." );
        //            valid = false;
        //        }
        //        else
        //        {
        //            if( !propertyType.IsAssignableFrom( sub.Type ) )
        //            {
        //                if( typeDeviants == null ) typeDeviants = new List<string>();
        //                typeDeviants.Add( sub.ToString() );
        //                valid = false;
        //            }
        //            else if( isWritableProperty )
        //            {
        //                valid &= CheckOneWritableType( out valid, ref interfaceCollections, sub );
        //            }
        //            if( sub.Kind.IsNullable() )
        //            {
        //                if( nullableDef == null ) nullableDef = new List<string>();
        //                nullableDef.Add( sub.ToString() );
        //                valid = false;
        //            }
        //        }
        //        valid &= AddWithoutDuplicate( monitor, logName, types, sub );
        //    }
        //    if( typeDeviants != null )
        //    {
        //        monitor.Error( $"Invalid [UnionType] attribute on '{logName}'. Union type{(typeDeviants.Count > 1 ? "s" : "")} '{typeDeviants.Concatenate( "' ,'" )}' {(typeDeviants.Count > 1 ? "are" : "is")} incompatible with the property type '{propertyType.Name}'." );
        //    }
        //    if( nullableDef != null )
        //    {
        //        monitor.Error( $"Invalid [UnionType] attribute on '{logName}'. Union type definitions must not be nullable: please change '{nullableDef.Concatenate( "' ,'" )}' to be not nullable." );
        //    }
        //    if( interfaceCollections != null )
        //    {
        //        monitor.Error( $"Invalid [UnionType] attribute on '{logName}'. Collection types must be concrete: {interfaceCollections.Concatenate()}." );
        //    }
        //    if( valid )
        //    {
        //        if( types.Count == 1 )
        //        {
        //            monitor.Warn( $"{logName}: UnionType contains only one type. This is weird (but ignored)." );
        //        }
        //        return types;
        //    }
        //    return null;
        //}

        //static bool AddWithoutDuplicate( IActivityMonitor monitor, string logName, List<NullableTypeTree> unionTypes, NullableTypeTree newType )
        //{
        //    Debug.Assert( !newType.Kind.IsNullable() );
        //    for( int i = 0; i < unionTypes.Count; ++i )
        //    {
        //        var tN = unionTypes[i];
        //        var t = tN.Type;
        //        var newOne = newType.Type;
        //        if( t.IsAssignableFrom( newOne ) )
        //        {
        //            monitor.Warn( newOne == t
        //                            ? $"{logName}: UnionType '{t.ToCSharpName()}' duplicated. Removing one."
        //                            : $"{logName}: UnionType '{t.ToCSharpName()}' is assignable from (is more general than) '{newOne.ToCSharpName()}'. Removing the second one." );
        //            return true;
        //        }
        //        else if( newOne.IsAssignableFrom( t ) )
        //        {
        //            monitor.Warn( $"{logName}: UnionType '{newOne.ToCSharpName()}' is assignable from (is more general than) '{t.ToCSharpName()}'. Removing the second one." );
        //            unionTypes.RemoveAt( i-- );
        //        }
        //    }
        //    unionTypes.Add( newType );
        //    return true;
        //}

        //static bool CheckOneWritableType( out bool valid, ref List<string>? interfaceCollections, NullableTypeTree sub )
        //{
        //    valid = true;
        //    if( sub.Type.IsGenericType )
        //    {
        //        var tGen = sub.Type.GetGenericTypeDefinition();
        //        if( tGen == typeof( IList<> )
        //            || tGen == typeof( IReadOnlyList<> ) )
        //        {
        //            if( interfaceCollections == null ) interfaceCollections = new List<string>();
        //            interfaceCollections.Add( $"{sub} should be a List<{sub.RawSubTypes[0]}>" );
        //            valid = false;
        //        }
        //        else if( tGen == typeof( IDictionary<,> )
        //                 || tGen == typeof( IReadOnlyDictionary<,> ) )
        //        {
        //            if( interfaceCollections == null ) interfaceCollections = new List<string>();
        //            interfaceCollections.Add( $"{sub} should be a Dictionary<{sub.RawSubTypes[0]},{sub.RawSubTypes[1]}>" );
        //            valid = false;
        //        }
        //        else if( tGen == typeof( ISet<> )
        //                  || tGen == typeof( IReadOnlySet<> ) )
        //        {
        //            if( interfaceCollections == null ) interfaceCollections = new List<string>();
        //            interfaceCollections.Add( $"{sub} should be a HashSet<{sub.RawSubTypes[0]}>" );
        //            valid = false;
        //        }
        //    }
        //    return valid;
        //}

        public override string ToString()
        {
            return _types.Select( t => t.ToString() ).Concatenate();
        }

    }

}
