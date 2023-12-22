using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static CK.CodeGen.TupleTypeName;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystem
    {
        sealed class PocoPropertyBuilder
        {
            readonly PocoTypeSystem _system;
            IPocoPropertyInfo? _props;
            IExtPropertyInfo? _bestProperty;
            PocoFieldAccessKind _fieldAccessKind;
            IPocoType? _bestReg;
            IExtMemberInfo? _defaultValueSource;
            FieldDefaultValue? _defaultValue;

            public PocoPropertyBuilder( PocoTypeSystem system )
            {
                _system = system;
            }

            public PrimaryPocoField? Build( IActivityMonitor monitor, PocoType.PrimaryPocoType p, IPocoPropertyInfo props )
            {
                Throw.DebugAssert( props.DeclaredProperties.All( p => p.HomogeneousNullabilityInfo != null ) );
                _props = props;
                _bestProperty = null;
                _bestReg = null;
                _defaultValueSource = null;
                _defaultValue = null;
                _fieldAccessKind = PocoFieldAccessKind.ReadOnly;
                if( !TryFindWritableAndCheckReadOnlys( monitor ) )
                {
                    return null;
                }
                Debug.Assert( _bestReg != null && _bestProperty != null );
                // Now that we know that there are no issues on the unified type across the property implementations:
                //  - We handle the potential UnionType type.
                //  - Or check the default value instance if there's one that will apply to the field.
                //
                // First, check the pathological case of an existing null default on a non null property.
                if( _defaultValue != null
                    && _defaultValue.SimpleValue == null
                    && !_bestReg.IsNullable )
                {
                    monitor.Error( $"Invalid null DefaultValue attribute on non nullable property {props}: '{_bestReg.CSharpName}'." );
                    return null;
                }

                IPocoType? finalType;
                if( props.UnionTypeDefinition != null )
                {
                    Debug.Assert( _bestReg.Kind == PocoTypeKind.Any, "Only 'object' property can be used for union types." );
                    // This has the awful side effect to alter the _defaultValue...
                    // But I'm tired and lazy.
                    finalType = HandleUnionTypeDefinition( monitor, props );
                    if( finalType == null ) return null;
                }
                else
                {
                    if( _defaultValue != null
                        && _defaultValue.SimpleValue != null
                        && !_bestReg.Type.IsAssignableFrom( _defaultValue.SimpleValue.GetType() ) )
                    {
                        monitor.Error( $"Invalid DefaultValue attribute on {props}: default value {_defaultValue} is not compatible with type '{_bestReg.CSharpName}'." );
                        return null;
                    }
                    finalType = _bestReg;
                }

                return new PrimaryPocoField( props,
                                             finalType,
                                             _fieldAccessKind,
                                             p,
                                             _defaultValue );
            }

            IUnionPocoType? HandleUnionTypeDefinition( IActivityMonitor monitor, IPocoPropertyInfo prop )
            {
                Debug.Assert( _bestReg != null && prop.UnionTypeDefinition != null );
                var isNullable = _bestReg.IsNullable;
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
                    var reusableContext = new MemberContext( pU );
                    foreach( var tInfo in nInfo.GenericTypeArguments )
                    {
                        if( tInfo.Type == typeof( object ) )
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
                            // Variance and nullability handling needs thoughts...
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
                            reusableContext.Reset();
                            var oneType = _system.Register( monitor, reusableContext, tInfo );
                            if( oneType == null ) return null;
                            // If the value is nullable, we project the nullability on each type
                            // of the union.
                            if( isNullable )
                            {
                                oneType = oneType.Nullable;
                            }
                            var oneTypeToAdd = oneType;
                            for( int i = 0; i < types.Count; ++i )
                            {
                                var tE = types[i];
                                if( tE.IsSamePocoType( oneType ) )
                                {
                                    monitor.Warn( $"{prop}: UnionType '{oneType.CSharpName}' duplicated. Removing one." );
                                    oneTypeToAdd = null;
                                }
                                else if( tE.IsReadableType( oneType ) )
                                {
                                    if( prop.UnionTypeDefinition.CanBeExtended )
                                    {
                                        monitor.Warn( $"{prop}: UnionType '{oneType.CSharpName}' is more general than '{tE.CSharpName}'. Removing the second one." );
                                        oneTypeToAdd = null;
                                    }
                                    else
                                    {
                                        monitor.Error( $"{prop}: Ambiguous UnionType '{oneType.CSharpName}' is more general than '{tE.CSharpName}'. Since CanBeExtended is false, types in the union must be unrelated." );
                                        success = false;
                                    }
                                }
                                else if( tE.IsWritableType( oneType ) )
                                {
                                    if( prop.UnionTypeDefinition.CanBeExtended )
                                    {
                                        monitor.Warn( $"{prop}: UnionType '{tE.CSharpName}' is more general than '{oneType.CSharpName}'. Removing the second one." );
                                        types.RemoveAt( i-- );
                                    }
                                    else
                                    {
                                        monitor.Error( $"{prop}: Ambiguous UnionType '{tE.CSharpName}' is more general than '{oneType.CSharpName}'. Since CanBeExtended is false, types in the union must be unrelated." );
                                        success = false;
                                    }
                                }
                            }
                            if( success && oneTypeToAdd != null ) types.Add( oneType );
                        }
                    }
                }
                if( success )
                {
                    // Types must be in a deterministic order for the PocoType.KeyUnionTypes to be correct.
                    // Before sorting, take the first type in the "visible" order that can handle a default value.
                    var tDef = types.FirstOrDefault( t => !t.DefaultValueInfo.IsDisallowed );
                    types.Sort( ( t1, t2 ) => StringComparer.Ordinal.Compare( t1.CSharpName, t2.CSharpName ) );
                    var unionType = _system.RegisterUnionType( monitor, types );
                    Debug.Assert( unionType != null && !unionType.IsNullable );

                    // Handle default value now.
                    if( _defaultValue != null )
                    {
                        if( _defaultValue.SimpleValue != null
                            && !unionType.AllowedTypes.Any( t => t.Type.IsAssignableFrom( _defaultValue.SimpleValue.GetType() ) ) )
                        {
                            string sTypes = unionType.AllowedTypes.Select( t => t.CSharpName ).Concatenate( "', '" );
                            monitor.Error( $"Invalid DefaultValue attribute on {prop}: default value {_defaultValue} is not compatible with any of the union type '{sTypes}'." );
                            return null;
                        }
                        // We have a default value (even if it is null - the fact that the property is not nullable in this case has been done above)
                        // that is compatible with the union type. This is fine.
                    }
                    else
                    {
                        // There's no default value. We must try to synthesize one from the first type that is "defaultable" since we need
                        // an object instance for the field's default value if the property is not nullable.
                        if( !unionType.IsNullable )
                        {
                            if( tDef == null )
                            {
                                monitor.Error( $"Unable to resolve a default value for non nullable {prop}." );
                                return null;
                            }
                            if( tDef.DefaultValueInfo.DefaultValue != null )
                            {
                                // The type itself has a valid field default value.
                                _defaultValue = (FieldDefaultValue)tDef.DefaultValueInfo.DefaultValue;
                            }
                            else
                            {
                                _defaultValue = FieldDefaultValue.CreateFromDefaultValue( monitor, _system, tDef.Type );
                                if( _defaultValue == null ) return null;
                            }
                        }
                    }
                    return isNullable ? unionType.Nullable : unionType;
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
                Debug.Assert( _props != null );
                List<(IExtPropertyInfo P, IPocoType T)>? abstractReadOnlyToCheck = null;
                foreach( var p in _props.DeclaredProperties )
                {
                    if( !Add( monitor, p, ref abstractReadOnlyToCheck ) )
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
                if( abstractReadOnlyToCheck != null )
                {
                    if( _bestReg == null )
                    {
                        // Gets the first type that satisfies all the declared type.
                        (_bestProperty, _bestReg) = abstractReadOnlyToCheck.FirstOrDefault( c => abstractReadOnlyToCheck.All( a => c.T.IsReadableType( a.T ) ) );
                        if( _bestReg == null )
                        {
                            var types = _props.DeclaredProperties.Select( p => p.TypeCSharpName ).Concatenate( Environment.NewLine );
                            monitor.Error( $"Failed to find a writable property for read only {_props} types:{Environment.NewLine}{types}" );
                            return false;
                        }
                    }
                    else
                    {
                        foreach( var (prop, type) in abstractReadOnlyToCheck )
                        {
                            if( !CheckAbstractReadOnly( monitor, prop, type ) )
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }

            bool Add( IActivityMonitor monitor, IExtPropertyInfo p, ref List<(IExtPropertyInfo, IPocoType)>? abstractReadOnlyToCheck )
            {
                Debug.Assert( _props != null );
                var reg = _system.RegisterPocoField( monitor, p );
                if( reg == null ) return false;

                // Check record constraints:
                // - In a Poco field, records must be "Read Only Compliant".
                // - The only way to expose a record is through a ref property get:
                //   - No ref and { get; }: There is no point to allow records to be Abstract Read Only properties for 2 reasons:
                //      - the type is completely defined, there is no possible "specialization".
                //      - the "ref" can easily be forgotten by the developper, preventing the easy update of the value.
                //   - A { get; set; }: This could be allowed but this would be error prone since some record will be by ref and
                //     other by value. There is no point to allow this.
                if( reg is IRecordPocoType record )
                {
                    // This is enough to test for "ref { get; }". ("ref { get; set; }" is invalid and { get; } has a !IsByRef type.
                    if( !p.Type.IsByRef )
                    {
                        monitor.Error( $"Property '{p.DeclaringType:N}.{p.Name}' must be a ref property: 'ref {reg.CSharpName} {p.Name} {{ get; }}'." );
                        return false;
                    }
                    if( !record.IsReadOnlyCompliant )
                    {
                        var b = new StringBuilder();
                        DumpReadOnlyCompliant( b, record, 1 );
                        monitor.Error( $"Invalid mutable reference types in '{p.DeclaringType:N}.{p.Name}':{b}" );
                        return false;
                    }
                }

                if( p.PropertyInfo.CanWrite || p.Type.IsByRef )
                {
                    _fieldAccessKind = p.Type.IsByRef ? PocoFieldAccessKind.IsByRef : PocoFieldAccessKind.HasSetter;

                    if( !AddWritable( monitor, p, reg ) )
                    {
                        return false;
                    }
                    // On success, always check that a collection must not
                    // have a setter and that any other type must be a regular property.
                    Debug.Assert( _bestReg != null );
                    Debug.Assert( _bestReg is not IRecordPocoType || _bestReg.Type.IsValueType, "IRecordPocoType => ValueType." );
                    if( _bestReg is IRecordPocoType )
                    {
                        Throw.DebugAssert( "Already checked.", p.Type.IsByRef );
                    }
                    else
                    {
                        if( _bestReg is ICollectionPocoType )
                        {
                            if( _bestReg.Kind != PocoTypeKind.Array )
                            {
                                monitor.Error( $"Property '{p.DeclaringType:N}.{p.Name}' is a {_bestReg.Kind}, it must be a read only property: '{_bestReg.CSharpName} {p.Name} {{ get; }}'." );
                                return false;
                            }
                        }
                        if( p.Type.IsByRef )
                        {
                            monitor.Error( $"Property '{p.DeclaringType:N}.{p.Name}' is not a record, it must not be a 'ref' property, only a regular property with a setter: '{_bestReg.CSharpName} {p.Name} {{ get; set; }}'." );
                            return false;
                        }
                    }
                    return true;
                }
                // The property is a simple { get; } and not a record. It may be an "Abstract read only Property"
                // or a real property that must be allocated.

                Throw.DebugAssert( reg.Kind is not PocoTypeKind.AnonymousRecord or PocoTypeKind.Record );

                // Secondary and PrimaryPoco: real property.
                var isSecondaryPoco = reg.Kind is PocoTypeKind.SecondaryPoco;
                if( isSecondaryPoco )
                {
                    Throw.DebugAssert( reg.ObliviousType is IPrimaryPocoType );
                    reg = reg.ObliviousType;
                }
                if( isSecondaryPoco || reg.Kind == PocoTypeKind.PrimaryPoco )
                {
                    return AddWritable( monitor, p, reg );
                }
                // IList, ISet or IDictionary: real property.
                if( reg.Kind is PocoTypeKind.List or PocoTypeKind.HashSet or PocoTypeKind.Dictionary
                    && !((ICollectionPocoType)reg).IsAbstractReadOnly )
                {
                    _fieldAccessKind = PocoFieldAccessKind.MutableCollection;
                    return AddWritable( monitor, p, reg );
                }

                // An "Abstract Read Only property".
                Throw.DebugAssert( reg.Kind is PocoTypeKind.Any
                                               or PocoTypeKind.Basic
                                               or PocoTypeKind.Enum
                                               or PocoTypeKind.Array
                                               or PocoTypeKind.AbstractPoco
                                               or PocoTypeKind.UnionType
                                    || (reg is ICollectionPocoType c && c.IsAbstractReadOnly) );
                if( _bestReg != null )
                {
                    if( !CheckAbstractReadOnly( monitor, p, reg ) )
                    {
                        return false;
                    }
                }
                else
                {
                    abstractReadOnlyToCheck ??= new List<(IExtPropertyInfo, IPocoType)>();
                    abstractReadOnlyToCheck.Add( (p, reg) );
                }
                return true;
            }

            static void DumpReadOnlyCompliant( StringBuilder b, IRecordPocoType record, int depth )
            {
                Throw.DebugAssert( !record.IsReadOnlyCompliant );
                foreach( var f in record.Fields )
                {
                    if( MemberContext.IsReadOnlyCompliant( f ) ) continue;
                    if( f.Type is IRecordPocoType r )
                    {
                        b.AppendLine().Append( ' ', depth * 2 ).Append( "in '" ).Append( f.Type.CSharpName ).Append( ' ' ).Append( f.Name ).Append( "':" );
                        DumpReadOnlyCompliant( b, r, depth + 1 );
                    }
                    else
                    {
                        b.AppendLine().Append( ' ', depth * 2 ).Append( f.Type.CSharpName ).Append( ' ' ).Append( f.Name );
                    }
                }
            }

            bool AddWritable( IActivityMonitor monitor, IExtPropertyInfo p, IPocoType reg )
            {
                if( _bestReg != null )
                {
                    Throw.DebugAssert( _bestProperty != null );
                    if( _bestReg.IsSamePocoType( reg ) )
                    {
                        return true;
                    }
                    monitor.Error( $"{p}: Type must be '{_bestReg.CSharpName}' since '{_bestProperty.DeclaringType!:N}.{_bestProperty.Name}' defines it." );
                    return false;
                }
                _bestProperty = p;
                _bestReg = reg;
                return true;
            }

            bool CheckAbstractReadOnly( IActivityMonitor monitor, IExtPropertyInfo p, IPocoType type )
            {
                Throw.DebugAssert( _bestReg != null && _bestProperty != null );
                Throw.DebugAssert( !p.PropertyInfo.PropertyType.IsByRef && !p.PropertyInfo.CanWrite );
                if( !_bestReg.IsReadableType( type ) )
                {
                    monitor.OpenError( $"Read only {p} is not compatible with {_bestReg}." );
                    return false;
                }
                return true;
            }
        }
    }
}
