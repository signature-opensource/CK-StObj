using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        sealed partial class PocoPropertyInfo : IPocoPropertyInfo
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

            public PocoConstructorAction ConstructorAction { get; private set; }

            public bool IsNullable => _best?.NullableTypeTree.Kind.IsNullable() ?? true;

            /// <summary>
            /// Setting of this property is done once the global result is built.
            /// </summary>
            public PocoRootInfo? PocoType { get; set; }

            IPocoRootInfo? IPocoPropertyInfo.PocoType => PocoType;

            IPocoPropertyDefaultValue? IPocoPropertyInfo.DefaultValue { get; }

            public DefaultValue? DefaultValue { get; private set; }

            // Setter is used whenever a previous property actually has a AutoImplementationClaimAttribute
            // to offset the remaining indexes.
            public int Index { get; set; }

            public IEnumerable<NullableTypeTree> PropertyUnionTypes => _unionTypes?.Types ?? Enumerable.Empty<NullableTypeTree>();

            public List<PocoPropertyImpl> Implementations { get; }

            IReadOnlyList<IPocoPropertyImpl> IPocoPropertyInfo.Implementations => Implementations;

            public PocoPropertyInfo( int initialIndex )
            {
                Implementations = new List<PocoPropertyImpl>();
                Index = initialIndex;
            }

            public PocoPropertyImpl? TryAddProperty( IActivityMonitor monitor, PropertyInfo info, ref PropertyInfo[]? cacheUnionTypesDef )
            {
                Debug.Assert( info.DeclaringType != null );
                var propertyType = info.PropertyType;
                var declaringType = info.DeclaringType;
                var nullTree = propertyType.GetNullableTypeTree();
                var isReadOnly = !info.CanWrite;
                if( !UnionType.TryCreate( monitor, info, ref cacheUnionTypesDef, nullTree.Kind.IsNullable(), out var unionType ) )
                {
                    return null;
                }
                bool isAbstractCollection = false;
                var kind = unionType != null
                            ? PocoPropertyKind.Union
                            : (PocoPropertyKind)PocoSupportResultExtension.GetPocoTypeKind( nullTree, out isAbstractCollection );
                if( kind == PocoPropertyKind.None )
                {
                    monitor.Error( $"Disallowed Poco property type for '{nullTree} {declaringType}.{info.Name}'." );
                    return null;
                }
                var result = new PocoPropertyImpl( this, info, isReadOnly, kind, nullTree, unionType );

                if( isReadOnly && result.UnionTypes?.CanBeExtended == true )
                {
                    monitor.Error( $"Property '{result}' is read only: CanBeExtended cannot be true." );
                    return null;
                }
                if( isAbstractCollection )
                {
                    monitor.Error( $"Property '{nullTree} {result}': abstract collection is not supported. It must be a List<>, HashSet<>, Dictionary<,> or array." );
                    return null;
                }
                if( kind == PocoPropertyKind.StandardCollection )
                {
                    List<string>? errors = null;
                    foreach( var sub in nullTree.GetAllSubTypes( false )
                                                .Select( s =>
                                                {
                                                    var k = PocoSupportResultExtension.GetPocoTypeKind( s, out var isAbstractCollection );
                                                    return (s,k,isAbstractCollection);
                                                } ) )
                    {
                        if( sub.k == PocoTypeKind.None )
                        {
                            errors ??= new List<string>();
                            errors.Add( $"the subordinated type '{sub}' is not an allowed Poco type." );
                        }
                        else if( sub.isAbstractCollection )
                        {
                            errors ??= new List<string>();
                            errors.Add( $"the subordinated collection '{sub}' must be a concrete type (List<>, HashSet<>, Dictionary<,> or array)." );
                        }
                    }

                    return null;
                }
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
                                // an eventually readonly PocoPropertyInfo (no writable) is either
                                // a CtorInstantiated IPoco or standard collection (but not an array).
                                // 
                                // And a nullable readonly PocoPropertyInfo is an error since it makes no sense to
                                // have a readonly forever null property.
                                if( !result.NullableTypeTree.Kind.IsNullable() )
                                {
                                    _best = result;
                                }

                            }
                            else if( !CheckNewReadOnly( monitor, result ) )
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
                                if( !result.IsNullable )
                                {
                                    monitor.Error( $"Invalid union definition for '{result}': a [Union( CanBeExtended = true )] must be nullable." );
                                    return false;
                                }
                                _unionTypes = result.UnionTypes.Clone();
                            }
                            else
                            {
                                _unionTypes = result.UnionTypes;
                            }
                        }
                        // Writable properties are type and nullability invariant: we
                        // can settle the property type.
                        _propertyNullableTypeTree = result.NullableTypeTree;
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
                // Check the property type equality except for IPoco.
                if( _best.PocoPropertyKind != result.PocoPropertyKind
                    || (_best.PocoPropertyKind != PocoPropertyKind.IPoco
                        && _best.Info.PropertyType != result.Info.PropertyType) )
                {
                    monitor.Error( $"{_best} type '{_best.NullableTypeTree}' is not the same as {result} that is '{result.NullableTypeTree}'. Writable properties are type invariants (including nullability)." );
                    return false;
                }
                // Even in the case of IPoco, the nullability must be the same
                // (we skip the root type equality check here).
                if( _best.IsNullable != result.IsNullable
                    || !(_best.NullableTypeTree.SubTypes.SequenceEqual( result.NullableTypeTree.SubTypes ) ) )
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

            bool CheckNewReadOnly( IActivityMonitor monitor, PocoPropertyImpl result )
            {
                Debug.Assert( _best != null && !_best.IsReadOnly && result.IsReadOnly );
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
                // Default interface methods require that the "backing property" exists (is reachable) where the adapter exists.
                // Here we can automatically implement the adapter based on a property that the primary interface is not aware of.
                //
                // So we check nothing here but nullability of the property itself: a read only property is allowed to be nullable
                // even if its writable is not (the opposite is not true).
                if( _best.IsNullable && !result.IsNullable )
                {
                    monitor.Error( $"{_best} property is nullable, the read only property {result} must also be nullable." );
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
                    // The types that we can instantiate in the constructor: IPoco and standard collections.
                    if( PocoPropertyKind == PocoPropertyKind.IPoco )
                    {
                        return HandleAutoInstantiatedPoco( monitor, root, pocoInfo );
                    }
                    if( PocoPropertyKind == PocoPropertyKind.StandardCollection )
                    {
                        return HandleAutoInstantiatedCollection( monitor, root, pocoInfo );
                    }
                    monitor.Error( $"Property {ToString()} cannot be instantiated in the constructor. Only IPoco and List<>, HashSet<> or Dictionary<,> can be." );
                    return false;
                }
                Debug.Assert( !_best.IsReadOnly );
                // A writable exists. All writable are already checked to be the same, except
                // for IPoco where we had to wait for the resolution of the family: we must now
                // find a single IPoco family for all implementations, read only or not.
                // If the property is not nullable, we auto instantiate it.
                if( _best.PocoPropertyKind == PocoPropertyKind.IPoco )
                {
                    if( !IsNullable )
                    {
                        if( !HandleAutoInstantiatedPoco( monitor, root, pocoInfo ) )
                        {
                            return false;
                        }
                        // We are done here because HandleAutoInstantiatedPoco has also checked all
                        // read only property implementations.
                        return true;
                    }
                    // Nullable property: just check the family.
                    var family = root.TryResolveFamily( monitor,
                                                        Implementations.Select( i => (i.Info.PropertyType, !i.IsReadOnly, i.ToString()) ) );
                    if( family == null )
                    {
                        return false;
                    }
                    PocoType = family;
                    // We are done here because TryResolveFamily has also checked all read only property implementations.
                    return true;
                }
                if( _best.PocoPropertyKind == PocoPropertyKind.StandardCollection )
                {
                    if( !IsNullable )
                    {
                        if( !HandleAutoInstantiatedCollection( monitor, root, pocoInfo ) )
                        {
                            return false;
                        }
                    }
                    // Nullable property: we must check the covariance of the read only against the writable.
                    // This is the same as for Basic, Union, ValueTuple, Enum and Any property kind: a CovariantAdapter
                    // must be resolved (be it the Void one).
                }
                // We must validate the read only implementations by finding a CovariantAdpater for each of them.
                foreach( var r in Implementations )
                {
                    if( r.IsReadOnly )
                    {
                        var adpater = TryFindAdapter( monitor, this, r );
                        if( adpater == null )
                        {
                            monitor.Error( $"Unable to create a type adapter from {r} to {_best} for property {this}." );
                            return false;
                        }
                    }
                }
            }

            private bool HandleAutoInstantiatedCollection( IActivityMonitor monitor, Result root, PocoRootInfo pocoInfo )
            {
                // We must resolve a concrete type that satisfies all the property read only types.
                // If stupidities appear in the set of types like for instance that
                // none of them expose a List<...> somewhere but only IReadOnlyList<...> are exposed,
                // we consider them as errors.
                var concrete = InferConcreteType( monitor, Implementations.Select( i => (i.NullableTypeTree, i.ToString()) ) );
                if( concrete == null ) return false;
                ConstructorAction = PocoConstructorAction.Instantiate;
                _propertyNullableTypeTree = concrete.Value;
                return true;
            }

            bool HandleAutoInstantiatedPoco( IActivityMonitor monitor, Result root, PocoRootInfo pocoInfo )
            {
                var family = root.TryResolveFamily( monitor,
                                                    Implementations.Select( i => (i.Info.PropertyType, !i.IsReadOnly, i.ToString()) ) );
                if( family == null ) return false;
                if( family == pocoInfo )
                {
                    monitor.Error( $"Recursive required instantiation detected for {(IsReadOnly ? "read only" : "")}property {ToString()}." );
                    return false;
                }
                PocoType = family;
                ConstructorAction = PocoConstructorAction.Instantiate;
                // Set the generated PocoClass as the non nullable actual type: this gives
                // the type that must be instantiated by the constructor just like for standard collection.
                _propertyNullableTypeTree = new NullableTypeTree( family.PocoClass,
                                                                  NullabilityTypeKind.NonNullableReferenceType,
                                                                  Array.Empty<NullableTypeTree>() );
                return true;
            }

            NullableTypeTree? InferConcreteType( IActivityMonitor monitor, IEnumerable<(NullableTypeTree Type, string RefName)> covariants )
            {
                throw new NotImplementedException();
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
