using CK.CodeGen;
using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

#nullable enable

namespace CK.Setup
{
    partial class PocoRegisterer
    {
        class PocoPropertyInfo : IPocoPropertyInfo
        {
            NullableTypeTree _nullableTypeTree;
            AnnotationSetImpl _annotations;
            List<NullableTypeTree>? _unionTypes;
            bool _unionTypesCanBeExtended;

            public bool IsReadOnly { get; set; }

            public bool HasDefaultValue { get; set; }

            public object? DefaultValue { get; set; }

            public string? DefaultValueSource { get; set; }

            public int Index { get; set; }

            public NullabilityTypeInfo PropertyNullabilityInfo { get; set; }

            public bool IsNullable => PropertyNullabilityInfo.Kind.IsNullable();

            public NullableTypeTree PropertyNullableTypeTree => _nullableTypeTree.Kind == NullabilityTypeKind.Unknown
                                                                    ? (_nullableTypeTree = PropertyType.GetNullableTypeTree( PropertyNullabilityInfo ))
                                                                    : _nullableTypeTree;

            public Type PropertyType => DeclaredProperties[0].PropertyType;

            public IEnumerable<NullableTypeTree> PropertyUnionTypes => _unionTypes != null
                                                                            ? _unionTypes
                                                                            : Enumerable.Empty<NullableTypeTree>();

            public string PropertyName => DeclaredProperties[0].Name;

            public List<PropertyInfo> DeclaredProperties { get; }

            IReadOnlyList<PropertyInfo> IPocoPropertyInfo.DeclaredProperties => DeclaredProperties;

            public PocoPropertyInfo( PropertyInfo first, int index )
            {
                DeclaredProperties = new List<PropertyInfo>() { first };
                Index = index;
            }

            /// <summary>
            /// Merges the types. Returns false if and only if CanBeExtended conflicts.
            /// </summary>
            public bool AddUnionPropertyTypes( IActivityMonitor monitor, List<NullableTypeTree> types, bool typesCanBeExtended )
            {
                Debug.Assert( types.Count > 0 );
                if( _unionTypes == null )
                {
                    _unionTypes = types;
                    _unionTypesCanBeExtended = typesCanBeExtended;
                    return true;
                }
                bool success = true;
                List<NullableTypeTree>? extended = null;
                foreach( var t in types )
                {
                    if( !_unionTypes.Contains( t ) )
                    {
                        if( !_unionTypesCanBeExtended )
                        {
                            monitor.Error( $"Existing union type cannot be extended. Type '{t}' is a new one (existing types are: '{_unionTypes.Select( t => t.ToString() ).Concatenate( "', '" )}')." );
                            success = false;
                        }
                        else
                        {
                            if( extended == null ) extended = _unionTypes as List<NullableTypeTree> ?? new List<NullableTypeTree>( _unionTypes );
                            extended.Add( t );
                        }
                    }
                }
                if( success )
                {
                    if( !typesCanBeExtended )
                    {
                        _unionTypesCanBeExtended = false;
                        foreach( var t in _unionTypes )
                        {
                            if( !types.Contains( t ) )
                            {
                                monitor.Error( $"Current union type definition cannot be extended. Existing union type defines type '{t}' that is not defined by these union types '{types.Select( t => t.ToString() ).Concatenate( "', '" )}')." );
                                success = false;
                            }
                        }
                    }
                    if( success && extended != null )
                    {
                        _unionTypes = extended;
                    }
                }
                return success;
            }

            public bool OptimizeUnionTypes( IActivityMonitor monitor )
            {
                Debug.Assert( _unionTypes != null && _unionTypes.Any() && _unionTypes.All( t => !t.Kind.IsNullable() ) );
                for( int i = 0; i < _unionTypes.Count; ++i )
                {
                    var tN = _unionTypes[i];
                    var t = tN.Type;
                    for( int j = i+1; j < _unionTypes.Count; ++j )
                    {
                        var tJN = _unionTypes[j];
                        var tJ = tJN.Type;
                        if( tJ == t )
                        {
                            monitor.Warn( $"{ToString()} UnionType '{t.ToCSharpName()}' duplicated. Removing one." );
                            _unionTypes.RemoveAt( j-- );
                        }
                        else if( tJ.IsAssignableFrom( t ) )
                        {
                            monitor.Warn( $"{ToString()} UnionType '{tJ.ToCSharpName()}' is assignable from (is more general than) '{t.ToCSharpName()}'. Removing the second one." );
                            _unionTypes.RemoveAt( i-- );
                            break;
                        }
                        else if( t.IsAssignableFrom( tJ ) )
                        {
                            monitor.Warn( $"{ToString()} UnionType '{t.ToCSharpName()}' is assignable from (is more general than) '{tJ.ToCSharpName()}'. Removing the second one." );
                            _unionTypes.RemoveAt( j-- );
                        }
                    }
                }
                if( _unionTypes.Count == 1 )
                {
                    monitor.Warn( $"{ToString()}: UnionType contains only one type. This is weird (but ignored)." );
                }
                return true;
            }

            public override string ToString() => $"Property '{PropertyName}' of type '{PropertyType.ToCSharpName()}' on Poco interfaces: '{DeclaredProperties.Select( p => p.DeclaringType!.GetExternalNameOrFullName() ).Concatenate( "', '" )}'.";

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
