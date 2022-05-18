using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        class PocoClassInfo : IPocoClassInfo
        {
            AnnotationSetImpl _annotations;

            public Type PocoType { get; }

            public string Name { get; }

            public IReadOnlyList<string> PreviousNames { get; }

            public bool IsDefaultNewable { get; }

            List<IPocoClassInfo>? _specializations;
            IReadOnlyList<IPocoClassInfo> IPocoClassInfo.Specializations => _specializations ?? (IReadOnlyList<IPocoClassInfo>)Array.Empty<IPocoClassInfo>();

            public readonly Dictionary<string, PocoClassPropertyInfo> Properties;
            readonly IReadOnlyDictionary<string, IPocoClassPropertyInfo> _exposedProperties;
            IReadOnlyDictionary<string, IPocoClassPropertyInfo> IPocoClassInfo.Properties => _exposedProperties;

            public IReadOnlyList<PocoClassPropertyInfo> PropertyList { get; }
            IReadOnlyList<IPocoClassPropertyInfo> IPocoClassInfo.PropertyList => PropertyList;

            public PocoClassInfo( Type t,
                                 string name,
                                 string[] previousNames,
                                 bool isDefaultNewable,
                                 Dictionary<string, PocoClassPropertyInfo> properties,
                                 IReadOnlyList<PocoClassPropertyInfo> propertyList )
            {
                PocoType = t;
                Name = name;
                PreviousNames = previousNames;
                IsDefaultNewable = isDefaultNewable;
                Properties = properties;
                _exposedProperties = Properties.AsIReadOnlyDictionary<string, PocoClassPropertyInfo, IPocoClassPropertyInfo>();
                PropertyList = propertyList;
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
