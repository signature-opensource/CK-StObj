using CK.Core;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;

partial class CachedType : CachedItem, ICachedType
{
    readonly GlobalTypeCache _cache;
    readonly int _typeDepth;
    readonly CachedAssembly _assembly;
    readonly ImmutableArray<ICachedType> _interfaces;
    readonly ICachedType? _baseType;
    [AllowNull]readonly ICachedType _nullable;

    ICachedType? _genericTypeDefinition;
    string? _csharpName;
    ICachedType? _declaringType;
    ICachedType? _elementType;
    ImmutableArray<ICachedMember> _declaredMembers;
    ImmutableArray<ICachedType> _genericArguments;

    const EngineUnhandledType _uninitialized = (EngineUnhandledType)0xFF;
    EngineUnhandledType _unhandledType;

    readonly bool _isGenericType;
    readonly bool _isGenericTypeDefinition;

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

        public string Name => _nonNullable.Name;

        public ImmutableArray<ICachedType> Interfaces => _nonNullable.Interfaces;

        public ICachedType? BaseType => _nonNullable.BaseType?.Nullable;

        public ICachedType? DeclaringType => _nonNullable.DeclaringType;

        public ICachedType? GenericTypeDefinition => _nonNullable.GenericTypeDefinition;

        public ImmutableArray<ICachedType> GenericArguments => _nonNullable.GenericArguments;

        public ImmutableArray<CustomAttributeData> CustomAttributes => _nonNullable.CustomAttributes;

        public ImmutableArray<ICachedMember> DeclaredMembers => _nonNullable.DeclaredMembers;

        public GlobalTypeCache TypeCache => _nonNullable.TypeCache;

        public ICachedType? ElementType => _nonNullable.ElementType;

        public EngineUnhandledType EngineUnhandledType => _nonNullable.EngineUnhandledType;

        public StringBuilder Write( StringBuilder b ) => b.Append( CSharpName );

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

        public string Name => _nonNullable.Name;

        public ImmutableArray<ICachedType> Interfaces => _nonNullable.Interfaces;

        public ICachedType? BaseType => null;

        public ICachedType? DeclaringType => _nonNullable.DeclaringType;

        public ICachedType? GenericTypeDefinition => _nonNullable.GenericTypeDefinition;

        public ImmutableArray<ICachedType> GenericArguments => _nonNullable.GenericArguments;

        public ImmutableArray<CustomAttributeData> CustomAttributes => _nonNullable.CustomAttributes;

        public ImmutableArray<ICachedMember> DeclaredMembers => _nonNullable.DeclaredMembers;

        public GlobalTypeCache TypeCache => _nonNullable.TypeCache;

        public ICachedType? ElementType => _nonNullable.ElementType;

        public EngineUnhandledType EngineUnhandledType => _nonNullable.EngineUnhandledType;

        public StringBuilder Write( StringBuilder b ) => b.Append( CSharpName );

        public override string ToString() => CSharpName;
    }

    CachedType( GlobalTypeCache cache,
                Type type,
                int typeDepth,
                CachedAssembly assembly,
                ImmutableArray<ICachedType> interfaces )
        : base( type )
    {
        _cache = cache;
        _typeDepth = typeDepth;
        _assembly = assembly;
        _interfaces = interfaces;
        _isGenericTypeDefinition = type.IsGenericTypeDefinition;
        _isGenericType = type.IsGenericType;
        if( !_isGenericType ) _genericArguments = ImmutableArray<ICachedType>.Empty;
    }

    // Reference type.
    internal CachedType( GlobalTypeCache cache,
                         Type type,
                         int typeDepth,
                         CachedAssembly assembly,
                         ImmutableArray<ICachedType> interfaces,
                         ICachedType? baseType )
        : this( cache, type, typeDepth, assembly, interfaces )
    {
        _baseType = baseType;
        _nullable = new NullReferenceType( this );
    }

    // Value type.
    internal CachedType( GlobalTypeCache cache,
                         Type type,
                         int typeDepth,
                         Type? nullableValueType,
                         CachedAssembly assembly,
                         ImmutableArray<ICachedType> interfaces )
        : this( cache, type, typeDepth, assembly, interfaces )
    {
        _nullable = nullableValueType != null
                    ? new NullValueType( this, nullableValueType )
                    : this;
    }

    public Type Type => Unsafe.As<Type>( _member );

    public bool IsTypeDefinition => _isGenericTypeDefinition;

    public bool IsGenericType => _isGenericType;

    public int TypeDepth => _typeDepth;

    public CachedAssembly Assembly => _assembly;

    public ImmutableArray<ICachedType> Interfaces => _interfaces;

    public ICachedType? BaseType => _baseType;

    public ICachedType? DeclaringType
    {
        get
        {
            if( _declaringType == null )
            {
                var t = Type.DeclaringType;
                _declaringType = t != null ? _cache.Get( t ) : _nullMarker;
            }
            return NullMarker.Filter( _declaringType );
        }
    }

    public EngineUnhandledType EngineUnhandledType
    {
        get
        {
            if( _unhandledType == _uninitialized )
            {
                if( Type.FullName == null ) _unhandledType = EngineUnhandledType.NullFullName;
                else if( _assembly.Assembly.IsDynamic ) _unhandledType = EngineUnhandledType.FromDynamicAssembly;
                else if( !Type.IsVisible ) _unhandledType = EngineUnhandledType.NotVisible;
                else if( !Type.IsValueType || !Type.IsClass || !Type.IsInterface || !Type.IsEnum ) _unhandledType = EngineUnhandledType.NotClassEnumValueTypeOrEnum;
                else _unhandledType = EngineUnhandledType.None;
                   
            }
            return _unhandledType;
        }
    }

    public ICachedType? GenericTypeDefinition => _genericTypeDefinition ??= _isGenericType ? _cache.Get( Type.GetGenericTypeDefinition() ) : null;

    public string CSharpName => _csharpName ??= Type.ToCSharpName();

    public bool IsNullable => false;

    public ICachedType Nullable => _nullable;

    public ICachedType NonNullable => this;

    public ICachedType? ElementType => NullMarker.Filter( _elementType ??= Type.HasElementType ? _cache.Get( Type.GetElementType()! ) : _nullMarker );

    public ImmutableArray<ICachedType> GenericArguments
    {
        get
        {
            if( _genericArguments.IsDefault )
            {
                Throw.DebugAssert( _isGenericType );
                var arguments = Type.GetGenericArguments();
                var b = ImmutableArray.CreateBuilder<ICachedType>( arguments.Length );
                foreach( Type a in arguments )
                {
                    b.Add( _cache.Get( a ) );
                }
                _genericArguments = b.MoveToImmutable();
            }
            return _genericArguments;
        }
    }

    public ImmutableArray<ICachedMember> DeclaredMembers
    {
        get
        {
            if( _declaredMembers.IsDefault )
            {
                var members = Type.GetMembers( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly );
                var b = ImmutableArray.CreateBuilder<ICachedMember>( members.Length );
                foreach( var m in members )
                {
                    var map = m switch
                    {
                        MethodInfo method => new CachedMethodInfo( this, method ),
                        ConstructorInfo ctor => new CachedConstructorInfo( this, ctor ),
                        PropertyInfo prop => new CachedPropertyInfo( this, prop ),
                        EventInfo ev => new CachedEventInfo( this, ev ),
                        FieldInfo f => new CachedFieldInfo( this, f ),
                        Type nested => null,
                        _ => Throw.NotSupportedException<ICachedMember>( m.ToString() )
                    };
                    if( map != null ) b.Add( map );
                }
                _declaredMembers = b.DrainToImmutable();
            }
            return _declaredMembers;
        }
    }

    public GlobalTypeCache TypeCache => _cache;

    public override StringBuilder Write( StringBuilder b ) => b.Append( CSharpName );

    public override string ToString() => CSharpName;
}
