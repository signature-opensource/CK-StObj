using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    partial class PocoType
    {

        /// <summary>
        /// Fake type that is only here to holds the oblivious fields.
        /// This is the nullable (oblivious reference types are nullable) and
        /// it contains a not nullable companion.
        /// <para>
        /// Instances of this type cannot exist: this is only here for the oblivious fields.
        /// The IPocoType.Index are the same as the actual IPrimaryPocoType.
        /// </para>
        /// </summary>
        internal sealed class PrimaryPocoTypeONull : IPrimaryPocoType
        {
            readonly IPrimaryPocoType _primary;
            readonly Field[] _fields;
            readonly NotNull _notNull;
            AnnotationSetImpl _annotations;

            sealed class NotNull : IPrimaryPocoType
            {
                readonly PrimaryPocoTypeONull _nullable;
                AnnotationSetImpl _annotations;

                public NotNull( PrimaryPocoTypeONull nullable )
                {
                    _nullable = nullable;
                }

                public IPocoFamilyInfo FamilyInfo => _nullable._primary.FamilyInfo;

                public IReadOnlyList<IPrimaryPocoField> Fields => _nullable._fields;

                public IPrimaryPocoType ObliviousType => _nullable;

                public IReadOnlyList<IAbstractPocoType> AbstractTypes => _nullable._primary.AbstractTypes;

                public string CSharpBodyConstructorSourceCode => _nullable._primary.CSharpBodyConstructorSourceCode;

                public IPrimaryPocoType Nullable => _nullable;

                public IPrimaryPocoType NonNullable => this;

                public int Index => _nullable._primary.Index;

                public Type Type => _nullable._primary.Type;

                public bool IsPurelyGeneratedType => false;

                public PocoTypeKind Kind => PocoTypeKind.IPoco;

                public DefaultValueInfo DefaultValueInfo => _nullable._primary.DefaultValueInfo;

                public bool IsNullable => false;

                public string CSharpName => _nullable._primary.CSharpName;

                public string ImplTypeName => _nullable._primary.ImplTypeName;

                public bool IsExchangeable => _nullable._primary.IsExchangeable;

                public IPocoType.ITypeRef? FirstBackReference => _nullable._primary.FirstBackReference;

                public ExternalNameAttribute? ExternalName => _nullable._primary.ExternalName;

                public string ExternalOrCSharpName => _nullable._primary.ExternalOrCSharpName;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => _nullable.Fields;

                ICompositePocoType ICompositePocoType.ObliviousType => _nullable;

                IPocoType IPocoType.ObliviousType => _nullable;

                ICompositePocoType ICompositePocoType.Nullable => _nullable;

                IPocoType IPocoType.Nullable => _nullable;

                ICompositePocoType ICompositePocoType.NonNullable => this;

                IPocoType IPocoType.NonNullable => this;

                public bool IsReadableType( IExtNullabilityInfo type ) => _nullable._primary.IsReadableType( type );

                public bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false ) => _nullable._primary.IsSameType( type, ignoreRootTypeIsNullable );

                public bool IsWritableType( IExtNullabilityInfo type ) => _nullable._primary.IsWritableType( type );

                public override string ToString() => PocoType.ToString( this, true );

                public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

                public object? Annotation( Type type ) => _annotations.Annotation( type );

                public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

                public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

                public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

                public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

                public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

            }

            public PrimaryPocoTypeONull( IPrimaryPocoType primary )
            {
                _primary = primary;
                _fields = new Field[primary.Fields.Count];
                for( int i = 0; i < primary.Fields.Count; i++ )
                {
                    _fields[i] = new Field( this, primary.Fields[i] );
                }
                _notNull = new NotNull( this );
            }

            public IPocoFamilyInfo FamilyInfo => _primary.FamilyInfo;

            public IReadOnlyList<IPrimaryPocoField> Fields => _fields;

            public IPrimaryPocoType ObliviousType => this;

            public IReadOnlyList<IAbstractPocoType> AbstractTypes => _primary.Nullable.AbstractTypes;

            public string CSharpBodyConstructorSourceCode => _primary.CSharpBodyConstructorSourceCode;

            public IPrimaryPocoType Nullable => this;

            public IPrimaryPocoType NonNullable => _notNull;

            public int Index => _primary.Nullable.Index;

            public Type Type => _primary.Type;

            public bool IsPurelyGeneratedType => _primary.IsPurelyGeneratedType;

            public PocoTypeKind Kind => PocoTypeKind.IPoco;

            public DefaultValueInfo DefaultValueInfo => _primary.DefaultValueInfo;

            public bool IsNullable => true;

            public string CSharpName => _primary.Nullable.CSharpName;

            public string ImplTypeName => _primary.ImplTypeName;

            public bool IsExchangeable => _primary.IsExchangeable;

            public IPocoType.ITypeRef? FirstBackReference => _primary.FirstBackReference;

            public ExternalNameAttribute? ExternalName => _primary.ExternalName;

            public string ExternalOrCSharpName => _primary.ExternalOrCSharpName;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            ICompositePocoType ICompositePocoType.ObliviousType => this;

            IPocoType IPocoType.ObliviousType => this;

            ICompositePocoType ICompositePocoType.Nullable => this;

            IPocoType IPocoType.Nullable => this;

            ICompositePocoType ICompositePocoType.NonNullable => _notNull;

            IPocoType IPocoType.NonNullable => _notNull;

            public override string ToString() => PocoType.ToString( this, true );

            public void AddAnnotation( object annotation ) => _annotations.AddAnnotation( annotation );

            public object? Annotation( Type type ) => _annotations.Annotation( type );

            public T? Annotation<T>() where T : class => _annotations.Annotation<T>();

            public IEnumerable<object> Annotations( Type type ) => _annotations.Annotations( type );

            public IEnumerable<T> Annotations<T>() where T : class => _annotations.Annotations<T>();

            public void RemoveAnnotations( Type type ) => _annotations.RemoveAnnotations( type );

            public void RemoveAnnotations<T>() where T : class => _annotations.RemoveAnnotations<T>();

            public bool IsReadableType( IExtNullabilityInfo type ) => _primary.IsReadableType( type );

            public bool IsSameType( IExtNullabilityInfo type, bool ignoreRootTypeIsNullable = false ) => _primary.IsSameType( type, ignoreRootTypeIsNullable );

            public bool IsWritableType( IExtNullabilityInfo type ) => _primary.IsWritableType( type );
        }

        sealed class Field : IPrimaryPocoField
        {
            readonly IPrimaryPocoField _f;
            readonly PrimaryPocoTypeONull _owner;

            public Field( PrimaryPocoTypeONull owner, IPrimaryPocoField f )
            {
                _owner = owner;
                _f = f;
            }

            public IPrimaryPocoType Owner => _owner;

            ICompositePocoType IPocoField.Owner => _owner;

            IPocoType IPocoType.ITypeRef.Owner => _owner;

            public IPocoType Type => _f.Type.ObliviousType;

            public string Name => _f.Name;

            public int Index => _f.Index;

            public IPocoPropertyInfo Property => _f.Property;

            public PocoFieldAccessKind FieldAccess => _f.FieldAccess;

            public string PrivateFieldName => _f.PrivateFieldName;

            public DefaultValueInfo DefaultValueInfo => _f.DefaultValueInfo;

            public bool IsExchangeable => _f.IsExchangeable;

            public IPocoType.ITypeRef? NextRef => ((IPocoType.ITypeRef)_f).NextRef;

        }

    }
}
