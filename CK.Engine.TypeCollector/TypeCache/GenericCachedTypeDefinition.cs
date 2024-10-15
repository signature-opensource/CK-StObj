using CK.Core;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace CK.Engine.TypeCollector;

sealed class GenericCachedTypeDefinition : ICachedType
{
    readonly Type _type;
    readonly CachedAssembly _assembly;
    readonly GlobalTypeCache _typeCache;
    ImmutableArray<CachedGenericParameter> _genericParameters;
    string? _csharpName;

    public GenericCachedTypeDefinition( GlobalTypeCache typeCache, Type type, CachedAssembly assembly )
    {
        _type = type;
        _assembly = assembly;
        _typeCache = typeCache;
    }

    public Type Type => _type;

    public bool IsTypeDefinition => true;

    public string CSharpName => _csharpName ??= _type.ToCSharpName();

    public CachedAssembly Assembly => _assembly;

    public bool IsNullable => false;

    public ICachedType Nullable => this;

    public ICachedType NonNullable => this;

    public ImmutableArray<ICachedType> Interfaces => ImmutableArray<ICachedType>.Empty;

    public ICachedType? BaseType => null;

    public int TypeDepth => 0;

    public ICachedType? GenericTypeDefinition => null;

    public ImmutableArray<CachedGenericParameter> GenericParameters
    {
        get
        {
            if( _genericParameters.IsDefault )
            {
                var parameters = _type.GetGenericArguments();
                var b = ImmutableArray.CreateBuilder<CachedGenericParameter>( parameters.Length );
                foreach( var p in parameters ) b.Add( new CachedGenericParameter( p ) );
                _genericParameters = b.MoveToImmutable();
            }
            return _genericParameters;
        }
    }

    public ImmutableArray<CustomAttributeData> CustomAttributes => ImmutableArray<CustomAttributeData>.Empty;

    public ImmutableArray<CachedMethodInfo> DeclaredMethodInfos => ImmutableArray<CachedMethodInfo>.Empty;

    public GlobalTypeCache TypeCache => _typeCache;

    public override string ToString() => CSharpName;
}
