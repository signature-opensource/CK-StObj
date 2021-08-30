using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    partial class PocoRegistrar
    {
        class PocoLikeInfo : IPocoLikeInfo
        {
            AnnotationSetImpl _annotations;

            public Type PocoType { get; }

            public string Name { get; }

            public IReadOnlyList<string> PreviousNames { get; }

            public bool IsDefaultNewable { get; }

            List<IPocoLikeInfo>? _specializations;
            IReadOnlyList<IPocoLikeInfo> IPocoLikeInfo.Specializations => _specializations ?? (IReadOnlyList<IPocoLikeInfo>)Array.Empty<IPocoLikeInfo>();

            public readonly Dictionary<string, PocoLikePropertyInfo> Properties;
            IReadOnlyDictionary<string, IPocoLikePropertyInfo> _exposedProperties;
            IReadOnlyDictionary<string, IPocoLikePropertyInfo> IPocoLikeInfo.Properties => _exposedProperties;

            public IReadOnlyList<PocoLikePropertyInfo> PropertyList { get; }
            IReadOnlyList<IPocoLikePropertyInfo> IPocoLikeInfo.PropertyList => PropertyList;

            public PocoLikeInfo( Type t,
                                 string name,
                                 string[] previousNames,
                                 bool isDefaultNewable,
                                 Dictionary<string, PocoLikePropertyInfo> properties,
                                 IReadOnlyList<PocoLikePropertyInfo> propertyList )
            {
                PocoType = t;
                Name = name;
                PreviousNames = previousNames;
                IsDefaultNewable = isDefaultNewable;
                Properties = properties;
                _exposedProperties = Properties.AsIReadOnlyDictionary<string, PocoLikePropertyInfo, IPocoLikePropertyInfo>();
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
