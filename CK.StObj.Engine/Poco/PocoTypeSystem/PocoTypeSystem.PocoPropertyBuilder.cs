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
                    && !_finalType.Type.IsAssignableFrom( _defaultValue.Value.GetType() ) )
                {
                    monitor.Error( $"Invalid DefaultValue attribute on {prop}: default value {_defaultValue} is not compatible with type '{_finalType}'." );
                    return null;
                }
                return new PrimaryPocoField( prop, _finalType, isReadOnly, p, _best.PropertyType.IsByRef, _defaultValue );
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
                        _defaultValue = FieldDefaultValue.CreateFromAttribute( monitor, _system._sharedWriter, p );
                        if( _defaultValue != null ) _defaultValueSource = p;
                    }
                    else
                    {
                        Debug.Assert( _defaultValueSource != null );
                        if( !_defaultValue.CheckSameOrNone( monitor, _defaultValueSource, _system._sharedWriter, p ) )
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
                    monitor.Error( $"Property '{p.DeclaringType}.{p.Name}': found type '{t}' that is not a Poco type." );
                    return null;
                }
            }

            bool Add( IActivityMonitor monitor, PropertyInfo p )
            {
                Debug.Assert( _prop != null );
                if( p.CanWrite || p.PropertyType.IsByRef )
                {
                    if( _best == null )
                    {
                        _best = p;
                        _finalType = _system.Register( monitor, p );
                        if( _finalType == null ) return false;
                        foreach( var pRead in _prop.DeclaredProperties )
                        {
                            if( pRead == p ) break;
                            if( !CheckNewReadOnly( monitor, pRead, null ) )
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        if( !CheckNewWritable( monitor, p ) )
                        {
                            return false;
                        }
                    }
                    // On success, always check that a record must be a ref property and that
                    // any other type must be a regular property.
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
                            monitor.Error( $"Invalid nullable '{culprit.Nullable.CSharpName}' in '{p.DeclaringType}.{p.Name}' type. It cannot be nullable since it is not nullable in '{_finalType.CSharpName} {_best.DeclaringType!.ToCSharpName()}.{_best.Name}'." );
                        }
                        else
                        {
                            monitor.Error( $"Invalid non nullable '{culprit.NonNullable.CSharpName}' in '{p.DeclaringType}.{p.Name}' type. It cannot be not null since it is nullable in '{_finalType.CSharpName} {_best.DeclaringType!.ToCSharpName()}.{_best.Name}'." );
                        }
                        return false;
                    }
                }
                monitor.Error( $"Property '{p.DeclaringType!.ToCSharpName()}.{p.Name}': Type must be exactly '{_finalType.CSharpName}' since '{_best.DeclaringType!.ToCSharpName()}.{_best.Name}' defines it." );
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
                    monitor.Error( $"Read only property '{p.DeclaringType}.{p.Name}': Type is not compatible with '{_finalType.CSharpName} {_best.DeclaringType}.{_best.Name}'." );
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
                        monitor.Error( $"Read only property '{p.DeclaringType}.{p.Name}' cannot be nullable since '{_finalType.CSharpName} {_best.DeclaringType}.{_best.Name}' is not." );
                        return false;
                    }
                    Debug.Assert( !culprit.IsNullable );
                    monitor.Error( $"Invalid nullable '{culprit.Nullable.CSharpName}' in '{p.DeclaringType}.{p.Name}' type. It cannot be nullable since it is not nullable in '{_finalType.CSharpName} {_best.DeclaringType}.{_best.Name}'." );
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
