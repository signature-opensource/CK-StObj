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

    partial class PocoDirectoryBuilder
    {
        sealed class PocoPropertyInfo : IPocoPropertyInfo
        {
            AnnotationSetImpl _annotations;

            // Setter is used whenever a previous property actually has a AutoImplementationClaimAttribute
            // to offset the remaining indexes.
            public int Index { get; set; }

            public string Name { get; }

            public List<PropertyInfo> DeclaredProperties { get; }

            IReadOnlyList<PropertyInfo> IPocoPropertyInfo.DeclaredProperties => DeclaredProperties;

            public PocoPropertyInfo( int initialIndex, string name )
            {
                DeclaredProperties = new List<PropertyInfo>();
                Index = initialIndex;
                Name = name;
            }

            /// <summary>
            /// Returns "'Name' on Poco interfaces: 'IPocoOne', 'IPocoTwo'"
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"Property '{Name}' on Poco interfaces: '{DeclaredProperties.Select( p => p.DeclaringType!.GetExternalNameOrCSharpName() ).Concatenate( "', '" )}'";
        
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
