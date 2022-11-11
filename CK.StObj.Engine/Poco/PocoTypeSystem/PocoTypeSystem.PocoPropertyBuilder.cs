using CK.CodeGen;
using CK.Core;
using CommunityToolkit.HighPerformance.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;
using NullabilityInfoContext = System.Reflection.TEMPNullabilityInfoContext;

namespace CK.Setup
{
    using RegisterResult = IPocoTypeSystem.RegisterResult;

    public sealed partial class PocoTypeSystem
    {
        sealed class PocoPropertyBuilder
        {
            readonly PocoTypeSystem _system;
            IPocoPropertyInfo? _prop;
            IExtPropertyInfo? _bestProperty;
            RegisterResult? _bestReg;
            IExtMemberInfo? _defaultValueSource;
            FieldDefaultValue? _defaultValue;
            RegisterResult?[] _cachedResult;

            public PocoPropertyBuilder( PocoTypeSystem system )
            {
                _system = system;
                _cachedResult = new RegisterResult?[16];
            }

            public PrimaryPocoField? Build( IActivityMonitor monitor, PocoType.PrimaryPocoType p, IPocoPropertyInfo prop )
            {
                _prop = prop;
                _bestProperty = null;
                _bestReg = null;
                _defaultValueSource = null;
                _defaultValue = null;
                if( _cachedResult.Length < prop.DeclaredProperties.Count )
                {
                    _cachedResult = new RegisterResult?[Math.Max(_cachedResult.Length * 2, prop.DeclaredProperties.Count)];
                }
                else
                {
                    Array.Clear( _cachedResult );
                }
                if( !TryFindWritableAndCheckReadOnlys( monitor ) )
                {
                    return null;
                }
                bool isWritable = _bestReg.HasValue;
                if( !isWritable )
                {
                    // No writable property defines the type.
                    // Trying to infer it and check this against all the other properties.
                    if( !TryInferTypeFromReadOnlysAndCheck( monitor ) )
                    {
                        return null;
                    }
                }
                Debug.Assert( _bestReg != null && _bestProperty != null );
                ref var best = ref _bestReg.DangerousGetValueOrDefaultReference();
                if( _defaultValue != null
                    && _defaultValue.SimpleValue != null
                    && !best.PocoType.Type.IsAssignableFrom( _defaultValue.SimpleValue.GetType() ) )
                {
                    monitor.Error( $"Invalid DefaultValue attribute on {prop}: default value {_defaultValue} is not compatible with type '{best.RegCSharpName}'." );
                    return null;
                }
                // Now that we know that there are no issues on the unified type across the property implementations
                // we handle the potential UnionType actual type.
                var finalType = best.PocoType;
                if( prop.UnionTypeDefinition.Count > 0 )
                {
                    Debug.Assert( best.PocoType.Kind == PocoTypeKind.Any, "Only 'object' property can be used for union types." );
                    finalType = HandleUnionTypeDefinition( monitor, prop );
                    if( finalType == null ) return null;
                }
                return new PrimaryPocoField( prop, finalType, best.RegCSharpName, _bestProperty.PropertyInfo.CanWrite, p, _bestProperty.Type.IsByRef, _defaultValue );
            }

            IUnionPocoType? HandleUnionTypeDefinition( IActivityMonitor monitor, IPocoPropertyInfo prop )
            {
                Debug.Assert( _bestReg != null && prop.UnionTypeDefinition.Count > 0 );
                var isNullable = _bestReg.Value.PocoType.IsNullable;
                bool success = true;
                List<IPocoType> types = new List<IPocoType>();
                foreach( var pU in prop.UnionTypeDefinition )
                {
                    var nInfo = pU.GetHomogeneousNullabilityInfo( monitor );
                    if( nInfo == null ) return null;
                    // The Value Tuple union type definition is always not nullable just like all its types:
                    // the nullability depends only on the object property nullability.
                    if( nInfo.IsNullable )
                    {
                        monitor.Error( $"{pU.DeclaringType!.DeclaringType.ToCSharpName()}.UnionTypes.{prop.Name}: union type definition must be a non nullable value tuple." );
                        return null;
                    }
                    if( !nInfo.Type.IsValueTuple() )
                    {
                        monitor.Error( $"Property '{prop.Name}' of the nested 'class {pU.DeclaringType.DeclaringType.ToCSharpName()}.UnionTypes' must be a value tuple (current type is {nInfo.Type.ToCSharpName()})." );
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
                        if( tInfo.IsNullable )
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
                            // We don't have this capability easily and this seems rather useless.
                            // So to keep things simple, we reject such ambiguities by testing the newly registered type
                            // to have the same nullabilities as any previously registered IsAssignableFrom types.
                            var rOneType = _system.RegisterRoot( monitor, new MemberContext( pU ), tInfo );
                            if( rOneType == null ) return null;
                            var oneType = rOneType.Value.PocoType;
                            // If the value is nullable, we project the nullability on each type
                            // of the union.
                            if( isNullable ) oneType = oneType.Nullable;
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
                        return isNullable ? newFinal.Nullable : newFinal;
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
                int idx = 0;
                foreach( var p in _prop.DeclaredProperties )
                {
                    if( !Add( monitor, p, idx ) )
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
                    ++idx;
                }
                return true;
            }

            bool TryInferTypeFromReadOnlysAndCheck( IActivityMonitor monitor )
            {
                Debug.Assert( _prop != null && _bestReg == null );
                bool isNotNull = false;
                int idx = 0;
                foreach( var p in _prop.DeclaredProperties )
                {
                    var nInfo = p.GetHomogeneousNullabilityInfo( monitor );
                    if( nInfo == null ) return false;
                    isNotNull |= !nInfo.IsNullable;
                    // As soon as we know that the type cannot be nullable and
                    // we have one, we can break.
                    if( isNotNull && _bestReg.HasValue ) break;
                    var reg = _cachedResult[idx] ?? _system.Register( monitor, p );
                    if( reg == null ) return false;
                    if( !reg.Value.PocoType.IsAbstract )
                    {
                        _bestProperty = p;
                        _bestReg = reg;
                        if( isNotNull ) break;
                    }
                    ++idx;
                }
                //if( _bestReg != null )
                //{
                //    Debug.Assert( _bestProperty != null );
                //    if( isNotNull ) _bestReg = _bestReg.Value.NonNullable;
                //    if( _bestProperty.Type != _bestReg.Type )
                //    {
                //        monitor.Trace( $"Inferred type '{_bestReg.CSharpName}' for {_prop}." );
                //    }
                //    idx = 0;
                //    foreach( var p in _prop.DeclaredProperties )
                //    {
                //        if( p == _bestProperty ) continue;
                //        if( !CheckNewReadOnly( monitor, p, cache[idx++] ) )
                //        {
                //            return false;
                //        }
                //    }
                //}

                return Throw.NotSupportedException<bool>( "Not yet." );

                //NullabilityInfo[] cache = new NullabilityInfo[_prop.DeclaredProperties.Count];
                //bool isNotNull = false;
                //int idx = 0;
                //foreach( var p in _prop.DeclaredProperties )
                //{
                //    var nInfo = _system._nullContext.Create( p );
                //    cache[idx++] = nInfo;
                //    isNotNull |= nInfo.ReadState == NullabilityState.NotNull;
                //    // As soon as we know that the type cannot be nullable and
                //    // we have one, we can break.
                //    if( isNotNull && _bestReg != null ) break;

                //    // If we can't find a concrete, we don't care: the error will
                //    // be handled while checking the read only properties.
                //    var t = TryFindConcreteOrPossible( monitor, p.PropertyType, p );
                //    if( t == null ) continue;

                //    if( t != typeof(object) && _bestReg == null )
                //    {
                //        _bestProperty = p;
                //        _bestReg = _system.RegisterRoot( monitor, new MemberContext( p ), nInfo );
                //        if( _bestReg == null ) return false;
                //        if( isNotNull ) break;
                //    }
                //}
                //if( _bestReg != null )
                //{
                //    Debug.Assert( _bestProperty != null );
                //    if( isNotNull ) _bestReg = _bestReg.NonNullable;
                //    if( _bestProperty.PropertyType != _bestReg.Type )
                //    {
                //        monitor.Trace( $"Inferred type '{_bestReg.CSharpName}' for {_prop}." );
                //    }
                //    idx = 0;
                //    foreach( var p in _prop.DeclaredProperties )
                //    {
                //        if( p == _bestProperty ) continue;
                //        if( !CheckNewReadOnly( monitor, p, cache[idx++] ) )
                //        {
                //            return false;
                //        }
                //    }
                //}
                //else
                //{
                //    if( isNotNull && _defaultValue == null )
                //    {
                //        monitor.Error( $"{_prop} has no writable property, no default value and a concrete instantiable type cannot be inferred. It cannot be resolved." );
                //        return false;
                //    }
                //    _bestReg = _system._objectType;
                //}
                //return true;

                //// Shallow type analysis of a candidate.
                //static Type? TryFindConcreteOrPossible( IActivityMonitor monitor, Type t, PropertyInfo p )
                //{
                //    if( t.IsValueType || t.IsArray || t == typeof( string ) || t == typeof( object ) )
                //    {
                //        return t;
                //    }
                //    if( t.IsGenericType )
                //    {
                //        var tGen = t.GetGenericTypeDefinition();

                //        if( tGen == typeof( List<> ) || tGen == typeof( HashSet<> ) || tGen == typeof( Dictionary<,> ) )
                //        {
                //            // No variance here. No better choice than the type itself...
                //            return t;
                //        }
                //        // This is the only covariant collection that can be handled directly.
                //        if( tGen == typeof( IReadOnlyList<> ) )
                //        {
                //            var args = t.GetGenericArguments();
                //            var tV = TryFindConcreteOrPossible( monitor, args[0], p );
                //            if( tV == null ) return null;
                //            return typeof( List<> ).MakeGenericType( args );
                //        }
                //        // To support the other ones, there's more to do (below for set, but it's the same story for
                //        // dictionary and its TValue):
                //        // - We need a ICKReadOnlySet<out T> where T : class with object as the in type in the interface.
                //        //   (the where T : class is to avoid the boxing fiesta).
                //        // - A class CKReadOnlySet : HashSet<T>, ICKReadOnlySet<out T>
                //        // - Users will have to explicitly use ICKReadOnlySet instead of IReadOnlySet for their read only views.
                //        // - This method will then be more complex: a "compliant list of types" has to be maintained across the
                //        //   properties for each type at stake.
                //        if( tGen == typeof( IReadOnlySet<> ) )
                //        {
                //            return typeof( HashSet<> ).MakeGenericType( t.GetGenericArguments() );
                //        }
                //        if( tGen == typeof( IReadOnlyDictionary<,> ) )
                //        {
                //            return typeof( Dictionary<,> ).MakeGenericType( t.GetGenericArguments() );
                //        }
                //        return null;
                //    }
                //    if( typeof( IPoco ).IsAssignableFrom( t ) ) return t;
                //    monitor.Error( $"Property '{p.DeclaringType.ToCSharpName()}.{p.Name}': found type '{t}' that is not a Poco type." );
                //    return null;
                //}
            }

            bool Add( IActivityMonitor monitor, IExtPropertyInfo p, int idxP )
            {
                Debug.Assert( _prop != null );

                bool isWritable = p.PropertyInfo.CanWrite || p.Type.IsByRef;
                if( !isWritable && !p.Type.IsValueType && p.Type.IsGenericType )
                {
                    // The property is not directly writable.
                    // If it's a I/List<>, I/Set<> or I/Dictionary<> then it also is
                    // a "writable" property.
                    var rColl = _system.TryRegisterCollection( monitor, p, out var error );
                    if( error ) return false;
                    if( rColl != null )
                    {
                        _cachedResult[idxP] = rColl;
                        Debug.Assert( rColl.Value.ReadOnlyStatus != null, "If it's a collection, it is either mutable or read only." );
                        if( rColl.Value.ReadOnlyStatus == false )
                        {
                            // Concrete collection: this fixes the type.
                            return AddWritable( monitor, p, idxP );
                        }
                    }
                    // It's not a concrete collection.
                    // Let the following code do its job (CheckNewReadOnly if a best has been found) since
                    // isWritable is false.
                }
                if( isWritable )
                {
                    if( !AddWritable( monitor, p, idxP ) ) return false;
                    // On success, always check that a record must be a ref property, that a collection must not
                    // have a setter and that any other type must be a regular property.
                    Debug.Assert( _bestReg != null );
                    ref var best = ref _bestReg.DangerousGetValueOrDefaultReference();
                    Debug.Assert( best.PocoType is not IRecordPocoType || best.PocoType.Type.IsValueType, "IRecordPocoType => ValueType." );
                    if( best.PocoType is IRecordPocoType )
                    {
                        if( !p.Type.IsByRef )
                        {
                            monitor.Error( $"Property '{p.DeclaringType}.{p.Name}' must be a ref property: 'ref {best.RegCSharpName} {p.Name} {{ get; }}'." );
                            return false;
                        }
                    }
                    else
                    {
                        if( best.PocoType is ICollectionPocoType )
                        {
                            monitor.Error( $"Property '{p.DeclaringType}.{p.Name}' is a {best.PocoType.Kind}, it must be a read only property: '{best.RegCSharpName} {p.Name} {{ get; }}'." );
                            return false;
                        }
                        if( p.Type.IsByRef )
                        {
                            monitor.Error( $"Property '{p.DeclaringType}.{p.Name}' is not a record nor a collection, it must be a regular property with a setter: '{best.RegCSharpName} {p.Name} {{ get; set; }}'." );
                            return false;
                        }
                    }
                    return true;
                }
                if( _bestProperty != null && !CheckNewReadOnly( monitor, p ) )
                {
                    return false;
                }
                return true;

                bool AddWritable( IActivityMonitor monitor, IExtPropertyInfo p, int idxP )
                {
                    var reg = _cachedResult[idxP] ??= _system.Register( monitor, p );
                    if( !reg.HasValue ) return false;
                    if( _bestReg.HasValue )
                    {
                        Debug.Assert( _bestProperty != null );
                        if( reg.Value.PocoType != _bestReg.Value.PocoType )
                        {
                            monitor.Error( $"{p}: Type must be exactly '{_bestReg.Value.RegCSharpName}' since '{_bestProperty.DeclaringType.ToCSharpName()}.{_bestProperty.Name}' defines it." );
                            return false;
                        }
                        return true;
                    }
                    _bestProperty = p;
                    _bestReg = reg;
                    return CheckExistingReadOnlyProperties( monitor, p );
                }

                bool CheckExistingReadOnlyProperties( IActivityMonitor monitor, IExtPropertyInfo p )
                {
                    foreach( var pRead in _prop.DeclaredProperties )
                    {
                        if( pRead == p ) break;
                        if( !CheckNewReadOnly( monitor, pRead ) )
                        {
                            return false;
                        }
                    }
                    return true;
                }

            }

            bool CheckNewReadOnly( IActivityMonitor monitor, IExtPropertyInfo p )
            {
                Debug.Assert( _bestProperty != null && _bestReg != null );
                ref var best = ref _bestReg.DangerousGetValueOrDefaultReference();

                var nInfo = p.GetHomogeneousNullabilityInfo( monitor );
                if( nInfo == null ) return false;

                if( !best.PocoType.IsReadableType( nInfo ) )
                {
                    monitor.Error( $"Read only {p}: Type is not compatible with '{best.RegCSharpName} {_bestProperty.DeclaringType.ToCSharpName()}.{_bestProperty.Name}'." );
                    return false;
                }
                return true;
            }
        }
    }
}
