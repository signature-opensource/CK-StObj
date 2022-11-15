using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

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

            public bool IsPurelyGeneratedType => NonNullable.IsPurelyGeneratedType;

            public bool IsAbstract => NonNullable.IsAbstract;

            public bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false )
            {
                if( !ignoreRootTypeIsNullable && !type.IsNullable ) return false;
                return NonNullable.IsSameType( type, true );
            }

            public bool IsReadableType( IExtNullabilityInfo type )
            {
                return NonNullable.IsReadableType( type );
            }

            public bool IsWritableType( IExtNullabilityInfo type )
            {
                return NonNullable.IsWritableType( type.ToNonNullable() );
            }

            public override string ToString() => $"[{Kind}]{CSharpName}";

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

            public bool IsPurelyGeneratedType => NonNullable.IsPurelyGeneratedType;

            public bool IsAbstract => NonNullable.IsAbstract;

            public bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false )
            {
                if( !ignoreRootTypeIsNullable && !type.IsNullable ) return false;
                return NonNullable.IsSameType( type, true );
            }

            public bool IsReadableType( IExtNullabilityInfo type ) => type.Type == typeof( object ) || type.Type == Type || type.Type == NonNullable.Type;

            public bool IsWritableType( IExtNullabilityInfo type ) => type.Type == Type;

            public override string ToString() => $"[{Kind}]{CSharpName}";

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

        public bool IsPurelyGeneratedType => Type == IDynamicAssembly.PurelyGeneratedType;

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

        public virtual bool IsAbstract => Kind == PocoTypeKind.Any || Kind == PocoTypeKind.AbstractIPoco;

        public virtual bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false )
        {
            Debug.Assert( !IsNullable, "Null implementations override this." );
            if( !ignoreRootTypeIsNullable && type.IsNullable ) return false;
            Debug.Assert( !IsPurelyGeneratedType, "Collections override this." );
            Debug.Assert( Kind != PocoTypeKind.IPoco, "PrimaryPocoType override this." );
            return Type == type.Type;
        }

        public virtual bool IsWritableType( IExtNullabilityInfo type )
        {
            Debug.Assert( !IsNullable, "Null implementations override this." );
            return !type.IsNullable && Type.IsAssignableFrom( type.Type );
        }

        /// <summary>
        /// Simply calls <c>type.Type.IsAssignableFrom( Type )</c> at this level.
        /// </summary>
        public virtual bool IsReadableType( IExtNullabilityInfo type )
        {
            Debug.Assert( !IsNullable, "Null implementations override this." );
            Debug.Assert( typeof( int? ).IsAssignableFrom( typeof( int ) ), "Value Type nullable <: not nullable is handled by .Net." );
            return type.Type.IsAssignableFrom( Type );
        }

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
