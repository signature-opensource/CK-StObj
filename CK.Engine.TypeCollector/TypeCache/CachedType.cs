using CK.Core;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace CK.Engine.TypeCollector;

class CachedType : ICachedType
{
    readonly GlobalTypeCache _cache;
    readonly Type _type;
    readonly int _typeDepth;
    readonly CachedAssembly _assembly;
    readonly ImmutableArray<ICachedType> _interfaces;
    readonly ICachedType? _baseType;
    readonly ICachedType? _genericTypeDefinition;
    readonly ICachedType _nullable;
    string? _csharpName;
    ImmutableArray<CustomAttributeData> _customAttributes;
    ImmutableArray<CachedMethodInfo> _declaredMethodInfos;
    ImmutableArray<CachedGenericArgument> _genericArguments;

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

        public bool IsGenericType => _nonNullable.IsGenericType;

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

        public ImmutableArray<CachedGenericArgument> GenericArguments => _nonNullable.GenericArguments;

        public ImmutableArray<CustomAttributeData> CustomAttributes => _nonNullable.CustomAttributes;

        public ImmutableArray<CachedMethodInfo> DeclaredMethodInfos => ((ICachedType)_nonNullable).DeclaredMethodInfos;

        public GlobalTypeCache TypeCache => ((ICachedType)_nonNullable).TypeCache;

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

        public bool IsGenericType => _nonNullable.IsGenericType;

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

        public ImmutableArray<CachedGenericArgument> GenericArguments => _nonNullable.GenericArguments;

        public ImmutableArray<CustomAttributeData> CustomAttributes => _nonNullable.CustomAttributes;

        public ImmutableArray<CachedMethodInfo> DeclaredMethodInfos => _nonNullable.DeclaredMethodInfos;

        public GlobalTypeCache TypeCache => _nonNullable.TypeCache;

        public override string ToString() => CSharpName;
    }

    // Reference type.
    internal CachedType( GlobalTypeCache cache,
                         Type type,
                         int typeDepth,
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
        _genericTypeDefinition = genericTypeDefinition;
        _nullable = new NullReferenceType( this );
    }

    // Value type.
    internal CachedType( GlobalTypeCache cache,
                         Type type,
                         int typeDepth,
                         Type? nullableValueType,
                         CachedAssembly assembly,
                         ImmutableArray<ICachedType> interfaces,
                         ICachedType? genericTypeDefinition )
    {
        _cache = cache;
        _type = type;
        _typeDepth = typeDepth;
        _assembly = assembly;
        _interfaces = interfaces;
        _genericTypeDefinition = genericTypeDefinition;
        if( genericTypeDefinition == null ) _genericArguments = ImmutableArray<CachedGenericArgument>.Empty;
        _nullable = nullableValueType != null
                    ? new NullValueType( this, nullableValueType )
                    : this;
    }

    public Type Type => _type;

    public bool IsTypeDefinition => false;

    public bool IsGenericType => _genericTypeDefinition != null;

    public int TypeDepth => _typeDepth;

    public CachedAssembly Assembly => _assembly;

    public ImmutableArray<ICachedType> Interfaces => _interfaces;

    public ICachedType? BaseType => _baseType;

    public ICachedType? GenericTypeDefinition => _genericTypeDefinition;

    public string CSharpName => _csharpName ??= _type.ToCSharpName();

    public bool IsNullable => false;

    public ICachedType Nullable => _nullable;

    public ICachedType NonNullable => this;

    public ImmutableArray<CachedGenericParameter> GenericParameters => ImmutableArray<CachedGenericParameter>.Empty;

    public ImmutableArray<CachedGenericArgument> GenericArguments
    {
        get
        {
            if( _genericArguments.IsDefault )
            {
                Throw.DebugAssert( _genericTypeDefinition != null );
                var arguments = _type.GetGenericArguments();
                var b = ImmutableArray.CreateBuilder<CachedGenericArgument>( arguments.Length );
                for( int i = 0; i < arguments.Length; i++ )
                {
                    Type? a = arguments[i];
                    if( a.IsGenericParameter )
                    {
                        Throw.DebugAssert( a.DeclaringType != null );
                        b.Add( new CachedGenericArgument( _cache.Get( a.DeclaringType ).GenericParameters[i], null ) );
                    }
                    else
                    {
                        b.Add( new CachedGenericArgument( _genericTypeDefinition.GenericParameters[i], _cache.Get( a ) ) );
                    }
                }
                _genericArguments = b.MoveToImmutable();
            }
            return _genericArguments;
        }
    }

    public ImmutableArray<CustomAttributeData> CustomAttributes
    {
        get
        {
            if( _customAttributes.IsDefault )
            {
                // Canot use ImmutableCollectionsMarshal.AsImmutableArray here: CustomAttributeData
                // can be retrieved by IList<CustomAttributeData> or IEnumerable<CustomAttributeData> only.
                _customAttributes = _type.CustomAttributes.ToImmutableArray();
            }
            return _customAttributes;
        }
    }

    public ImmutableArray<CachedMethodInfo> DeclaredMethodInfos
    {
        get
        {
            if( _declaredMethodInfos.IsDefault )
            {
                var methods = _type.GetMethods( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly );
                var b = ImmutableArray.CreateBuilder<CachedMethodInfo>( methods.Length );
                foreach( var method in methods ) b.Add( new CachedMethodInfo( this, method ) );
                _declaredMethodInfos = b.MoveToImmutable();
            }
            return _declaredMethodInfos;
        }
    }

    public GlobalTypeCache TypeCache => _cache;

    public override string ToString() => CSharpName;
}
