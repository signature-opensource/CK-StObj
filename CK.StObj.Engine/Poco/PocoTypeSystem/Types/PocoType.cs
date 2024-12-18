using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup;

/// <summary>
/// Base implementation of IPocoType.
/// </summary>
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

        public bool IsNullable => true;

        public int Index => _nonNullable.Index + 1;

        public string CSharpName => _csharpName;

        public string ImplTypeName => _nonNullable.ImplTypeName;

        public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

        public Type Type => _nonNullable.Type;

        public PocoTypeKind Kind => _nonNullable.Kind;

        public bool ImplementationLess => _nonNullable.ImplementationLess;

        // A null cannot be observed.
        public bool IsSerializedObservable => false;

        public IPocoType Nullable => this;

        public IPocoType NonNullable => _nonNullable;

        public IPocoType.ITypeRef? FirstBackReference => _nonNullable.FirstBackReference;

        public bool IsOblivious => ObliviousType == this;

        /// <summary>
        /// Returning "NonNullable.ObliviousType" by default (that can be this instance: oblivious reference type are always nullable).
        /// </summary>
        public virtual IPocoType ObliviousType => _nonNullable.ObliviousType;

        public bool IsRegular => RegularType == this;

        public virtual IPocoType? RegularType
        {
            get
            {
                Throw.DebugAssert( "Collections override.", this is not ICollectionPocoType );
                return this;
            }
        }


        public bool IsStructuralFinalType => StructuralFinalType == this;

        public bool IsFinalType => FinalType == this;

        /// <summary>
        /// Returning "NonNullable.FinalType": it is the non nullable implementation that handles it,
        /// potentially returning this nullable (final reference type are always nullable).
        /// </summary>
        public IPocoType? StructuralFinalType => _nonNullable.StructuralFinalType;

        public bool IsPolymorphic => _nonNullable.IsPolymorphic;

        public bool IsReadOnlyCompliant => _nonNullable.IsReadOnlyCompliant;

        public IPocoType? FinalType => _nonNullable.FinalType;

        public bool IsSamePocoType( IPocoType type ) => PocoType.IsSamePocoType( this, type );

        public bool IsSubTypeOf( IPocoType type )
        {
            // We are on a nullable: if the the type is non nullable, it's over because we
            // cannot read a non nullable from a nullable.
            // Non nullable CanReadFrom don't care of the
            // type nullability (a nullable can always be read from it's non nullable): we
            // simply relay the type here.
            return type.IsNullable && _nonNullable.IsSubTypeOf( type );
        }

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

        public bool IsNullable => true;

        public int Index => _nonNullable.Index + 1;

        public string CSharpName => _csharpName;

        public string ImplTypeName => _csharpName;

        public bool ImplementationLess
        {
            get
            {
                Throw.DebugAssert( _nonNullable.ImplementationLess is false );
                return false;
            }
        }

        // We can avoid the Primary/SecondaryPoco test because we are on a value type.
        public bool IsSamePocoType( IPocoType type ) => type == this;

        public bool IsOblivious => ObliviousType == this;

        /// <summary>
        /// For basic value types and enumerations.
        /// </summary>
        public virtual IPocoType ObliviousType
        {
            get
            {
                Throw.DebugAssert( "Other types override.", Kind == PocoTypeKind.Basic || Kind == PocoTypeKind.Enum );
                return this;
            }
        }

        public bool IsRegular => RegularType == this;

        public virtual IPocoType? RegularType
        {
            get
            {
                Throw.DebugAssert( "Anonymous records override.", Kind != PocoTypeKind.AnonymousRecord );
                return this;
            }
        }

        // For value type, a final type is always non nullable.
        public bool IsStructuralFinalType => false;
        public bool IsFinalType => false;
        // A null cannot be observed.
        public bool IsSerializedObservable => false;

        public IPocoType? StructuralFinalType => _nonNullable.StructuralFinalType;

        public IPocoType? FinalType => _nonNullable.FinalType;

        public DefaultValueInfo DefaultValueInfo => DefaultValueInfo.Allowed;

        public Type Type => _type;

        public PocoTypeKind Kind => _nonNullable.Kind;

        public IPocoType Nullable => this;

        public IPocoType NonNullable => _nonNullable;

        public IPocoType.ITypeRef? FirstBackReference => _nonNullable.FirstBackReference;

        public bool IsPolymorphic => _nonNullable.IsPolymorphic;

        public bool IsReadOnlyCompliant => _nonNullable.IsReadOnlyCompliant;

        public bool IsSubTypeOf( IPocoType type )
        {
            // We are on a nullable: if the the type is non nullable, it's over because we
            // cannot read a non nullable from a nullable.
            // Non nullable IsReadableType predicates don't care of the
            // type nullability (a nullable can always be read from it's non nullable): we
            // simply relay the type here.
            return type.IsNullable && _nonNullable.IsSubTypeOf( type );
        }

        public override string ToString() => PocoType.ToString( this );

        public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

        public object? Annotation( Type type ) => _annotations.Annotation( type );

        public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

        public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

        public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

        public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

        public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

    }

    private protected PocoType( PocoTypeSystemBuilder s,
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

    /// <inheritdoc/>
    public int Index => _index;

    /// <inheritdoc/>
    public Type Type => _type;

    /// <inheritdoc/>
    public PocoTypeKind Kind => _kind;

    /// <inheritdoc/>
    public bool IsNullable => false;

    /// <inheritdoc/>
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
    /// <see cref="RecordNamedType.IsReadOnlyCompliant"/> and <see cref="RecordAnonType.IsReadOnlyCompliant"/>.
    /// </summary>
    public virtual bool IsReadOnlyCompliant => _kind == PocoTypeKind.Basic;

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

    private protected virtual void OnBackRefImplementationLess( IPocoType.ITypeRef r )
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
    /// <see cref="BasicRefType.IsPolymorphic"/> overrides this (polymorphism for BasicRefType is declarative).
    /// <see cref="AbstractReadOnlyCollectionType"/> overrides this to return true.
    /// </summary>
    public virtual bool IsPolymorphic => _kind is PocoTypeKind.Any or PocoTypeKind.AbstractPoco or PocoTypeKind.UnionType;

    /// <inheritdoc/>
    public bool IsOblivious => ObliviousType == this;

    /// <summary>
    /// This base PocoType implements Any and all the basic value types.
    /// Value types oblivious are themselves: returns this for <see cref="PocoTypeKind.Basic"/> otherwise <see cref="Nullable"/>.
    /// <see cref="IBasicRefPocoType"/> overrides this to correct this "basic" default behavior.
    /// Reference types oblivious is <see cref="Nullable"/>: this implementation (that returns the nullable for them) could work
    /// for the Abstract, Primary and Secondary poco but they override this to provide strongly typed oblivious.
    /// </summary>
    public virtual IPocoType ObliviousType
    {
        get
        {
            Throw.DebugAssert( "All other types must override.",
                               Kind == PocoTypeKind.Any
                               || (Kind == PocoTypeKind.Basic && this is not IBasicRefPocoType) );
            return Kind == PocoTypeKind.Basic ? this : Nullable;
        }
    }

    /// <inheritdoc/>
    public bool IsRegular => RegularType == this;

    /// <inheritdoc/>
    public virtual IPocoType? RegularType
    {
        get
        {
            Throw.DebugAssert( "Anonymous records and collection handle thier Regular type.", Kind != PocoTypeKind.AnonymousRecord && this is not ICollectionPocoType );
            return this;
        }
    }

    /// <inheritdoc/>
    public bool IsFinalType => !ImplementationLess && IsStructuralFinalType;

    /// <inheritdoc/>
    public IPocoType? FinalType => ImplementationLess ? null : StructuralFinalType;

    /// <inheritdoc/>
    public bool IsStructuralFinalType => StructuralFinalType == this;

    /// <summary>
    /// Returns the ObliviousType by default except for <see cref="PocoTypeKind.Any"/> (that is not a final type).
    /// <see cref="BasicRefType"/> overrides this to return null if its Type is abstract.
    /// <see cref="AbstractPocoType"/> and <see cref="AbstractPocoBase"/>, <see cref="AbstractReadOnlyCollectionType"/>
    /// and <see cref="UnionType"/> override this to always return null.
    /// Mutable collections (<see cref="ListOrSetOrArrayType"/> and <see cref="DictionaryType"/>) and <see cref="RecordNamedType"/> are the
    /// only ones to have their own final type.
    /// </summary>
    public virtual IPocoType? StructuralFinalType => _kind != PocoTypeKind.Any ? ObliviousType : null;

    /// <inheritdoc/>
    public bool IsSerializedObservable => IsRegular && (IsFinalType || Nullable.IsFinalType);

    /// <summary>
    /// All Basic types are allowed (DateTime and string are BasicTypeWithDefaultValue that
    /// overrides this). Concrete collections have their own default (new List, Array.Empty, etc.).
    /// <para>
    /// The only case where we disallow is object (Any), AbstractPoco, abstract readonly list/set/dictionary and UnionType:
    /// union type default is handled at the field level based on the DefaultValue attribute (like the others)
    /// or based on the first type in the variants definition that can provide a default value.
    /// </para>
    /// <para>
    /// The <see cref="UserMessage"/>, <see cref="SimpleUserMessage"/> and <see cref="FormattedString"/> value types are special cases as
    /// we don't allow their <c>default</c> value because it is invalid.
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

    /// <inheritdoc/>
    public IPocoType Nullable => _nullable;

    /// <inheritdoc/>
    public IPocoType NonNullable => this;

    /// <inheritdoc/>
    public IPocoType.ITypeRef? FirstBackReference => _firstRef;

    internal IPocoType.ITypeRef? AddBackRef( IPocoType.ITypeRef r )
    {
        Debug.Assert( r != null );
        var f = _firstRef;
        _firstRef = r;
        return f;
    }

    /// <inheritdoc/>
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
    public virtual bool IsSubTypeOf( IPocoType type )
    {
        Throw.DebugAssert( "Null implementations override this.", !IsNullable );
        Throw.DebugAssert( "Value Type nullable <: not nullable is kindly handled by .Net.", typeof( int? ).IsAssignableFrom( typeof( int ) ) );
        return type.Kind == PocoTypeKind.Any || type.NonNullable == this || type.Type.IsAssignableFrom( Type );
    }

    static string ToString( IPocoType t ) => $"[{t.Kind}]{t.CSharpName}";

    /// <summary>
    /// Overridden to return [Kind]CSharpName.
    /// </summary>
    /// <returns>The kind and type.</returns>
    public override sealed string ToString() => ToString( this );

    /// <inheritdoc/>
    public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

    /// <inheritdoc/>
    public object? Annotation( Type type ) => _annotations.Annotation( type );

    /// <inheritdoc/>
    public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

    /// <inheritdoc/>
    public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

    /// <inheritdoc/>
    public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

    /// <inheritdoc/>
    public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

    /// <inheritdoc/>
    public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

}
