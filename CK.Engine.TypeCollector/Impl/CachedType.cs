using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace CK.Engine.TypeCollector;


class CachedType : ICachedType
{
    readonly TypeCache _cache;
    readonly Type _type;
    readonly int _typeDepth;
    readonly CachedAssembly _assembly;
    readonly string _csharpName;
    readonly ImmutableArray<ICachedType> _interfaces;
    readonly ICachedType? _baseType;
    readonly ICachedType? _genericTypeDefinition;
    readonly ICachedType _nullable;
    readonly ImmutableArray<CachedGenericParameter> _genericParameters;
    ImmutableArray<CustomAttributeData> _customAttributes;
    readonly bool _isTypeDefinition;

    sealed class NullReferenceType : ICachedType
    {
        readonly CachedType _nonNullable;
        string? _name;

        public NullReferenceType( CachedType nonNullable )
        {
            _nonNullable = nonNullable;
        }

        public Type Type => _nonNullable.Type;

        public bool IsTypeDefinition => _nonNullable.IsTypeDefinition;

        public int TypeDepth => _nonNullable.TypeDepth;

        public CachedAssembly Assembly => _nonNullable.Assembly;

        public bool IsNullable => true;

        public ICachedType Nullable => this;

        public ICachedType NonNullable => _nonNullable;

        public string CSharpName => _name ??= _nonNullable.CSharpName + "?";

        public ImmutableArray<ICachedType> Interfaces => _nonNullable.Interfaces;

        public ICachedType? BaseType => _nonNullable.BaseType?.Nullable;

        public ICachedType? GenericTypeDefinition => _nonNullable.GenericTypeDefinition;

        public ImmutableArray<CachedGenericParameter> GenericParameters => _nonNullable.GenericParameters;

        public ImmutableArray<CustomAttributeData> CustomAttributes => _nonNullable.CustomAttributes;

        public override string ToString() => CSharpName;
    }

    sealed class NullValueType : ICachedType
    {
        readonly CachedType _nonNullable;
        readonly Type _type;
        string? _name;

        public NullValueType( CachedType nonNullable, Type t )
        {
            Throw.DebugAssert( System.Nullable.GetUnderlyingType( t ) == nonNullable.Type );
            _nonNullable = nonNullable;
            _type = t;
        }

        public Type Type => _type;

        public bool IsTypeDefinition => _nonNullable.IsTypeDefinition;

        public int TypeDepth => _nonNullable.TypeDepth;

        public CachedAssembly Assembly => _nonNullable.Assembly;

        public bool IsNullable => true;

        public ICachedType Nullable => this;

        public ICachedType NonNullable => _nonNullable;

        public string CSharpName => _name ??= _nonNullable.CSharpName + "?";

        public ImmutableArray<ICachedType> Interfaces => _nonNullable.Interfaces;

        public ICachedType? BaseType => null;

        public ICachedType? GenericTypeDefinition => _nonNullable.GenericTypeDefinition;

        public ImmutableArray<CachedGenericParameter> GenericParameters => _nonNullable.GenericParameters;

        public ImmutableArray<CustomAttributeData> CustomAttributes => _nonNullable.CustomAttributes;

        public override string ToString() => CSharpName;
    }

    internal CachedType( TypeCache cache,
                         Type type,
                         int typeDepth,
                         Type? nullableValueType,
                         CachedAssembly assembly,
                         ImmutableArray<ICachedType> interfaces,
                         ICachedType? baseType,
                         ICachedType? genericTypeDefinition )
    {
        _cache = cache;
        _type = type;
        _typeDepth = typeDepth;
        _assembly = assembly;
        _interfaces = interfaces;
        _baseType = baseType;
        _csharpName = type.ToCSharpName();
        _isTypeDefinition = type.IsGenericTypeDefinition;
        _genericTypeDefinition = genericTypeDefinition;
        _genericParameters = type.IsGenericTypeDefinition
                                ? type.GetGenericArguments().Select( t => new CachedGenericParameter( t ) ).ToImmutableArray()
                                : ImmutableArray<CachedGenericParameter>.Empty;
        _nullable = nullableValueType == null
                        ? new NullReferenceType( this )
                        : new NullValueType( this, nullableValueType );
    }

    public TypeCache Cache => _cache;

    public Type Type => _type;

    public bool IsTypeDefinition => _isTypeDefinition;

    public int TypeDepth => _typeDepth;

    public CachedAssembly Assembly => _assembly;

    public ImmutableArray<ICachedType> Interfaces => _interfaces;

    public ICachedType? BaseType => _baseType;

    public ICachedType? GenericTypeDefinition => _genericTypeDefinition;

    public string CSharpName => _csharpName;

    public bool IsNullable => false;

    public ICachedType Nullable => _nullable;

    public ICachedType NonNullable => this;

    public ImmutableArray<CachedGenericParameter> GenericParameters => _genericParameters;

    public ImmutableArray<CustomAttributeData> CustomAttributes
    {
        get
        {
            if( _customAttributes.IsDefault )
            {
                _customAttributes = _type.CustomAttributes.ToImmutableArray();
            }
            return _customAttributes;
        }
    }

    public override string ToString() => _csharpName;
}
