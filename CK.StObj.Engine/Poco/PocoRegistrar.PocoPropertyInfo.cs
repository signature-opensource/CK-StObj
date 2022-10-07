using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class PocoPropertyInfo : IPocoPropertyInfo
        {
            AnnotationSetImpl _annotations;
            PocoPropertyImpl? _best;
            UnionType? _unionTypes;
            NullableTypeTree _propertyNullableTypeTree;

            public NullableTypeTree PropertyNullableTypeTree => _propertyNullableTypeTree;

            public Type PropertyType => _best?.Info.PropertyType!;

            public string PropertyName => _best?.Name!;

            public bool IsReadOnly => _best?.IsReadOnly ?? true;

            public PocoPropertyKind PocoPropertyKind => _best?.PocoPropertyKind ?? PocoPropertyKind.None;

            /// <summary>
            /// Setting of this property (just like PocoType) is done when the global result is built.
            /// </summary>
            public PocoClassInfo? PocoClassType { get; set; }

            IPocoClassInfo? IPocoBasePropertyInfo.PocoClassType => PocoClassType;

            /// <summary>
            /// Setting of this property (just like PocoClassType) is done when the global result is built.
            /// </summary>
            public PocoRootInfo? PocoType { get; set; }

            IPocoRootInfo? IPocoBasePropertyInfo.PocoType => PocoType;

            IPocoPropertyDefaultValue? IPocoPropertyInfo.DefaultValue { get; }

            public DefaultValue? DefaultValue { get; private set; }

            // Setter is used whenever a previous property actually has a AutoImplementationClaimAttribute
            // to offset the remaining indexes.
            public int Index { get; set; }

            public IEnumerable<NullableTypeTree> PropertyUnionTypes => _unionTypes?.Types ?? Enumerable.Empty<NullableTypeTree>();

            /// <summary>
            /// When a PocoPropertyInfo already exists, the very first property is in this list and PropertyInfo
            /// from the other interfaces are added to this list. See <see cref="IPocoPropertyInfo.DeclaredProperties"/>
            /// </summary>
            public List<PropertyInfo> DeclaredProperties { get; }

            IReadOnlyList<PropertyInfo> IPocoPropertyInfo.DeclaredProperties => DeclaredProperties;

            public List<PocoPropertyImpl> Implementations { get; }

            IReadOnlyList<IPocoPropertyImpl> IPocoPropertyInfo.Implementations => Implementations;

            public PocoPropertyInfo( int initialIndex )
            {
                Implementations = new List<PocoPropertyImpl>();
                DeclaredProperties = new List<PropertyInfo>();
                Index = initialIndex;
            }

            public PocoPropertyImpl? TryAddProperty( IActivityMonitor monitor, PropertyInfo info, ref PropertyInfo[]? cacheUnionTypesDef )
            {
                Debug.Assert( info.DeclaringType != null );
                var propertyType = info.PropertyType;
                var declaringType = info.DeclaringType;
                var nullTree = propertyType.GetNullableTypeTree();
                var isReadOnly = !info.CanWrite;
                // We can bail early on this one.
                if( isReadOnly && propertyType.IsArray )
                {
                    monitor.Error( $"Poco property '{declaringType}.{info.Name}' type is a readonly array but a readonly array doesn't prevent its content to be mutated and is unsafe regarding variance. Use a IReadOnlyList instead to express immutability." );
                    return null;
                }
                if( !UnionType.TryCreate( monitor, info, ref cacheUnionTypesDef, nullTree.Kind.IsNullable(), out var unionType ) )
                {
                    return null;
                }
                var kind = unionType != null
                            ? PocoPropertyKind.Union
                            : PocoSupportResultExtension.GetPocoPropertyKind( nullTree, out var _ );
                var result = new PocoPropertyImpl( this, info, isReadOnly, kind, nullTree, unionType );

                if( !TryHandleDefaultValue( monitor, result ) )
                {
                    return null;
                }
                // Promote the best so far and check type compliance if both are Writable.
                if( !HandleBestSoFar( monitor, isReadOnly, result ) )
                {
                    return null;
                }
                Implementations.Add( result );
                return result;

                bool HandleBestSoFar( IActivityMonitor monitor, bool isReadOnly, PocoPropertyImpl result )
                {
                    if( _best == null )
                    {
                        if( isReadOnly )
                        {
                            _best = result;
                        }    
                        else if( !SetBestFirstWriter( monitor, result ) )
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if( !isReadOnly )
                        {
                            // There is a already a best PocoPropertyImpl.
                            // The first Writable replaces a ReadOnly. 
                            if( _best.IsReadOnly )
                            {
                                if( !SetBestFirstWriter( monitor, result ) )
                                {
                                    return false;
                                }
                            }
                            else if( !CheckNewWritable( monitor, result ) )
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if( _best.IsReadOnly )
                            {
                                // Switch the best so that it is not nullable if possible:
                                // an eventually readonly PocoPropertyInfo (no writable) is:
                                // - A CtorInstantiated IPoco, PocoClass or standard collection (but not an array).
                                // - an error...
                                // And a nullable readonly PocoPropertyInfo is an error since it makes no sense to
                                // have a readonly forever null property.
                                if( !result.NullableTypeTree.Kind.IsNullable() )
                                {
                                    _best = result;
                                }
                            }
                            if( !CheckNewReadOnly( monitor, result ) )
                            {
                                return false;
                            }
                        }
                    }
                    return true;

                    bool SetBestFirstWriter( IActivityMonitor monitor, PocoPropertyImpl result )
                    {
                        _best = result;
                        Debug.Assert( _unionTypes == null );
                        if( result.UnionTypes != null )
                        {
                            Debug.Assert( result.PocoPropertyKind == PocoPropertyKind.Union );
                            if( result.UnionTypes.CanBeExtended )
                            {
                                _unionTypes = result.UnionTypes.Clone();
                            }
                            else
                            {
                                _unionTypes = result.UnionTypes;
                            }
                        }
                        Debug.Assert( Implementations.All( r => r.IsReadOnly ) );
                        foreach( var r in Implementations )
                        {
                            if( !CheckNewReadOnly( monitor, r ) )
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                }

            }

            /// <summary>
            /// Writable properties must be same (type invariance).
            /// Note that if the property type is a IPoco on both, we cannot really conclude yet
            /// this will be checked after the resolution of all roots.
            /// </summary>
            bool CheckNewWritable( IActivityMonitor monitor, PocoPropertyImpl result )
            {
                Debug.Assert( _best != null && !_best.IsReadOnly && !result.IsReadOnly );
                if( !CheckPropertyType( monitor, result, kindOnly: false ) )
                {
                    return false;
                }
                // Even in the case of IPoco, the nullability must be the same.
                if( _best.NullableTypeTree.Kind.IsNullable() != result.NullableTypeTree.Kind.IsNullable()
                    || _best.NullableTypeTree.SubTypes.SequenceEqual( result.NullableTypeTree.SubTypes ) )
                {
                    monitor.Error( $"{_best} type '{_best.NullableTypeTree}' has not the same nullability as {result} that is '{result.NullableTypeTree}'. Writable properties are type invariants (including nullability)." );
                    return false;
                }
                // Union case: they must be equivalent or the existing types must be extended.
                if( result.PocoPropertyKind == PocoPropertyKind.Union )
                {
                    Debug.Assert( result.UnionTypes != null && _unionTypes != null );
                    if( !CheckUnionSameCanBeExtended( monitor, result ) )
                    {
                        return false;
                    }
                    if( _unionTypes.CanBeExtended )
                    {
                        _unionTypes.AddExtended( monitor, _best, result );
                    }
                    else if( !CheckUnionTypeEquality( monitor, result ) )
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>
            /// Check Union and TEMPORARILY property type must exactly match.
            /// </summary>
            bool CheckNewReadOnly( IActivityMonitor monitor, PocoPropertyImpl result )
            {
                Debug.Assert( _best != null && !_best.IsReadOnly && result.IsReadOnly );

                // Read only Union definitions cannot extend an Union!
                if( result.PocoPropertyKind == PocoPropertyKind.Union )
                {
                    Debug.Assert( result.UnionTypes != null );
                    if( result.UnionTypes.CanBeExtended )
                    {
                        monitor.Error( $"{result} is not writable, Union cannot be extended." );
                        return false;
                    }
                    // For the moment, they must be exactly the same as the writable one.
                    // (This may be changed once to allow a kind of covariance.)
                    if( !CheckUnionTypeEquality( monitor, result ) )
                    {
                        return false;
                    }
                }

                // We have the actual type given by (at least one) writable property: this is not a
                // "CtorInstantiated" property. We may test the covariance match of the type here
                // but we miss the IPoco family sets to be able to handle IPoco interface type equivalence.
                // So here, we may just check the kind equality...
                // However we want to be able to play with funny adaptations that may make sense. For instance:
                // - a readonly nullable property with a type that is compatible with an item in a tuple, under the condition
                //   that the item can be unambiguously selected.
                // - The same nullable property can also be satisfied by a Union.
                // A similar adaptation that is for the same kind (ValueTuple): a readonly nullable tuple property can be a subset of the writable one.
                //
                // One may think that all of these adaptations can be handled by default interface methods by the developer, but it's not!
                // Default interface methods require that the "backing field" exists where the adapter exists. Here we automatically
                // implement the adapter based on a property that the primary interface is not aware of.
                //
                // Temporary: type must fully match (just like writable).
                if( !CheckPropertyType( monitor, result, kindOnly: false ) )
                {
                    return false;
                }
                // Even in the case of IPoco, the nullability must be the same.
                if( _best.NullableTypeTree.Kind.IsNullable() != result.NullableTypeTree.Kind.IsNullable()
                    || _best.NullableTypeTree.SubTypes.SequenceEqual( result.NullableTypeTree.SubTypes ) )
                {
                    monitor.Error( $"{_best} type '{_best.NullableTypeTree}' has not the same nullability as {result} that is '{result.NullableTypeTree}'. Readable properties are TEMPORARILY type invariants (including nullability)." );
                    return false;
                }
                return true;
            }

            bool CheckPropertyType( IActivityMonitor monitor, PocoPropertyImpl result, bool kindOnly )
            {
                Debug.Assert( _best != null );
                if( _best.PocoPropertyKind != result.PocoPropertyKind
                    || (!kindOnly
                        && _best.PocoPropertyKind != PocoPropertyKind.IPoco
                        && _best.Info.PropertyType != result.Info.PropertyType ) )
                {
                    monitor.Error( $"{_best} type '{_best.NullableTypeTree}' is not the same as {result} that is '{result.NullableTypeTree}'. Writable properties (and TEMPORARILY readable properties also) are type invariants (including nullability)." );
                    return false;
                }
                return true;
            }

            bool CheckUnionSameCanBeExtended( IActivityMonitor monitor, PocoPropertyImpl result )
            {
                Debug.Assert( _best != null && result.UnionTypes != null && _unionTypes != null );
                if( _unionTypes.CanBeExtended != result.UnionTypes.CanBeExtended )
                {
                    monitor.Error( $"{_best} is a UnionType that can{(_unionTypes.CanBeExtended ? "" : "not")} be extended but {result} can{(result.UnionTypes.CanBeExtended ? "" : "not")} be extended. All property definitions of a IPoco family must agree on this." );
                    return false;
                }
                return true;
            }

            bool CheckUnionTypeEquality( IActivityMonitor monitor, PocoPropertyImpl result )
            {
                Debug.Assert( _best != null && result.UnionTypes != null && _unionTypes != null );
                Debug.Assert( _unionTypes.CanBeExtended == result.UnionTypes.CanBeExtended, "This check has already been done." );
                if( !_unionTypes.HasSameVariantsAs( result.UnionTypes ) )
                {
                    monitor.Error( $"{_best} UnionType cannot be extended and is {_best.UnionTypes} but {result} is {result.UnionTypes}. Variants must be the same." );
                    return false;
                }
                return true;
            }

            bool TryHandleDefaultValue( IActivityMonitor monitor, PocoPropertyImpl impl )
            {
                if( DefaultValue == null )
                {
                    if( !DefaultValue.TryCreate( monitor, impl, out var defaultValue ) )
                    {
                        return false;
                    }
                    DefaultValue = defaultValue;
                    return true;
                }
                return DefaultValue.CheckSameOrNone( monitor, impl );
            }

            internal bool Conclude( IActivityMonitor monitor, Result root, PocoRootInfo pocoInfo )
            {
                Debug.Assert( _best != null );
                if( _best.IsReadOnly )
                {
                    // There is no writable implementation:
                    // It is necessarily a CtorInstantiated property or an error.
                    // First, if all properties are nullable, we consider this an error since it makes
                    // no sense to have an immutable null property value.
                    // A non nullable readonly has been promoted as the best by the initial construction step.
                    Debug.Assert( _best.NullableTypeTree.Kind.IsNullable() == Implementations.All( i => i.NullableTypeTree.Kind.IsNullable() ) );
                    if( _best.NullableTypeTree.Kind.IsNullable() )
                    {
                        monitor.Error( $"Nullable read only property {ToString()}. It makes no sense to expose an immutable null property value." );
                        return false;
                    }
                    // The types that we can instantiate in the constructor: IPoco, PocoClass and standard collections (but not array).
                    if( PocoPropertyKind == PocoPropertyKind.IPoco )
                    {
                        PocoType = root.TryResolveFamily( monitor,
                                                          Implementations.Select( i => (i.NullableTypeTree.Type, i.ToString()) ) ) );
                        if( PocoType == null )
                        {
                            return false;
                        }
                        if( PocoType == pocoInfo )
                        {
                            monitor.Error( $"Recursive instantiation detected for property {ToString()}." );
                            return false;
                        }
                    }
                    else if( PocoPropertyKind == PocoPropertyKind.PocoClass )
                    {
                        PocoClassType = root.PocoClass.TryResolveMostSpecified( monitor, Implementations.Select( i => (i.NullableTypeTree.Type, i.ToString()) ) );
                        if( PocoClassType == null )
                        {
                            return false;
                        }
                    }


                    // Type must be "concrete": only standard collection types
                    // and any other foreign types are PocoClass.

                }
            }

            /// <summary>
            /// Returns "'Name' on Poco interfaces: 'IPocoOne', 'IPocoTwo'"
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                Debug.Assert( _best != null, "Must not be called before the first implementation." );
                return $"'{_best.Name}' on Poco interfaces: '{Implementations.Select( p => p.DeclaringType.GetExternalNameOrFullName() ).Concatenate( "', '" )}'";
            }

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        }
    }
}
