using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    partial class PocoType : IPocoType
    {
        AnnotationSetImpl _annotations;
        readonly IPocoType _nullable;

        class NullBasicRelay : IPocoType
        {
            AnnotationSetImpl _annotations;

            public NullBasicRelay( IPocoType notNullable )
            {
                NonNullable = notNullable;
                CSharpName = notNullable.CSharpName + '?';
            }

            public int Index => NonNullable.Index + 1;

            public bool IsNullable => true;

            public string CSharpName { get; }

            public Type Type => NonNullable.Type;

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable { get; }

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        }

        class NullBasicWithType : IPocoType
        {
            AnnotationSetImpl _annotations;

            public NullBasicWithType( IPocoType notNullable, Type type )
            {
                NonNullable = notNullable;
                Type = type;
                CSharpName = notNullable.CSharpName + '?';
            }

            public int Index => NonNullable.Index + 1;

            public bool IsNullable => true;

            public string CSharpName { get; }

            public Type Type { get; }

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable { get; }

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        }

        protected PocoType( PocoTypeSystem s,
                            Type notNullable,
                            string csharpName,
                            PocoTypeKind kind,
                            Func<PocoType,IPocoType> nullFactory )
        {
            Index = s.AllTypes.Count;
            Type = notNullable;
            CSharpName = csharpName;
            Kind = kind;
            _nullable = nullFactory( this );
        }

        internal static PocoType CreateBasicRef( PocoTypeSystem s,
                                                 Type type,
                                                 string csharpName,
                                                 PocoTypeKind kind )
        {
            Debug.Assert( !type.IsValueType );
            return new PocoType( s, type, csharpName, kind, t => new NullBasicRelay( t ) );
        }

        internal static PocoType CreateBasicValue( PocoTypeSystem s,
                                                   Type notNullable,
                                                   Type nullable,
                                                   string csharpName )
        {
            Debug.Assert( notNullable.IsValueType );
            return new PocoType( s, notNullable, csharpName, PocoTypeKind.Basic, t => new NullBasicWithType( t, nullable ) );
        }

        public int Index { get; }

        public Type Type { get; }

        public PocoTypeKind Kind { get; }

        public IPocoFamilyInfo? PocoFamily { get; }

        public bool IsNullable => false;

        public string CSharpName { get; }

        public IPocoType Nullable => _nullable;

        public IPocoType NonNullable => this;

        public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

        public object? Annotation( Type type ) => _annotations.Annotation( type );

        public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

        public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

        public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

        public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

        public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

    }

}
