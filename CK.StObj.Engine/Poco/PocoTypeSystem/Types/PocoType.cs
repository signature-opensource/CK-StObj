using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    partial class PocoType : IPocoType
    {
        readonly IPocoType _nullable;
        readonly Type _type;
        readonly string _csharpName;
        IPocoType.ITypeRef? _firstRef;
        AnnotationSetImpl _annotations;
        readonly int _index;
        readonly PocoTypeKind _kind;

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

            public bool ImplementationLess => NonNullable.ImplementationLess;

            public IPocoType Nullable => this;

            public IPocoType NonNullable => _nonNullable;

            public bool IsPurelyGeneratedType => NonNullable.IsPurelyGeneratedType;

            public IPocoType.ITypeRef? FirstBackReference => NonNullable.FirstBackReference;

            /// <summary>
            /// Returning "NonNullable.ObliviousType" always works since we only have to
            /// erase this nullability.
            /// </summary>
            public IPocoType ObliviousType => _nonNullable.ObliviousType;

            public bool IsPolymorphic => NonNullable.IsPolymorphic;

            public bool IsNonNullableFinalType => false;

            public bool IsHashSafe => NonNullable.IsHashSafe;

            public bool IsSamePocoType( IPocoType type ) => PocoType.IsSamePocoType( this, type );

            public bool CanReadFrom( IPocoType type )
            {
                // We are on a nullable: if the the type is non nullable, it's over because we
                // cannot read a non nullable from a nullable.
                // Non nullable CanReadFrom don't care of the
                // type nullability (a nullable can always be read from it's non nullable): we
                // simply relay the type here.
                return type.IsNullable && NonNullable.CanReadFrom( type );
            }

            public bool CanWriteTo( IPocoType type ) => type.CanReadFrom( this );

            public override string ToString() => PocoType.ToString( this );

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

            public bool ImplementationLess => NonNullable.ImplementationLess;

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

            public IPocoType.ITypeRef? FirstBackReference => NonNullable.FirstBackReference;

            public bool IsPolymorphic => NonNullable.IsPolymorphic;

            public bool IsNonNullableFinalType => false;

            public bool IsHashSafe => NonNullable.IsHashSafe;

            public bool CanReadFrom( IPocoType type )
            {
                // We are on a nullable: if the the type is non nullable, it's over because we
                // cannot read a non nullable from a nullable.
                // Non nullable IsReadableType predicates don't care of the
                // type nullability (a nullable can always be read from it's non nullable): we
                // simply relay the type here.
                return type.IsNullable && NonNullable.CanReadFrom( type );
            }

            public bool CanWriteTo( IPocoType type ) => type.CanReadFrom( this );

            public override string ToString() => PocoType.ToString( this );

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

        }

        protected PocoType( PocoTypeSystemBuilder s,
                            Type notNullable,
                            string csharpName,
                            PocoTypeKind kind,
                            Func<PocoType, IPocoType> nullFactory )
        {
            Throw.DebugAssert( !notNullable.IsValueType || System.Nullable.GetUnderlyingType( notNullable ) == null );
            Throw.DebugAssert( !csharpName.EndsWith( '?' ) );
            // We register in the AllTypes list only: key for cache is much more complex
            // and is managed externally.
            _index = s.Count;
            _type = notNullable;
            _csharpName = csharpName;
            _kind = kind;
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
        /// False for basic, record, primary and secondary poco.
        /// Overridden by abstract poco, collections and union types.
        /// </summary>
        public virtual bool ImplementationLess
        {
            get
            {
                Throw.DebugAssert( "These type must override.",
                                    Kind != PocoTypeKind.AbstractPoco
                                    && Kind != PocoTypeKind.Array
                                    && Kind != PocoTypeKind.List
                                    && Kind != PocoTypeKind.HashSet
                                    && Kind != PocoTypeKind.Dictionary
                                    && Kind != PocoTypeKind.UnionType );
                return false;
            }
        }

        /// <summary>
        /// Overridden by <see cref="BasicRefType"/> (true for Extended and NormalizedCultureInfo),
        /// <see cref="RecordNamedType"/> and <see cref="RecordAnonType"/> (true if <see cref="IRecordPocoType.IsReadOnlyCompliant"/>).
        /// </summary>
        public virtual bool IsHashSafe => _kind == PocoTypeKind.Basic;

        internal virtual void SetImplementationLess()
        {
            Throw.DebugAssert( ImplementationLess is true );
            Throw.DebugAssert( "Only these types can become implementation less.",
                                Kind == PocoTypeKind.AbstractPoco
                                || Kind == PocoTypeKind.Array
                                || Kind == PocoTypeKind.List
                                || Kind == PocoTypeKind.HashSet
                                || Kind == PocoTypeKind.Dictionary
                                || Kind == PocoTypeKind.UnionType );
            var r = _firstRef;
            while( r != null )
            {
                if( !r.Owner.ImplementationLess )
                {
                    ((PocoType)r.Owner).OnBackRefImplementationLess( r );
                }
                r = r.NextRef;
            }
        }

        protected virtual void OnBackRefImplementationLess( IPocoType.ITypeRef r )
        {
            Throw.DebugAssert( ImplementationLess is false );
            Throw.DebugAssert( "These type must override.",
                                Kind != PocoTypeKind.AbstractPoco
                                && Kind != PocoTypeKind.Array
                                && Kind != PocoTypeKind.List
                                && Kind != PocoTypeKind.HashSet
                                && Kind != PocoTypeKind.Dictionary
                                && Kind != PocoTypeKind.UnionType );
        }

        /// <summary>
        /// Only <see cref="BasicRefType"/> overrides this: if the reference type has at least one specialization,
        /// it is polymorphic.
        /// </summary>
        public virtual bool IsPolymorphic => _kind is PocoTypeKind.Any or PocoTypeKind.AbstractPoco or PocoTypeKind.UnionType;

        /// <summary>
        /// <see cref="BasicRefType"/> overrides this to check that the actual type is not abstract
        /// (even if currently all basic reference types are concrete).
        /// <see cref="AbstractReadOnlyCollectionType"/> overrides this to always be false.
        /// <see cref="ListOrSetOrArrayType"/> and <see cref="DictionaryType"/> override this with:
        /// !ImplementationLess && (IsOblivious || (IsAbstractCollection && Itemtypes are oblivious && ObliviousType.ImplTypeName != ImplTypeName )).
        /// </summary>
        public virtual bool IsNonNullableFinalType => !ImplementationLess
                                                      && ObliviousType == this
                                                      && _kind is not PocoTypeKind.Any
                                                               and not PocoTypeKind.SecondaryPoco
                                                               and not PocoTypeKind.AbstractPoco
                                                               and not PocoTypeKind.UnionType;

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
        /// overrides this). Concrete collections have their own default (new List, Array.Empty, etc.).
        /// <para>
        /// The only case where we disallow is object (Any), AbstractPoco, abstract readonly list/set/dictionary and UnionType:
        /// union type default is handled at the field level based on the DefaultValue attribute (like the others)
        /// or based on the first type in the variants definition that can provide a default value.
        /// </para>
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
                                   || this is AbstractReadOnlyCollectionType );

                return Kind == PocoTypeKind.Basic ? DefaultValueInfo.Allowed : DefaultValueInfo.Disallowed;
            }
        }

        public IPocoType Nullable => _nullable;

        public IPocoType NonNullable => this;

        public IPocoType.ITypeRef? FirstBackReference => _firstRef;

        internal IPocoType.ITypeRef? AddBackRef( IPocoType.ITypeRef r )
        {
            Debug.Assert( r != null );
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
        public virtual bool CanReadFrom( IPocoType type )
        {
            Throw.DebugAssert( "Null implementations override this.", !IsNullable );
            Throw.DebugAssert( "Value Type nullable <: not nullable is kindly handled by .Net.", typeof( int? ).IsAssignableFrom( typeof( int ) ) );
            return type.Kind == PocoTypeKind.Any || type.NonNullable == this || type.Type.IsAssignableFrom( Type );
        }

        public virtual bool CanWriteTo( IPocoType type ) => type.CanReadFrom( this );

        static string ToString( IPocoType t ) => $"[{t.Kind}]{t.CSharpName}";

        public override sealed string ToString() => ToString( this );

        public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

        public object? Annotation( Type type ) => _annotations.Annotation( type );

        public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

        public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

        public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

        public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

        public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

    }

}
