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

            public ITypeRef? FirstBackReference => NonNullable.FirstBackReference;

            public bool IsExchangeable => NonNullable.IsExchangeable;

            /// <summary>
            /// Returning "NonNullable.ObliviousType" always works since we only have to
            /// erase this nullability.
            /// </summary>
            public IPocoType ObliviousType => _nonNullable.ObliviousType;

            public bool IsSamePocoType( IPocoType type ) => PocoType.IsSamePocoType( this, type );

            public bool IsReadableType( IPocoType type )
            {
                // We are on a nullable: if the the type is non nullable, it's over because we
                // cannot read a non nullable from a nullable.
                // Non nullable IsReadableType predicates don't care of the
                // type nullability (a nullable can always be read from it's non nullable): we
                // simply relay the type here.
                return type.IsNullable && NonNullable.IsReadableType( type );
            }

            public bool IsWritableType( IPocoType type )
            {
                // We are on a nullable type. Non nullable IsWritableType predicates rejects
                // nullable type (a nullable cannot be set to a non nullable). We
                // relay the non nullable type here and if it is writable then we, as a nullable
                // type are writable regardless of type.IsNullable.
                return NonNullable.IsWritableType( type.NonNullable );
            }

            public override string ToString() => PocoType.ToString( this, true );

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

            // We can avoid the Primary/SecondaryPoco test because we are on a value type.
            public bool IsSamePocoType( IPocoType type ) => type == this;

            /// <summary>
            /// Returning this works for basic value types, enumerations and named records
            /// but not for anonymous records.
            /// </summary>
            public virtual IPocoType ObliviousType
            {
                get
                {
                    Debug.Assert( Kind == PocoTypeKind.Basic || Kind == PocoTypeKind.Enum || Kind == PocoTypeKind.Record );
                    return this;
                }
            }

            public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

            public Type Type => _type;

            public PocoTypeKind Kind => NonNullable.Kind;

            public IPocoType Nullable => this;

            public IPocoType NonNullable => _nonNullable;

            public bool IsPurelyGeneratedType => NonNullable.IsPurelyGeneratedType;

            public ITypeRef? FirstBackReference => NonNullable.FirstBackReference;

            public bool IsExchangeable => NonNullable.IsExchangeable;

            public bool IsReadableType( IPocoType type )
            {
                // We are on a nullable: if the the type is non nullable, it's over because we
                // cannot read a non nullable from a nullable.
                // Non nullable IsReadableType predicates don't care of the
                // type nullability (a nullable can always be read from it's non nullable): we
                // simply relay the type here.
                return type.IsNullable && NonNullable.IsReadableType( type );
            }

            public bool IsWritableType( IPocoType type )
            {
                // We are on a nullable type. Non nullable IsWritableType predicates rejects
                // nullable type (a nullable cannot be set to a non nullable). We
                // relay the non nullable type here and if it is writable then we, as a nullable
                // type are writable regardless of type.IsNullable.
                return NonNullable.IsWritableType( type.NonNullable );
            }

            public override string ToString() => PocoType.ToString( this, true );

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
                            Func<PocoType, IPocoType> nullFactory,
                            bool isExchangeable = true )
        {
            Debug.Assert( !notNullable.IsValueType || System.Nullable.GetUnderlyingType( notNullable ) == null );
            Debug.Assert( !csharpName.EndsWith( '?' ) );
            // We register in the AllTypes list only: key for cache is much more complex
            // and is managed externally.
            _index = s.AllTypes.Count;
            _type = notNullable;
            _csharpName = csharpName;
            _kind = kind;
            _isExchangeable = isExchangeable;
            _nullable = nullFactory( this );
            s.AddNew( this );
        }

        public int Index => _index;

        public Type Type => _type;

        public bool IsPurelyGeneratedType => _type == IDynamicAssembly.PurelyGeneratedType;

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
        /// Defaults to "this" that works for everything except for:
        /// <list type="bullet">
        ///     <item>
        ///     Anonymous records: their obblivious are value tuples that have no field name and oblivious field types.
        ///     </item>
        ///     <item>
        ///     Collections: their oblivious is the concrete type composed only of oblivious types.
        ///     </item>
        ///     <item>
        ///     Union types: their oblivious is composed only of oblivious types.
        ///     </item>
        ///     <item>
        ///     SecondaryPoco: their oblivious is the PrimaryPoco.
        ///     </item>
        /// </list>
        /// </summary>
        public virtual IPocoType ObliviousType
        {
            get
            {
                Throw.DebugAssert( "These type must override.",
                                    Kind != PocoTypeKind.AnonymousRecord
                                    && Kind != PocoTypeKind.Array
                                    && Kind != PocoTypeKind.List
                                    && Kind != PocoTypeKind.HashSet
                                    && Kind != PocoTypeKind.Dictionary
                                    && Kind != PocoTypeKind.UnionType
                                    && Kind != PocoTypeKind.SecondaryPoco );
                Throw.DebugAssert( Kind == PocoTypeKind.Any
                                   || Kind == PocoTypeKind.Basic
                                   || Kind == PocoTypeKind.Enum
                                   || Kind == PocoTypeKind.Record
                                   || Kind == PocoTypeKind.PrimaryPoco
                                   || Kind == PocoTypeKind.AbstractPoco );
                return this;
            }
        }

        /// <summary>
        /// All Basic types are allowed (DateTime and string are BasicTypeWithDefaultValue that
        /// overrides this).
        /// The only case where we disallow is object (Any), AbstractPoco, abstract readonly list/set/dictionary and UnionType:
        /// union type default is handled at the field level based on the DefaultValue attribute (like the others)
        /// or based on the first type in the variants definition that can provide a default value.
        /// </summary>
        public virtual DefaultValueInfo DefaultValueInfo
        {
            get
            {
                Throw.DebugAssert( "All other PocoType override this.",
                                   Kind == PocoTypeKind.Any
                                   || (Kind == PocoTypeKind.Basic && !(Type == typeof( string ) || Type == typeof( DateTime )))
                                   || Kind == PocoTypeKind.UnionType
                                   || Kind == PocoTypeKind.AbstractPoco
                                   || this is AbstractCollectionType );

                return Kind == PocoTypeKind.Basic ? DefaultValueInfo.Allowed : DefaultValueInfo.Disallowed;
            }
        }

        public IPocoType Nullable => _nullable;

        public IPocoType NonNullable => this;

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

        public bool IsSamePocoType( IPocoType type ) => IsSamePocoType( this, type );

        static bool IsSamePocoType( IPocoType t1, IPocoType t2 )
        {
            return t2.IsNullable == t1.IsNullable
                   &&
                   (t2 == t1
                     || (t2.Kind == PocoTypeKind.SecondaryPoco && t1.Kind == PocoTypeKind.SecondaryPoco
                         && ((ISecondaryPocoType)t2).PrimaryPocoType == ((ISecondaryPocoType)t1).PrimaryPocoType)
                     || (t2.Kind == PocoTypeKind.PrimaryPoco && t1.Kind == PocoTypeKind.SecondaryPoco
                         && t2 == ((ISecondaryPocoType)t1).PrimaryPocoType)
                     || (t2.Kind == PocoTypeKind.SecondaryPoco && t1.Kind == PocoTypeKind.PrimaryPoco
                         && ((ISecondaryPocoType)t2).PrimaryPocoType == (IPrimaryPocoType)t1));
        }

        /// <summary>
        /// Simply calls <c>type.Type.IsAssignableFrom( Type )</c> at this level.
        /// </summary>
        public virtual bool IsReadableType( IPocoType type )
        {
            Throw.DebugAssert( "Null implementations override this.", !IsNullable );
            Throw.DebugAssert( "Value Type nullable <: not nullable is kindly handled by .Net.", typeof( int? ).IsAssignableFrom( typeof( int ) ) );
            return type.Kind == PocoTypeKind.Any || type.NonNullable == this || type.Type.IsAssignableFrom( Type );
        }

        /// <summary>
        /// Checks that <paramref name="type"/> is not nullable and calls <c>Type.IsAssignableFrom( type.Type )</c> at this level.
        /// </summary>
        public virtual bool IsWritableType( IPocoType type )
        {
            Debug.Assert( !IsNullable, "Null implementations override this." );
            return type == this || (!type.IsNullable && Type.IsAssignableFrom( type.Type ));
        }

        static string ToString( IPocoType t, bool withOblivious )
        {
            var r = $"[{t.Kind}]{t.CSharpName}";
            if( t.CSharpName != t.ImplTypeName )
            {
                r += $"/ {t.ImplTypeName}";
            }
            if( withOblivious )
            {
                if( t.IsOblivious ) r += " (IsOblivious)";
                else r += $" (Oblivious: {ToString( t.ObliviousType, false )})";
            }
            return r;
        }

        public override sealed string ToString() => $"[{_kind}]{_csharpName}";

        public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

        public object? Annotation( Type type ) => _annotations.Annotation( type );

        public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

        public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

        public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

        public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

        public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

    }

}
