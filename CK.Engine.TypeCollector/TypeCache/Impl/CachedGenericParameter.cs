using CK.Core;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

class CachedGenericParameter : ICachedType
{
    readonly Type _parameter;
    readonly GlobalTypeCache _typeCache;
    readonly CachedAssembly _assembly;
    ICachedType? _declaringType;

    public CachedGenericParameter( GlobalTypeCache typeCache, Type type, CachedAssembly assembly )
    {
        Throw.DebugAssert( type.DeclaringType != null );
        _typeCache = typeCache;
        _parameter = type;
        _assembly = assembly;
    }

    public Type Type => _parameter;

    public bool IsGenericType => false;

    public bool IsTypeDefinition => false;

    public bool IsPublic => Type.IsVisible;

    public string CSharpName => _parameter.Name;

    public CachedAssembly Assembly => _assembly;

    public bool IsNullable => false;

    public ICachedType Nullable => this;

    public ICachedType NonNullable => this;

    public ImmutableArray<ICachedType> Interfaces => ImmutableArray<ICachedType>.Empty;

    public ICachedType? BaseType => null;

    public ICachedType? DeclaringType => _declaringType ??= _typeCache.Get( _parameter.DeclaringType! );

    public int TypeDepth => 0;

    public ICachedType? GenericTypeDefinition => null;

    public ImmutableArray<ICachedType> GenericArguments => ImmutableArray<ICachedType>.Empty;

    public ImmutableArray<CustomAttributeData> CustomAttributes => ImmutableArray<CustomAttributeData>.Empty;

    public ImmutableArray<object> Attributes => ImmutableArray<object>.Empty;

    public ImmutableArray<CachedMethodInfo> DeclaredMethodInfos => ImmutableArray<CachedMethodInfo>.Empty;

    public GlobalTypeCache TypeCache => _typeCache;

    public ImmutableArray<ICachedMember> DeclaredMembers => ImmutableArray<ICachedMember>.Empty;

    public ImmutableArray<ICachedType> DeclaredBaseTypes => ImmutableArray<ICachedType>.Empty;

    public string Name => _parameter.Name;

    public ICachedType? ElementType => null;

    public TypeKind Kind => TypeKind.IsNullFullName;

    public override string ToString() => _parameter.Name;

    public StringBuilder Write( StringBuilder b ) => b.Append( _parameter.Name );
}
