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
            // Used for the inferred type.
            PropertyInfo? _inferredPropertyInfo;

            public PocoPropertyBuilder( PocoTypeSystem system )
            {
                _system = system;
            }

            public PocoPropertyBuilder Inferred => this;

            public PrimaryPocoField? Build( IActivityMonitor monitor, PocoType.PrimaryPocoType p, IPocoPropertyInfo prop )
            {
                Debug.Assert( prop.DeclaredProperties.All( p => p.HomogeneousNullabilityInfo != null ) );
                _prop = prop;
                _bestProperty = null;
                _bestReg = null;
                _defaultValueSource = null;
                _defaultValue = null;
                if( !TryFindWritableAndCheckReadOnlys( monitor ) )
                {
                    return null;
                }
                bool isWritable = _bestReg.HasValue;
                if( !isWritable )
                {
                    // No writable property defines the type.
                    // Trying to infer it and check this against all the real properties.
                    var infered = ExtNullabilityInfo.ToConcrete( monitor, _system, prop.DeclaredProperties, out var longestTupleNamesAttribute );
                    if( infered == null )
                    {
                        var types = prop.DeclaredProperties.Select( p => p.TypeCSharpName ).Concatenate( Environment.NewLine );
                        monitor.Error( $"Failed to infer type from read only {prop} types:{Environment.NewLine}{types}" );
                        return null;
                    }
                    monitor.Trace( $"Inferred type for {prop}: {infered.Type:C}" );

                    _inferredPropertyInfo ??= GetType().GetProperty( nameof( Inferred ) )!;
                    var inferred = new ExtMemberInfo( _inferredPropertyInfo, infered, longestTupleNamesAttribute );
                    _bestReg = _system.Register( monitor, inferred );
                    if( _bestReg == null ) return null;
                    _bestProperty = inferred;
                    if( !CheckExistingReadOnlyProperties( monitor, null ) ) return null;
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
                if( prop.UnionTypeDefinition != null )
                {
                    Debug.Assert( best.PocoType.Kind == PocoTypeKind.Any, "Only 'object' property can be used for union types." );
                    finalType = HandleUnionTypeDefinition( monitor, prop );
                    if( finalType == null ) return null;
                }
                return new PrimaryPocoField( prop,
                                             finalType,
                                             best.RegCSharpName,
                                             _bestProperty.PropertyInfo.CanWrite,
                                             p,
                                             _bestProperty.Type.IsByRef,
                                             _defaultValue );
            }

            IUnionPocoType? HandleUnionTypeDefinition( IActivityMonitor monitor, IPocoPropertyInfo prop )
            {
                Debug.Assert( _bestReg != null && prop.UnionTypeDefinition != null );
                var isNullable = _bestReg.Value.PocoType.IsNullable;
                bool success = true;
                // The final list of Poco types to build.
                List<IPocoType> types = new List<IPocoType>();
                foreach( var pU in prop.UnionTypeDefinition.Types )
                {
                    var nInfo = pU.GetHomogeneousNullabilityInfo( monitor );
                    if( nInfo == null ) return null;
                    // The Value Tuple union type definition is always not nullable just like all its types:
                    // the nullability depends only on the object property nullability.
                    if( nInfo.IsNullable )
                    {
                        monitor.Error( $"{pU.DeclaringType!.DeclaringType!:N}.UnionTypes.{prop.Name}: union type definition must be a non nullable value tuple." );
                        return null;
                    }
                    if( !nInfo.Type.IsValueTuple() )
                    {
                        monitor.Error( $"Property '{prop.Name}' of the nested 'class {pU.DeclaringType.DeclaringType!:N}.UnionTypes' must be a value tuple (current type is {nInfo.Type:C})." );
                        return null;
                    }
                    foreach( var tInfo in nInfo.GenericTypeArguments )
                    {
                        if( tInfo.Type == typeof(object) )
                        {
                            monitor.Error( $"'{pU.DeclaringType!.DeclaringType!:N}.UnionTypes.{prop.Name}' cannot define the type 'object' since this would erase all possible types." );
                            success = false;
                        }
                        // No nullable definition must exist: the nullability depends only on the object property nullability.
                        if( tInfo.IsNullable )
                        {
                            monitor.Error( $"{pU.DeclaringType!.DeclaringType!:N}.UnionTypes.{prop.Name}: type definition '{tInfo.Type:C}' must not be nullable: nullability of the union type is defined by the 'object' property nullability." );
                            success = false;
                        }
                        if( success )
                        {
                            // If the type is a IPoco family member, we map it to the PrimaryInterface.
                            // This supports once for all the family equivalence class by erasing the specific
                            // interface.
                            //
                            // Note that this works because we have first created all the IPoco types: now that we handle
                            // their fields can we rely on their topology.
                            //
                            // Variance and nullability handling needs more thoughts...
                            //
                            // A List<int> is not related to a List<int?>. We can keep both and let the user deal with
                            // this complexity.
                            // But for List<User> and List<User?> a choice must be made: which one should be kept?
                            // Or should this be forbidden?
                            // The answer depends on the point of view: is a UnionType definition a mean to "relax" a type or
                            // a mean to "constrain" it? Adding a type to a union type is clearly "relaxing" it: does it mean 
                            // that List<User?> should win against a List<User> type? Maybe not, because a UnionType is a "writable":
                            // when the user states that List<User> is supported, she doesn't want a List<User?> to "come back"
                            // (with unexpected nulls in the list).
                            // Note that null variance is actually the same as type variance (T <: T?), this List<T>/List<T?>
                            // is a example of List<T>/List<X> with T <: X.
                            //
                            // If we consider the user to be aware of all the types defined in an union type, the question is
                            // "What does it mean to define both List<User> and List<User?> types?".
                            // There seems to be no answer: we have no way to know which type "came first" and which type is the
                            // "amendment". To unambiguously resolve this we'd need a kind of "Replace" trait on the type definition
                            // We don't have this capability easily and this seems rather useless.
                            //
                            // If we consider that the user is not aware of all the defined types, then a defined
                            // type should not be "widen" or "narrowed": union types must be unrelated to each others so
                            // that pattern matching on them will be deterministic.
                            //
                            // After a lot of thoughts to this, the answer is to rely on the "CanBeExtended" flag:
                            //  - When true, types can be widened.
                            //  - When false, types must be unrelated.
                            //
                            var rOneType = _system.Register( monitor, new MemberContext( pU ), tInfo );
                            if( rOneType == null ) return null;
                            var oneType = rOneType.Value.PocoType;
                            // If the value is nullable, we project the nullability on each type
                            // of the union.
                            var newInfo = tInfo;
                            if( isNullable )
                            {
                                oneType = oneType.Nullable;
                                newInfo = newInfo.ToNullable();
                            }
                            for( int i = 0; i < types.Count; ++i )
                            {
                                var tE = types[i];
                                var eeeeeeee = newInfo.ToString();
                                if( tE.IsSameType( newInfo ) )
                                {
                                    monitor.Warn( $"{prop}: UnionType '{rOneType.Value.RegCSharpName}' duplicated. Removing one." );
                                    oneType = null;
                                }
                                else if( tE.IsReadableType( newInfo ) )
                                {
                                    if( prop.UnionTypeDefinition.CanBeExtended )
                                    {
                                        monitor.Warn( $"{prop}: UnionType '{rOneType.Value.PocoType.CSharpName}' is more general than '{tE.CSharpName}'. Removing the second one." );
                                        oneType = null;
                                    }
                                    else
                                    {
                                        monitor.Error( $"{prop}: Ambiguous UnionType '{rOneType.Value.PocoType.CSharpName}' is more general than '{tE.CSharpName}'. Since CanBeExtended is false, types in the union must be unrelated." );
                                        success = false;
                                    }
                                }
                                else if( tE.IsWritableType( newInfo ) )
                                {
                                    if( prop.UnionTypeDefinition.CanBeExtended )
                                    {
                                        monitor.Warn( $"{prop}: UnionType '{tE.CSharpName}' is more general than '{rOneType.Value.PocoType.CSharpName}'. Removing the second one." );
                                        types.RemoveAt( i-- );
                                    }
                                    else
                                    {
                                        monitor.Error( $"{prop}: Ambiguous UnionType '{tE.CSharpName}' is more general than '{rOneType.Value.PocoType.CSharpName}'. Since CanBeExtended is false, types in the union must be unrelated." );
                                        success = false;
                                    }
                                }
                            }
                            if( success && oneType != null ) types.Add( oneType );
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

            bool Add( IActivityMonitor monitor, IExtPropertyInfo p, int idxP )
            {
                Debug.Assert( _prop != null );

                bool isWritable = p.PropertyInfo.CanWrite || p.Type.IsByRef;
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
                            monitor.Error( $"Property '{p.DeclaringType:N}.{p.Name}' must be a ref property: 'ref {best.RegCSharpName} {p.Name} {{ get; }}'." );
                            return false;
                        }
                    }
                    else
                    {
                        if( best.PocoType is ICollectionPocoType )
                        {
                            if( best.PocoType.Kind != PocoTypeKind.Array )
                            {
                                monitor.Error( $"Property '{p.DeclaringType:N}.{p.Name}' is a {best.PocoType.Kind}, it must be a read only property: '{best.RegCSharpName} {p.Name} {{ get; }}'." );
                                return false;
                            }
                        }
                        if( p.Type.IsByRef )
                        {
                            monitor.Error( $"Property '{p.DeclaringType:N}.{p.Name}' is not a record nor a collection, it must be a regular property with a setter: '{best.RegCSharpName} {p.Name} {{ get; set; }}'." );
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
                    var reg = _system.Register( monitor, p );
                    if( !reg.HasValue ) return false;
                    if( _bestReg.HasValue )
                    {
                        Debug.Assert( _bestProperty != null );
                        if( reg.Value.PocoType != _bestReg.Value.PocoType )
                        {
                            monitor.Error( $"{p}: Type must be exactly '{_bestReg.Value.RegCSharpName}' since '{_bestProperty.DeclaringType!:N}.{_bestProperty.Name}' defines it." );
                            return false;
                        }
                        return true;
                    }
                    _bestProperty = p;
                    _bestReg = reg;
                    return CheckExistingReadOnlyProperties( monitor, p );
                }
            }

            bool CheckExistingReadOnlyProperties( IActivityMonitor monitor, IExtPropertyInfo? stopper )
            {
                Debug.Assert( _prop != null );
                foreach( var pRead in _prop.DeclaredProperties )
                {
                    if( pRead == stopper ) break;
                    if( !CheckNewReadOnly( monitor, pRead ) )
                    {
                        return false;
                    }
                }
                return true;
            }

            bool CheckNewReadOnly( IActivityMonitor monitor, IExtPropertyInfo p )
            {
                Debug.Assert( _bestReg != null && _bestProperty != null );
                ref var best = ref _bestReg.DangerousGetValueOrDefaultReference();

                var nInfo = p.HomogeneousNullabilityInfo;
                Debug.Assert( nInfo != null );

                // If the property has no setter, then its type is allowed to be a nullable (since we have a writable,
                // either the writable is nullable or the property will never be null).
                // Note that if its a ref property then it is a writable one and we are not here.
                Debug.Assert( !p.PropertyInfo.PropertyType.IsByRef );
                var bestType = p.PropertyInfo.CanWrite ? best.PocoType : best.PocoType.Nullable;
                if( !bestType.IsReadableType( nInfo ) )
                {
                    using( monitor.OpenError( $"Read only {p} has incompatible types." ) )
                    {
                        monitor.Trace( $"Property type: {p.TypeCSharpName}" );
                        monitor.Trace( $"Implemented type: {best.PocoType}" );
                        monitor.Trace( $"Implementation decided by: {_bestProperty.DeclaringType:C}.{_bestProperty.Name}" );
                    }
                    return false;
                }
                return true;
            }
        }
    }
}
