using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using static CK.Setup.IPocoType;

namespace CK.Setup
{
    partial class PocoType : IPocoType
    {
        readonly IPocoType _nullable;
        readonly Type _type;
        readonly string _csharpName;
        ITypeRef? _firstRef;
        AnnotationSetImpl _annotations;
        readonly int _index;
        readonly PocoTypeKind _kind;
        bool _isExchangeable;

        class NullReferenceType : IPocoType
        {
            readonly IPocoType _nonNullable;
            readonly string _csharpName;
            AnnotationSetImpl _annotations;

            public NullReferenceType( IPocoType notNullable )
            {
                Debug.Assert( !notNullable.Type.IsValueType );
                _nonNullable = notNullable;
                _csharpName = notNullable.CSharpName + '?';
            }

            public int Index => NonNullable.Index + 1;

            public bool IsNullable => true;

            public string CSharpName => _csharpName;

            public string ImplTypeName => NonNullable.ImplTypeName;

            public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

            public Type Type => NonNullable.Type;

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable => _nonNullable;

            public bool IsPurelyGeneratedType => NonNullable.IsPurelyGeneratedType;

            public bool IsAbstract => NonNullable.IsAbstract;

            public ITypeRef? FirstBackReference => NonNullable.FirstBackReference;

            public bool IsExchangeable => NonNullable.IsExchangeable;

            public IPocoType ImplNominalType => NonNullable.ImplNominalType;

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
            readonly IPocoType _nonNullable;
            readonly string _csharpName;
            readonly Type _type;
            AnnotationSetImpl _annotations;

            public NullValueType( IPocoType notNullable, Type type )
            {
                Debug.Assert( notNullable.Type.IsValueType
                              && notNullable.Type != type
                              && !notNullable.Type.IsAssignableFrom( type )
                              && type.IsAssignableFrom( notNullable.Type ) );

                _nonNullable = notNullable;
                _type = type;
                _csharpName = notNullable.CSharpName + '?';
            }

            public int Index => NonNullable.Index + 1;

            public bool IsNullable => true;

            public string CSharpName => _csharpName;

            public string ImplTypeName => _csharpName;

            public IPocoType ImplNominalType => this;

            public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

            public Type Type => _type;

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable => _nonNullable;

            public bool IsPurelyGeneratedType => NonNullable.IsPurelyGeneratedType;

            public bool IsAbstract => NonNullable.IsAbstract;

            public ITypeRef? FirstBackReference => NonNullable.FirstBackReference;

            public bool IsExchangeable => NonNullable.IsExchangeable;

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
                            Func<PocoType, IPocoType> nullFactory )
        {
            Debug.Assert( !notNullable.IsValueType || System.Nullable.GetUnderlyingType( notNullable ) == null );
            Debug.Assert( !csharpName.EndsWith( '?' ) );
            // We register in the AllTypes list only: key for cache is much more complex
            // and is managed externally.
            _index = s.AllTypes.Count;
            _type = notNullable;
            _csharpName = csharpName;
            _kind = kind;
            _isExchangeable = true;
            _nullable = nullFactory( this );
            s.AddNew( this );
        }

        public int Index => _index;

        public Type Type => _type;

        public PocoTypeKind Kind => _kind;

        public bool IsNullable => false;

        public string CSharpName => _csharpName;

        /// <summary>
        /// This works for basic types and for record.
        /// For collection, this can be the regular type, an adapter or a
        /// purely generated type name.
        /// </summary>
        public virtual string ImplTypeName => _csharpName;

        /// <summary>
        /// By default this: this works for basic types and for record.
        /// </summary>
        public virtual IPocoType ImplNominalType => this;

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
                              || (Kind == PocoTypeKind.Basic && !(Type == typeof( string ) || Type == typeof( DateTime )))
                              || Kind == PocoTypeKind.AbstractIPoco, "All other PocoType override this." );

                return Kind == PocoTypeKind.Basic ? DefaultValueInfo.Allowed : DefaultValueInfo.Disallowed;
            }
        }

        public IPocoType Nullable => _nullable;

        public IPocoType NonNullable => this;

        public virtual bool IsAbstract => Kind == PocoTypeKind.Any || Kind == PocoTypeKind.AbstractIPoco;

        public virtual bool IsExchangeable => _isExchangeable;

        internal void SetNotExchangeable( IActivityMonitor monitor, string reason )
        {
            Debug.Assert( _isExchangeable );
            using( monitor.OpenInfo( $"{ToString()} is not exchangeable: {reason}" ) )
            {
                _isExchangeable = false;
                var r = _firstRef;
                while( r != null )
                {
                    ((PocoType)r.Owner).OnNoMoreExchangeable( monitor, r );
                    r = r.NextRef;
                }
            }
        }

        /// <summary>
        /// By default propagates the Not Exchangeable to this type (this works for Collections).
        /// This must obviously be specialized by types.
        /// </summary>
        protected virtual void OnNoMoreExchangeable( IActivityMonitor monitor, ITypeRef r )
        {
            Debug.Assert( _kind != PocoTypeKind.Any, "Object doesn't track its back references." );
            Debug.Assert( _kind != PocoTypeKind.Basic, "Basic types have no back references." );
            if( _isExchangeable )
            {
                SetNotExchangeable( monitor, $"'{r.Type}' is not exchangeable." );
            }
        }

        public ITypeRef? FirstBackReference => _firstRef;

        internal ITypeRef? AddBackRef( ITypeRef r )
        {
            Debug.Assert( r != null );
            Debug.Assert( r.Type == null || r.Type.Kind != PocoTypeKind.Any,
                "Type may not be set (for recursive named record), but if it is, it can't be the Any type." );
            var f = _firstRef;
            _firstRef = r;
            return f;
        }

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
