using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        sealed class PocoClassPropertyInfo : IPocoClassPropertyInfo
        {
            AnnotationSetImpl _annotations;

            public PropertyInfo PropertyInfo { get; }

            public bool IsReadOnly { get; }

            public int Index { get; }

            public string PropertyName => PropertyInfo.Name;

            public Type PropertyType => PropertyInfo.PropertyType;

            public NullableTypeTree PropertyNullableTypeTree { get; }

            public bool HasDefaultValue { get; set; }

            public object? DefaultValue { get; set; }

            public string? DefaultValueSource { get; set; }

            public IPocoClassInfo? PocoClassType { get; set; }

            public IPocoRootInfo? PocoType { get; set; }

            public bool IsStandardCollectionType { get; }

            public bool IsUnionType => false;

            public bool IsBasicPropertyType { get; }

            public PocoClassPropertyInfo( PropertyInfo p, int index )
            {
                Debug.Assert( p != null );
                PropertyInfo = p;
                Index = index;
                PropertyNullableTypeTree = p.GetNullableTypeTree();
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
