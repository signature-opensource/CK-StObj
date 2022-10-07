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
            readonly NullableTypeTree _nullableTypeTree;

            public PropertyInfo PropertyInfo { get; }

            public bool IsReadOnly => false;

            public int Index { get; }

            public string PropertyName => PropertyInfo.Name;

            public Type PropertyType => PropertyInfo.PropertyType;

            public NullableTypeTree PropertyNullableTypeTree => _nullableTypeTree;

            public PocoClassInfo? PocoClassType { get; set; }

            IPocoClassInfo? IPocoBasePropertyInfo.PocoClassType => PocoClassType;

            public IPocoRootInfo? PocoType { get; set; }

            public PocoPropertyKind PocoPropertyKind { get; }

            public PocoClassPropertyInfo( PropertyInfo p, int index )
            {
                Debug.Assert( p != null );
                PropertyInfo = p;
                Index = index;
                _nullableTypeTree = p.GetNullableTypeTree();
                PocoPropertyKind = PocoSupportResultExtension.GetPocoPropertyKind( _nullableTypeTree, out var _ );
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
