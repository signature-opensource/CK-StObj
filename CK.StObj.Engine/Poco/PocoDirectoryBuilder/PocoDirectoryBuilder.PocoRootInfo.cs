using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

#nullable enable

namespace CK.Setup
{
    partial class PocoDirectoryBuilder
    {
        sealed class PocoRootInfo : IPocoFamilyInfo
        {
            AnnotationSetImpl _annotations;

            public Type PocoClass { get; }
            public Type PocoFactoryClass { get; }
            public string Name { get; set; }
            public IReadOnlyList<string> PreviousNames { get; set; }
            public Type? ClosureInterface { get; }
            public bool IsClosedPoco { get; }
            public readonly List<InterfaceInfo> Interfaces;
            public HashSet<Type> OtherInterfaces;

            public readonly Dictionary<string, PocoPropertyInfo> Properties;
            readonly IReadOnlyDictionary<string, IPocoPropertyInfo> _exposedProperties;
            public IReadOnlyList<PocoPropertyInfo> PropertyList { get; }

            IReadOnlyDictionary<string, IPocoPropertyInfo> IPocoFamilyInfo.Properties => _exposedProperties;
            IReadOnlyList<IPocoPropertyInfo> IPocoFamilyInfo.PropertyList => PropertyList;


            IReadOnlyList<IPocoInterfaceInfo> IPocoFamilyInfo.Interfaces => Interfaces;
            IReadOnlyCollection<Type> IPocoFamilyInfo.OtherInterfaces => OtherInterfaces;

            public IReadOnlyList<PropertyInfo> ExternallyImplementedPropertyList { get; }

            public IPocoInterfaceInfo PrimaryInterface => Interfaces[0];

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
            public PocoRootInfo( Type pocoClass,
                                 Type pocoFactoryClass,
                                 bool mustBeClosed,
                                 Type? closureInterface,
                                 HashSet<Type> others,
                                 Dictionary<string, PocoPropertyInfo> properties,
                                 IReadOnlyList<PocoPropertyInfo> propertyList,
                                 IReadOnlyList<PropertyInfo>? externallyImplementedPropertyList )
            {
                PocoClass = pocoClass;
                PocoFactoryClass = pocoFactoryClass;
                ClosureInterface = closureInterface;
                IsClosedPoco = mustBeClosed;
                Interfaces = new List<InterfaceInfo>();
                OtherInterfaces = others;
                Properties = properties;
                _exposedProperties = Properties.AsIReadOnlyDictionary<string, PocoPropertyInfo, IPocoPropertyInfo>();
                PropertyList = propertyList;
                ExternallyImplementedPropertyList = externallyImplementedPropertyList ?? Array.Empty<PropertyInfo>();
            }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

            public override string ToString() => $"Poco: {PrimaryInterface.PocoInterface.FullName} ({Interfaces.Count} interfaces)";

            public bool InitializeNames( IActivityMonitor monitor )
            {
                Type primary = Interfaces[0].PocoInterface;
                if( !primary.GetExternalNames( monitor, out string name, out string[] previousNames ) )
                {
                    return false;
                }
                var others = Interfaces.Where( i => i.PocoInterface != primary
                                                    && i.PocoInterface.GetCustomAttributesData().Any( x => typeof( ExternalNameAttribute ).IsAssignableFrom( x.AttributeType ) ) );
                if( others.Any() )
                {
                    monitor.Error( $"ExternalName attribute appear on '{others.Select( i => i.PocoInterface.ToCSharpName() ).Concatenate( "', '" )}'. Only the primary IPoco interface (i.e. '{primary.ToCSharpName()}') should define the Poco names." );
                    return false;
                }
                Name = name;
                PreviousNames = previousNames;
                return true;
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