using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;
using NullabilityInfoContext = System.Reflection.TEMPNullabilityInfoContext;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystem
    {
        sealed class PocoPropertyBuilder
        {
            readonly PocoTypeSystem _system;
            IPocoPropertyInfo? _prop;
            PropertyInfo? _best;
            IPocoType? _finalType;
            MemberInfo? _defaultValueSource;
            FieldDefaultValue? _defaultValue;

            public PocoPropertyBuilder( PocoTypeSystem system )
            {
                _system = system;
            }

            public PrimaryPocoField? Build( IActivityMonitor monitor, PocoType.PrimaryPocoType p, IPocoPropertyInfo prop )
            {
                _prop = prop;
                _best = null;
                _finalType = null;
                _defaultValueSource = null;
                _defaultValue = null;
                if( !TryFindWritableAndCheckReadOnlys( monitor ) )
                {
                    return null;
                }
                bool isReadOnly;
                if( isReadOnly = (_finalType == null) )
                {
                    // No writable property defines the type.
                    // Trying to infer it and check this against all the other properties.
                    if( !TryInferTypeFromReadOnlysAndCheck( monitor ) )
                    {
                        return null;
                    }
                }
                Debug.Assert( _finalType != null && _best != null );
                if( _defaultValue != null
                    && _defaultValue.SimpleValue != null
                    && !_finalType.Type.IsAssignableFrom( _defaultValue.SimpleValue.GetType() ) )
                {
                    monitor.Error( $"Invalid DefaultValue attribute on {prop}: default value {_defaultValue} is not compatible with type '{_finalType}'." );
                    return null;
                }
                // Now that we know that there are no issues on the unified type across the property implementations
                // we handle the potential UnionType actual type.
                if( prop.UnionTypeDefinition.Count > 0 )
                {
                    Debug.Assert( _finalType.Kind == PocoTypeKind.Any, "Only 'object' property can be used for union types." );
                    _finalType = HandleUnionTypeDefinition( monitor, prop );
                    if( _finalType == null ) return null;
                }
                return new PrimaryPocoField( prop, _finalType, isReadOnly, p, _best.PropertyType.IsByRef, _defaultValue );
            }

            IUnionPocoType? HandleUnionTypeDefinition( IActivityMonitor monitor, IPocoPropertyInfo prop )
            {
                Debug.Assert( _finalType != null && prop.UnionTypeDefinition.Count > 0 );
                bool success = true;
                List<IPocoType> types = new List<IPocoType>();
                foreach( var pU in prop.UnionTypeDefinition )
                {
                    var nInfo = _system._nullContext.Create( pU );
                    // The Value Tuple union type definition is always not nullable just like all its types:
                    // the nullability depends only on the object property nullability.
                    if( nInfo.ReadState == NullabilityState.Nullable )
                    {
                        monitor.Error( $"{pU.DeclaringType!.DeclaringType.ToCSharpName()}.UnionTypes.{prop.Name}: union type definition must be a non nullable value tuple." );
                        return null;
                    }
                    if( !nInfo.Type.IsValueTuple() )
                    {
                        monitor.Error( $"Property '{prop.Name}' of the nested 'class {pU.DeclaringType!.DeclaringType.ToCSharpName()}.UnionTypes' must be a value tuple (current type is {nInfo.Type.ToCSharpName()})." );
                        return null;
                    }
                    foreach( var tInfo in nInfo.GenericTypeArguments )
                    {
                        if( tInfo.Type == typeof(object) )
                        {
                            monitor.Error( $"'{pU.DeclaringType!.DeclaringType.ToCSharpName()}.UnionTypes.{prop.Name}' cannot define the type 'object' since this would erase all possible types." );
                            success = false;
                        }
                        // No nullable definition must exist: the nullability depends only on the object property nullability.
                        if( tInfo.ReadState == NullabilityState.Nullable )
                        {
                            monitor.Error( $"{pU.DeclaringType!.DeclaringType.ToCSharpName()}.UnionTypes.{prop.Name}: type definition '{tInfo.Type.ToCSharpName(false)}' must not be nullable: nullability of the union type is defined by the 'object' property nullability." );
                            success = false;
                        }
                        if( success )
                        {
                            // If the type is a IPoco family member, we map it to the PrimaryInterface.
                            // This supports once for all the family equivalence class by erasing the specific
                            // interface.
                            // When the type is an abstract IPoco, we keep it as-is: IsAssignableFrom is required
                            // and will work.
                            //
                            // Note that this works because we have first created all the IPoco types: now that we handle
                            // their fields can we rely on their topology.
                            //
                            // Nullability handling needs more thoughts...
                            //
                            // A List<int> is not related to a List<int?>. We can keep both and let the user deal with
                            // this complexity.
                            // But for List<User> and List<User?> a choice must be made: which one should be kept?
                            // Or should this be forbidden?
                            // The answer depends on the point of view: is a UnionType definition a mean to "relax" a type or
                            // a mean to "constrain" it? Adding a type to a union type is clearly "relaxing" it: does it mean 
                            // that List<User?> should win against a List<User> type? Maybe not, because a UnionType is a "writable":
                            // when the user states that List<User> is supported, she doesn't want a List<User?> to "come back".
                            // But we must consider the user to be aware of all the types defined in an union type. The question is
                            // "What does it mean to define both List<User> and List<User?> types?".
                            // There seems to be no answer: we have no way to know which type "came first" and which type is the
                            // "amendment". To unambiguously resolve this we'd need a kind of "Replace" trait on the type definition
                            // We don't have this capability and this seems rather useless.
                            // So to keep things simple, we reject such ambiguities by testing the newly registered type
                            // to have the same nullabilities as any previously registered IsAssignableFrom types.
                            var oneType = _system.RegisterRoot( monitor, new MemberContext( pU ) { UsePrimaryPocoMapping = true }, tInfo );
                            if( oneType == null ) return null;
                            if( oneType is IConcretePocoType poco )
                            {
                                oneType = poco.PrimaryInterface;
                            }
                            // If the value is nullable, we project the nullability on each type
                            // of the union.
                            if( _finalType.IsNullable ) oneType = oneType.Nullable;
                            var newOne = oneType.Type;
                            for( int i = 0; i < types.Count; ++i )
                            {
                                var tN = types[i];
                                var t = tN.Type;
                                if( t.IsAssignableFrom( newOne ) )
                                {
                                    if( newOne == t )
                                    {
                                        monitor.Warn( $"{prop}: UnionType '{t.ToCSharpName()}' duplicated. Removing one." );
                                    }
                                    else
                                    {
                                        // TODO: FindNullabilityViolations here between the 2 types.
                                        monitor.Warn( $"{prop}: UnionType '{t.ToCSharpName()}' is assignable from (is more general than) '{newOne.ToCSharpName()}'. Removing the second one." );
                                    }
                                    oneType = null;
                                }
                                else if( newOne.IsAssignableFrom( t ) )
                                {
                                    // TODO: FindNullabilityViolations here between the 2 types.
                                    monitor.Warn( $"{prop}: UnionType '{newOne.ToCSharpName()}' is assignable from (is more general than) '{t.ToCSharpName()}'. Removing the second one." );
                                    types.RemoveAt( i-- );
                                }
                            }
                            if( oneType != null ) types.Add( oneType );
                        }
                    }
                }
                if( success )
                {
                    var newFinal = _system.RegisterUnionType( monitor, types );
                    if( newFinal != null )
                    {
                        return _finalType.IsNullable ? newFinal.Nullable : newFinal;
                    }
                }
                return null;
            }

            /// <summary>
            /// One pass on the declared properties, computing the default values and
            /// trying to find a writable property: if it exists, it fixes the type and
            /// the read only properties are challenged against the writable one.
            /// </summary>
            bool TryFindWritableAndCheckReadOnlys( IActivityMonitor monitor )
            {
                Debug.Assert( _prop != null );
                foreach( var p in _prop.DeclaredProperties )
                {
                    if( !Add( monitor, p ) )
                    {
                        return false;
                    }
                    if( _defaultValue == null )
                    {
                        _defaultValue = FieldDefaultValue.CreateFromAttribute( monitor, _system.StringBuilderPool, p );
                        if( _defaultValue != null ) _defaultValueSource = p;
                    }
                    else
                    {
                        Debug.Assert( _defaultValueSource != null );
                        if( !_defaultValue.CheckSameOrNone( monitor, _defaultValueSource, _system.StringBuilderPool, p ) )
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            bool TryInferTypeFromReadOnlysAndCheck( IActivityMonitor monitor )
            {
                Debug.Assert( _prop != null && _finalType == null );

                NullabilityInfo[] cache = new NullabilityInfo[_prop.DeclaredProperties.Count];
                bool isNotNull = false;
                int idx = 0;
                foreach( var p in _prop.DeclaredProperties )
                {
                    var nInfo = _system._nullContext.Create( p );
                    cache[idx++] = nInfo;
                    isNotNull |= nInfo.ReadState == NullabilityState.NotNull;
                    // As soon as we know that the type cannot be nullable and
                    // we have one, we can break.
                    if( isNotNull && _finalType != null ) break;

                    // If we can't find a concrete, we don't care: the error will
                    // be handled while checking the read only properties.
                    var t = TryFindConcreteOrPossible( monitor, p.PropertyType, p );
                    if( t == null ) continue;

                    if( t != typeof(object) && _finalType == null )
                    {
                        _best = p;
                        _finalType = _system.RegisterRoot( monitor, new MemberContext( p ), nInfo );
                        if( _finalType == null ) return false;
                        if( isNotNull ) break;
                    }
                }
                if( _finalType != null )
                {
                    Debug.Assert( _best != null );
                    if( isNotNull ) _finalType = _finalType.NonNullable;
                    if( _best.PropertyType != _finalType.Type )
                    {
                        monitor.Trace( $"Inferred type '{_finalType.CSharpName}' for {_prop}." );
                    }
                    idx = 0;
                    foreach( var p in _prop.DeclaredProperties )
                    {
                        if( p == _best ) continue;
                        if( !CheckNewReadOnly( monitor, p, cache[idx++] ) )
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if( isNotNull && _defaultValue == null )
                    {
                        monitor.Error( $"{_prop} has no writable property, no default value and a concrete instantiable type cannot be inferred. It cannot be resolved." );
                        return false;
                    }
                    _finalType = _system._objectType;
                }
                return true;

                // Shallow type analysis of a candidate.
                static Type? TryFindConcreteOrPossible( IActivityMonitor monitor, Type t, PropertyInfo p )
                {
                    if( t.IsValueType || t.IsArray || t == typeof( string ) || t == typeof( object ) )
                    {
                        return t;
                    }
                    if( t.IsGenericType )
                    {
                        var tGen = t.GetGenericTypeDefinition();

                        if( tGen == typeof( List<> ) || tGen == typeof( HashSet<> ) || tGen == typeof( Dictionary<,> ) )
                        {
                            // No variance here. No better choice than the type itself...
                            return t;
                        }
                        // This is the only covariant collection that can be handled directly.
                        if( tGen == typeof( IReadOnlyList<> ) )
                        {
                            var args = t.GetGenericArguments();
                            var tV = TryFindConcreteOrPossible( monitor, args[0], p );
                            if( tV == null ) return null;
                            return typeof( List<> ).MakeGenericType( args );
                        }
                        // To support the other ones, there's more to do (below for set, but it's the same story for
                        // dictionary and its TValue):
                        // - We need a ICKReadOnlySet<out T> where T : class with object as the in type in the interface.
                        //   (the where T : class is to avoid the boxing fiesta).
                        // - A class CKReadOnlySet : HashSet<T>, ICKReadOnlySet<out T>
                        // - Users will have to explicitly use ICKReadOnlySet instead of IReadOnlySet for their read only views.
                        // - This method will then be more complex: a "compliant list of types" has to be maintained across the
                        //   properties for each type at stake.
                        if( tGen == typeof( IReadOnlySet<> ) )
                        {
                            return typeof( HashSet<> ).MakeGenericType( t.GetGenericArguments() );
                        }
                        if( tGen == typeof( IReadOnlyDictionary<,> ) )
                        {
                            return typeof( Dictionary<,> ).MakeGenericType( t.GetGenericArguments() );
                        }
                        return null;
                    }
                    if( typeof( IPoco ).IsAssignableFrom( t ) ) return t;
                    monitor.Error( $"Property '{p.DeclaringType.ToCSharpName()}.{p.Name}': found type '{t}' that is not a Poco type." );
                    return null;
                }
            }

            bool Add( IActivityMonitor monitor, PropertyInfo p )
            {
                Debug.Assert( _prop != null );
                bool isWritable = p.CanWrite || p.PropertyType.IsByRef;
                if( !isWritable && !p.PropertyType.IsValueType && p.PropertyType.IsGenericType )
                {
                    // The property is not directly writable.
                    // If it's a IList<>, ISet<> or IDictionary<> then it also is
                    // a "writable" property.
                    var fT = _system.RegisterWritableCollection( monitor, p, out var error );
                    if( error ) return false;
                    if( fT != null )
                    {
                        // It's a concrete collection.
                        if( _best == null )
                        {
                            _best = p;
                            _finalType = fT;
                            if( !CheckExistingReadOnlyProperties( monitor, p ) )
                            {
                                return false;
                            }
                        }
                        else
                        {
                            // If a writable has been previously found, we must
                            // be on the same type.
                            Debug.Assert( _finalType != null );
                            if( _finalType != fT )
                            {
                                monitor.Error( $"Property '{p.DeclaringType.ToCSharpName()}.{p.Name}': Type must be exactly '{_finalType.CSharpName}' since '{_best.DeclaringType.ToCSharpName()}.{_best.Name}' defines it." );
                                return false;
                            }
                        }
                        return true;
                    }
                    // It's not a concrete collection.
                    // Let the following code do its job (CheckNewReadOnly if there is a best) since
                    // isWritable is false.
                }
                if( isWritable )
                {
                    if( _best == null )
                    {
                        _best = p;
                        _finalType = _system.Register( monitor, p );
                        if( _finalType == null ) return false;
                        if( !CheckExistingReadOnlyProperties( monitor, p ) )
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if( !CheckNewWritable( monitor, p ) )
                        {
                            return false;
                        }
                    }
                    // On success, always check that a record must be a ref property, that a collection must not
                    // have a setter and that any other type must be a regular property.
                    Debug.Assert( _finalType != null );
                    Debug.Assert( _finalType is not IRecordPocoType || _finalType.Type.IsValueType, "IRecordPocoType => ValueType." );
                    if( _finalType is IRecordPocoType )
                    {
                        if( !p.PropertyType.IsByRef )
                        {
                            monitor.Error( $"Property '{p.DeclaringType}.{p.Name}' must be a ref property: 'ref {_finalType.CSharpName} {p.Name} {{ get; }}'." );
                            return false;
                        }
                    }
                    else
                    {
                        if( _finalType is ICollectionPocoType )
                        {
                            monitor.Error( $"Property '{p.DeclaringType}.{p.Name}' is a {_finalType.Kind}, it must be a read only property: '{_finalType.CSharpName} {p.Name} {{ get; }}'." );
                            return false;
                        }
                        if( p.PropertyType.IsByRef )
                        {
                            monitor.Error( $"Property '{p.DeclaringType}.{p.Name}' is not a record, it must be a regular property with a setter: '{_finalType.CSharpName} {p.Name} {{ get; set; }}'." );
                            return false;
                        }
                    }
                    return true;
                }
                if( _best != null && !CheckNewReadOnly( monitor, p, null ) )
                {
                    return false;
                }
                return true;

                bool CheckExistingReadOnlyProperties( IActivityMonitor monitor, PropertyInfo p )
                {
                    foreach( var pRead in _prop.DeclaredProperties )
                    {
                        if( pRead == p ) break;
                        if( !CheckNewReadOnly( monitor, pRead, null ) )
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            bool CheckNewWritable( IActivityMonitor monitor, PropertyInfo p )
            {
                Debug.Assert( _best != null && _finalType != null );
                if( _finalType.IsWritableType( p.PropertyType ) )
                {
                    var nInfo = _system._nullContext.Create( p );
                    var culprit = FindNullabilityViolation( _finalType, nInfo, strict: true );
                    if( culprit == null ) return true;
                    if( culprit != _finalType )
                    {
                        if( !culprit.IsNullable )
                        {
                            monitor.Error( $"Invalid nullable '{culprit.Nullable.CSharpName}' in '{p.DeclaringType.ToCSharpName()}.{p.Name}' type. It cannot be nullable since it is not nullable in '{_finalType.CSharpName} {_best.DeclaringType.ToCSharpName()}.{_best.Name}'." );
                        }
                        else
                        {
                            monitor.Error( $"Invalid non nullable '{culprit.NonNullable.CSharpName}' in '{p.DeclaringType.ToCSharpName()}.{p.Name}' type. It cannot be not null since it is nullable in '{_finalType.CSharpName} {_best.DeclaringType.ToCSharpName()}.{_best.Name}'." );
                        }
                        return false;
                    }
                }
                monitor.Error( $"Property '{p.DeclaringType.ToCSharpName()}.{p.Name}': Type must be exactly '{_finalType.CSharpName}' since '{_best.DeclaringType.ToCSharpName()}.{_best.Name}' defines it." );
                return false;
            }

            bool CheckNewReadOnly( IActivityMonitor monitor, PropertyInfo p, NullabilityInfo? knownInfo )
            {
                Debug.Assert( _best != null && _finalType != null );

                // If the read only property cannot be assigned to the write one, this is an error.
                // We are bound to the C# limitations here and don't try to workaround this: we
                // currently don't implement or support adapters.
                //  - IReadOnlyList is covariant without adapters.
                //  - IReadOnlyDictionary and IReadOnlySet require an adapter to be covariant on TValue (resp. T).
                //    This alone (as a root property type is easy) but as soon as the type appear in a array, list or set, 
                //    a type must be generated with inner converters/wrappers... 
                if( !_finalType.IsReadableType( p.PropertyType ) )
                {
                    monitor.Error( $"Read only property '{p.DeclaringType.ToCSharpName()}.{p.Name}': Type is not compatible with '{_finalType.CSharpName} {_best.DeclaringType.ToCSharpName()}.{_best.Name}'." );
                    return false;
                }
                // But IsReadableType (and even type equality) is not enough because of reference types.
                // We prevent any nullable to non nullable mapping.
                knownInfo ??= _system._nullContext.Create( p );
                var culprit = FindNullabilityViolation( _finalType, knownInfo, strict: false );
                if( culprit != null )
                {
                    if( culprit == _finalType )
                    {
                        monitor.Error( $"Read only property '{p.DeclaringType.ToCSharpName()}.{p.Name}' cannot be nullable since '{_finalType.CSharpName} {_best.DeclaringType.ToCSharpName()}.{_best.Name}' is not." );
                        return false;
                    }
                    Debug.Assert( !culprit.IsNullable );
                    monitor.Error( $"Invalid nullable '{culprit.Nullable.CSharpName}' in '{p.DeclaringType.ToCSharpName()}.{p.Name}' type. It cannot be nullable since it is not nullable in '{_finalType.CSharpName} {_best.DeclaringType.ToCSharpName()}.{_best.Name}'." );
                    return false;
                }
                return true;
            }

            static IPocoType? FindNullabilityViolation( IPocoType w, NullabilityInfo r, bool strict )
            {
                bool rIsNullable = r.ReadState == NullabilityState.Nullable || r.ReadState == NullabilityState.Unknown;
                if( (!rIsNullable && w.IsNullable) || (strict && rIsNullable != w.IsNullable) )
                {
                    return w;
                }
                if( r.Type != typeof( object ) )
                {
                    if( w is ICollectionPocoType wC )
                    {
                        Debug.Assert( r.Type.IsArray || (r.Type.IsGenericType && r.GenericTypeArguments.Length == wC.ItemTypes.Count) );

                        // We don't have (yet) the AbstractCollectionType : PocoType since we only handle the single IReadOnlyList
                        // covariant type.
                        // Even in non strict mode, collection items cannot be allowed be nullable
                        // if the "source" item is not nullable.
                        // Currently, IReadOnlyList is the only exception.
                        // Before thinking to implement a "better covariance" and AbstractCollectionType, the first step
                        // is to transfer this code to IPocoType.IsWritable( NullabilityInfo t ) and IPocoType.IsReadable( NullabilityInfo t )
                        // that will centralize the nullable stuff. 
                        if( r.Type.IsGenericType && r.Type.GetGenericTypeDefinition() == typeof( IReadOnlyList<> ) )
                        {
                            strict = false;
                        }
                        else
                        {
                            strict = true;
                        }
                        for( int i = 0; i < wC.ItemTypes.Count; i++ )
                        {
                            var culprit = FindNullabilityViolation( wC.ItemTypes[i], r.ElementType ?? r.GenericTypeArguments[i], strict );
                            if( culprit != null ) return culprit;
                        }
                    }
                }
                return null;
            }
        }
    }
}
