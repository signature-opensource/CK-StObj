using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup;

partial class PocoDirectoryBuilder
{
    class InterfaceInfo : IPocoInterfaceInfo
    {
        public readonly PocoRootInfo Root;
        public Type PocoInterface { get; }
        public Type PocoFactoryInterface { get; }
        public string CSharpName { get; }

        AnnotationSetImpl _annotations;

        IPocoFamilyInfo IPocoInterfaceInfo.Family => Root;

        public InterfaceInfo( PocoRootInfo root, Type pocoInterface, Type pocoFactoryInterface )
        {
            Root = root;
            PocoInterface = pocoInterface;
            PocoFactoryInterface = pocoFactoryInterface;
            CSharpName = pocoInterface.ToCSharpName();
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
