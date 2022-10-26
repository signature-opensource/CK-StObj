using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Setup
{


    partial class PocoType : IPocoType
    {
        AnnotationSetImpl _annotations;
        readonly IPocoType _nullable;

        class NullReferenceType : IPocoType
        {
            AnnotationSetImpl _annotations;

            public NullReferenceType( IPocoType notNullable )
            {
                Debug.Assert( !notNullable.Type.IsValueType );
                NonNullable = notNullable;
                CSharpName = notNullable.CSharpName + '?';
            }

            public int Index => NonNullable.Index + 1;

            public bool IsNullable => true;

            public string CSharpName { get; }

            public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

            public Type Type => NonNullable.Type;

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable { get; }

            public bool IsReadableType( Type type ) => NonNullable.IsReadableType( type );

            public bool IsWritableType( Type type ) => NonNullable.IsWritableType( type );

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        }

        class NullValueType : IPocoType
        {
            AnnotationSetImpl _annotations;

            public NullValueType( IPocoType notNullable, Type type )
            {
                Debug.Assert( notNullable.Type.IsValueType
                              && notNullable.Type != type
                              && !notNullable.Type.IsAssignableFrom( type )
                              && type.IsAssignableFrom( notNullable.Type ) );

                NonNullable = notNullable;
                Type = type;
                CSharpName = notNullable.CSharpName + '?';
            }

            public int Index => NonNullable.Index + 1;

            public bool IsNullable => true;

            public string CSharpName { get; }

            public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

            public Type Type { get; }

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable { get; }

            public bool IsReadableType( Type type ) => type == typeof( object ) || type == Type || type == NonNullable.Type;

            public bool IsWritableType( Type type ) => type == Type;

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
            Debug.Assert( !notNullable.IsValueType || System.Nullable.GetUnderlyingType( notNullable ) == null );
            Debug.Assert( !csharpName.EndsWith( '?' ) );
            // We register in the AllTypes list only: key for cache is much more complex
            // and is managed externally.
            Index = s.AllTypes.Count;
            Type = notNullable;
            CSharpName = csharpName;
            Kind = kind;
            _nullable = nullFactory( this );
            s.AddNew( this );
        }

        public int Index { get; }

        public Type Type { get; }

        public PocoTypeKind Kind { get; }

        public bool IsNullable => false;

        public string CSharpName { get; }

        /// <summary>
        /// All Basic types are allowed (DateTime and string are BasicTypeWithDefaultValue that
        /// overrides this).
        /// The only case where we disallow is object and AbstractIPoco.
        /// </summary>
        public virtual DefaultValueInfo DefaultValueInfo
        {
            get
            {
                Debug.Assert( Kind == PocoTypeKind.Any
                              || (Kind == PocoTypeKind.Basic && !(Type == typeof(string) || Type == typeof(DateTime)))
                              || Kind == PocoTypeKind.AbstractIPoco, "All other PocoType override this." );

                return Kind == PocoTypeKind.Basic ? DefaultValueInfo.Allowed : DefaultValueInfo.Disallowed;
            }
        }
        
        public IPocoType Nullable => _nullable;

        public IPocoType NonNullable => this;

        public virtual bool IsWritableType( Type type ) => Type.IsAssignableFrom( type );

        public virtual bool IsReadableType( Type type ) => type.IsAssignableFrom( Type );

        public override string ToString() => $"[{Kind}]{CSharpName}";

        public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

        public object? Annotation( Type type ) => _annotations.Annotation( type );

        public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

        public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

        public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

        public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

        public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

    }

}
