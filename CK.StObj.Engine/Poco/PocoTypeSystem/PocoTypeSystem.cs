using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;
using NullabilityInfoContext = System.Reflection.TEMPNullabilityInfoContext;

namespace CK.Setup
{
    using RegisterResult = IPocoTypeSystem.RegisterResult;

    /// <summary>
    /// Implementation of <see cref="IPocoTypeSystem"/>.
    /// </summary>
    public sealed partial class PocoTypeSystem : IPocoTypeSystem
    {
        readonly NullabilityInfoContext _nullContext;
        // Indexed by:
        //  - Type: for value types (non nullable types only), record struct, IPoco (the interface types)
        //          and non generic and non collection types.
        //  - CSharpName: for anonymous records (Value Tuples) and collection types (because of nullabilities:
        //                the ? marker does the job).
        readonly Dictionary<object, IPocoType> _cache;
        readonly PocoType _objectType;
        readonly PocoType _stringType;
        readonly List<PocoType> _allTypes;
        readonly HalfTypeList _exposedAllTypes;
        readonly Stack<StringBuilder> _stringBuilderPool;
        readonly Dictionary<string, PocoRequiredSupportType> _requiredSupportTypes;

        /// <summary>
        /// Initializes a new type system with only the basic types registered.
        /// </summary>
        public PocoTypeSystem()
        {
            _stringBuilderPool = new Stack<StringBuilder>();
            _nullContext = new NullabilityInfoContext();
            _allTypes = new List<PocoType>( 8192 );
            _exposedAllTypes = new HalfTypeList( _allTypes );
            _requiredSupportTypes = new Dictionary<string, PocoRequiredSupportType>();
            _cache = new Dictionary<object, IPocoType>()
            {
                { typeof(int), PocoType.CreateBasicValue(this, typeof(int), typeof(int?), "int") },
                { typeof(long), PocoType.CreateBasicValue(this, typeof(long), typeof(long?), "long") },
                { typeof(short), PocoType.CreateBasicValue(this, typeof(short), typeof(short?), "short") },
                { typeof(byte), PocoType.CreateBasicValue(this, typeof(byte), typeof(byte?), "byte") },
                { typeof(double), PocoType.CreateBasicValue(this, typeof(double), typeof(double?), "double") },
                { typeof(float), PocoType.CreateBasicValue(this, typeof(float), typeof(float?), "float") },
                { typeof(DateTime), PocoType.CreateBasicValue(this, typeof(DateTime), typeof(DateTime?), "DateTime") },
                { typeof(DateTimeOffset), PocoType.CreateBasicValue(this, typeof(DateTimeOffset), typeof(DateTimeOffset?), "DateTimeOffset") },
                { typeof(TimeSpan), PocoType.CreateBasicValue(this, typeof(TimeSpan), typeof(TimeSpan?), "TimeSpan") },
                { typeof(Guid), PocoType.CreateBasicValue(this, typeof(Guid), typeof(Guid?), "Guid") },
                { typeof(decimal), PocoType.CreateBasicValue(this, typeof(decimal), typeof(decimal?), "decimal") },
                { typeof(System.Numerics.BigInteger), PocoType.CreateBasicValue(this, typeof(System.Numerics.BigInteger), typeof(System.Numerics.BigInteger?), "System.Numerics.BigInteger") },
                { typeof(uint), PocoType.CreateBasicValue(this, typeof(uint), typeof(uint?), "uint") },
                { typeof(ulong), PocoType.CreateBasicValue(this, typeof(ulong), typeof(ulong?), "ulong") },
                { typeof(ushort), PocoType.CreateBasicValue(this, typeof(ushort), typeof(ushort?), "ushort") },
                { typeof(sbyte), PocoType.CreateBasicValue(this, typeof(sbyte), typeof(sbyte?), "sbyte") }
            };
            _cache.Add( "object", _objectType = PocoType.CreateBasicRef( this, typeof( object ), "object", PocoTypeKind.Any ) );
            _cache.Add( _objectType.Type, _objectType );
            _cache.Add( "string", _stringType = PocoType.CreateBasicRef( this, typeof( string ), "string", PocoTypeKind.Basic ) );
            _cache.Add( _stringType.Type, _stringType );
        }

        public IReadOnlyList<IPocoType> AllTypes => _exposedAllTypes;

        public IReadOnlyList<IPocoType> AllNonNullableTypes => _allTypes;

        public IReadOnlyCollection<PocoRequiredSupportType> RequiredSupportTypes => _requiredSupportTypes.Values;

        internal void AddNew( PocoType t )
        {
            Debug.Assert( t.Index == _allTypes.Count * 2 );
            _allTypes.Add( t );
        }

        public IPocoType? FindByType( Type type )
        {
            if( type.IsValueType )
            {
                var tNotNull = Nullable.GetUnderlyingType( type );
                if( tNotNull != null )
                {
                    return _cache.GetValueOrDefault( tNotNull )?.Nullable;
                }
            }
            return _cache.GetValueOrDefault( type );
        }

        public IPrimaryPocoType? GetPrimaryPocoType( Type i )
        {
            if( _cache.TryGetValue( i, out var result ) )
            {
                return result as IPrimaryPocoType;
            }
            return null;
        }

        public RegisterResult? Register( IActivityMonitor monitor, PropertyInfo p )
        {
            var nullabilityInfo = _nullContext.Create( p );
            return RegisterRoot( monitor, new MemberContext( p ), nullabilityInfo );
        }

        public RegisterResult? Register( IActivityMonitor monitor, FieldInfo f )
        {
            var nullabilityInfo = _nullContext.Create( f );
            return RegisterRoot( monitor, new MemberContext( f ), nullabilityInfo );
        }

        public RegisterResult? Register( IActivityMonitor monitor, ParameterInfo p )
        {
            var nullabilityInfo = _nullContext.Create( p );
            return RegisterRoot( monitor, new MemberContext( p ), nullabilityInfo );
        }

        internal RegisterResult? TryRegisterCollection( IActivityMonitor monitor, PropertyInfo p, out bool error )
        {
            var nullabilityInfo = _nullContext.Create( p );
            Debug.Assert( !nullabilityInfo.Type.IsByRef );
            Debug.Assert( !nullabilityInfo.Type.IsValueType );
            Debug.Assert( nullabilityInfo.Type.IsGenericType );
            var r = TryHandleCollectionType( monitor, nullabilityInfo.Type, nullabilityInfo, new MemberContext( p ), out var isCollection );
            error = r == null && isCollection;
            return r;
        }

        RegisterResult? RegisterRoot( IActivityMonitor monitor, MemberContext root, NullabilityInfo nullabilityInfo )
        {
            if( nullabilityInfo.Type.IsByRef )
            {
                // Erase the ByRef if any: this why the Type is
                // (unfortunately) given to the called methods that must
                // not used nInfo.Type.
                Type t = nullabilityInfo.Type.GetElementType()!;
                if( !t.IsValueType )
                {
                    monitor.Error( $"Invalid '{root}': ref properties must be used only for record, not for reference type." );
                    return null;
                }
                Debug.Assert( nullabilityInfo.WriteState == NullabilityState.Unknown );
                var result = OnValueType( monitor, t, nullabilityInfo, root );
                if( result == null ) return null;
                return nullabilityInfo.ReadState == NullabilityState.NotNull ? result : result.Value.Nullable;
            }
            return TryRegister( monitor, nullabilityInfo, root );
        }

        RegisterResult? TryRegister( IActivityMonitor monitor,
                                     NullabilityInfo nInfo,
                                     MemberContext ctx )
        {
            Debug.Assert( !nInfo.Type.IsByRef );
            // First, reject any difference between Read and Write state.
            if( nInfo.WriteState != NullabilityState.Unknown && nInfo.ReadState != nInfo.WriteState )
            {
                monitor.Error( $"Read/Write nullability differ for {ctx}. No [AllowNull], [DisallowNull] or other nullability attributes should be used." );
                return null;
            }
            var result = nInfo.Type.IsValueType
                                  ? OnValueType( monitor, nInfo.Type, nInfo, ctx )
                                  : OnReferenceType( monitor, nInfo, ctx );
            if( result == null ) return null;
            // Consider Unknown as being nullable (oblivious context).
            bool isNullable = nInfo.ReadState != NullabilityState.NotNull;
            return isNullable ? result.Value.Nullable : result;
        }

        RegisterResult? OnReferenceType( IActivityMonitor monitor, NullabilityInfo nInfo, MemberContext ctx )
        {
            Type t = nInfo.Type;
            if( t.IsSZArray )
            {
                return OnArray( monitor, t, nInfo, ctx );
            }
            if( t.IsGenericType )
            {
                var r = TryHandleCollectionType( monitor, t, nInfo, ctx, out var isCollection );
                if( r == null )
                {
                    if( !isCollection )
                    {
                        monitor.Error( $"{ctx}: Unsupported Poco generic type: '{t.ToCSharpName( false )}'." );
                    }
                    return null;
                }
                return r;
            }
            if( _cache.TryGetValue( t, out var result ) )
            {
                Debug.Assert( result.Kind == PocoTypeKind.Any
                              || result.Type == typeof( string )
                              || result.Kind == PocoTypeKind.IPoco
                              || result.Kind == PocoTypeKind.AbstractIPoco );
                return new RegisterResult( result, null, false );
            }
            if( typeof( IPoco ).IsAssignableFrom( t ) )
            {
                monitor.Error( $"IPoco '{t}' has been excluded." );
            }
            else
            {
                monitor.Error( $"Unsupported Poco type: '{t}'." );
            }
            return null;
        }

        RegisterResult? TryHandleCollectionType( IActivityMonitor monitor, Type t, NullabilityInfo nInfo, MemberContext ctx, out bool isCollection )
        {
            isCollection = true;
            var tGen = t.GetGenericTypeDefinition();
            bool isReadOnly = tGen == typeof( IReadOnlyList<> );
            if( isReadOnly || tGen == typeof( IList<> ) )
            {
                var rtI = TryRegister( monitor, nInfo.GenericTypeArguments[0], ctx );
                if( rtI == null ) return null;
                var regTypeName = isReadOnly
                                    ? $"System.Collections.Generic.IReadOnlyList<{rtI.Value.RegCSharpName}>"
                                    : $"System.Collections.Generic.IList<{rtI.Value.RegCSharpName}>";
                var tI = rtI.Value.PocoType;
                string typeName;
                if( tI.Type.IsValueType )
                {
                    if( tI.IsNullable )
                    {
                        typeName = $"CK.Core.CovariantHelpers.CovNullableValueList<{tI.NonNullable.CSharpName}>";
                    }
                    else
                    {
                        typeName = $"CK.Core.CovariantHelpers.CovNotNullValueList<{tI.CSharpName}>";
                    }
                }
                else
                {
                    if( tI.Kind == PocoTypeKind.IPoco )
                    {
                        var cType = EnsurePocoListType( monitor, (IPrimaryPocoType)tI );
                        if( cType == null ) return default;
                        typeName = cType;
                    }
                    else
                    {
                        typeName = $"System.Collections.Generic.List<{tI.CSharpName}>";
                    }
                }
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.List, tI );
                    _cache.Add( typeName, result );
                }
                return new RegisterResult( result, regTypeName, isReadOnly || rtI.Value.HasReadOnlyCollection );
            }
            isReadOnly = tGen == typeof( IReadOnlySet<> );
            if( isReadOnly || tGen == typeof( ISet<> ) )
            {
                var rtI = TryRegister( monitor, nInfo.GenericTypeArguments[0], ctx );
                if( rtI == null ) return null;
                var regTypeName = isReadOnly
                                    ? $"System.Collections.Generic.IReadOnlySet<{rtI.Value.RegCSharpName}>"
                                    : $"System.Collections.Generic.ISet<{rtI.Value.RegCSharpName}>";
                var tI = rtI.Value.PocoType;
                string typeName;
                if( tI.Type.IsValueType )
                {
                    if( tI.IsNullable )
                    {
                        typeName = $"CK.Core.CovariantHelpers.CovNullableValueHashSet<{tI.NonNullable.CSharpName}>";
                    }
                    else
                    {
                        typeName = $"CK.Core.CovariantHelpers.CovNotNullValueHashSet<{tI.CSharpName}>";
                    }
                }
                else
                {
                    if( tI.Kind == PocoTypeKind.IPoco )
                    {
                        var cType = EnsurePocoHashSetType( monitor, (IPrimaryPocoType)tI );
                        if( cType == null ) return default;
                        typeName = cType;
                    }
                    else
                    {
                        typeName = $"System.Collections.Generic.HashSet<{tI.CSharpName}>";
                    }
                }
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.HashSet, tI );
                    _cache.Add( typeName, result );
                }
                return new RegisterResult( result, regTypeName, isReadOnly || rtI.Value.HasReadOnlyCollection );
            }
            isReadOnly = tGen == typeof( IReadOnlyDictionary<,> );
            if( isReadOnly || tGen == typeof( IDictionary<,> ) )
            {
                var rtK = TryRegister( monitor, nInfo.GenericTypeArguments[0], ctx );
                if( rtK == null ) return null;
                if( rtK.Value.PocoType == null )
                {
                    monitor.Error( $"{ctx}: Dictionary key cannot be \"abstract\" type. Type '{rtK.Value.RegCSharpName}' is not supported." );
                    return null;
                }
                var tK = rtK.Value.PocoType;
                var rtV = TryRegister( monitor, nInfo.GenericTypeArguments[1], ctx );
                if( rtV == null ) return null;
                var regTypeName = isReadOnly
                    ? $"System.Collections.Generic.IReadOnlyDictionary<{rtK.Value.RegCSharpName},{rtV.Value.RegCSharpName}>"
                    : $"System.Collections.Generic.IDictionary<{rtK.Value.RegCSharpName},{rtV.Value.RegCSharpName}>";
                var tV = rtV.Value.PocoType;
                string typeName;
                if( tV.Type.IsValueType )
                {
                    if( tV.IsNullable )
                    {
                        typeName = $"CK.Core.CovariantHelpers.CovNullableValueDictionary<{tK.CSharpName},{tV.NonNullable.CSharpName}>";
                    }
                    else
                    {
                        typeName = $"CK.Core.CovariantHelpers.CovNotNullValueDictionary<{tK.CSharpName},{tV.CSharpName}>";
                    }
                }
                else
                {
                    if( tV.Kind == PocoTypeKind.IPoco )
                    {
                        var cType = EnsurePocoDictionaryType( monitor, tK, (IPrimaryPocoType)tV );
                        if( cType == null ) return default;
                        typeName = cType;
                    }
                    else
                    {
                        typeName = $"System.Collections.Generic.Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                    }
                }
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.Dictionary, rtK.Value.PocoType, tV );
                    _cache.Add( typeName, result );
                }
                return new RegisterResult( result, regTypeName, isReadOnly || rtV.Value.HasReadOnlyCollection );
            }
            isCollection = false;
            return null;

            string? EnsurePocoListType( IActivityMonitor monitor, IPrimaryPocoType tI )
            {
                var genTypeName = $"PocoList_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoListRequiredSupport( tI, genTypeName ) );
                }
                return g.FullName;
            }

            string? EnsurePocoHashSetType( IActivityMonitor monitor, IPrimaryPocoType tI )
            {
                var genTypeName = $"PocoHashSet_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoHashSetRequiredSupport( tI, genTypeName ) );
                }
                return g.FullName;
            }

            string? EnsurePocoDictionaryType( IActivityMonitor monitor, IPocoType key, IPrimaryPocoType tI )
            {
                var genTypeName = $"PocoDicitionary_{key.Index}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoDictionaryRequiredSupport( key, tI, genTypeName ) );
                }
                return g.FullName;
            }

        }

        RegisterResult? OnArray( IActivityMonitor monitor, Type t, NullabilityInfo nInfo, MemberContext ctx )
        {
            Debug.Assert( nInfo.ElementType != null );
            var rtE = TryRegister( monitor, nInfo.ElementType, ctx );
            if( rtE == null ) return null;
            var tE = rtE.Value.PocoType;
            var typeName = tE.CSharpName + "[]";
            if( !_cache.TryGetValue( typeName, out var result ) )
            {
                result = PocoType.CreateCollection( this, t, typeName, PocoTypeKind.Array, tE );
                _cache.Add( typeName, result );
            }
            var regTypeName = rtE.Value.HasRegCSharpName ? rtE.Value.RegCSharpName + "[]" : null;
            return new RegisterResult( result, regTypeName, rtE.Value.HasReadOnlyCollection );
        }

        RegisterResult? OnValueType( IActivityMonitor monitor, Type t, NullabilityInfo nInfo, MemberContext ctx )
        {
            // Unwrap the nullable value type (or wrap): we reason only on non nullable types.
            Type tNull;
            Type tNotNull;
            if( nInfo.ReadState == NullabilityState.Nullable )
            {
                tNull = t;
                tNotNull = Nullable.GetUnderlyingType( t )!;
                Debug.Assert( tNotNull != null );
                // The NullabityInfo model is not like ours: there is no GenericTypeArguments
                // for Nullable<T> when T is not a generic type.
                // If nInfo must be used below it has to be lifted with the following trick:
                // if( t == tNull ) nInfo = _nullContext.Create( tNull.GetProperty("Value")! );
            }
            else
            {
                tNotNull = t;
                tNull = typeof( Nullable<> ).MakeGenericType( tNotNull );
            }
            // We must first handle ValueTuple since the cache key must be computed.
            // For other value types, the key is the (non null) type.
            if( tNotNull.IsValueTuple() )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nInfo.GenericTypeArguments.Length );
                if( t == tNull ) nInfo = _nullContext.Create( tNull.GetProperty( "Value" )! );
                // Anonymous record: the CSharpName is the key and it can
                // be found in the cache or a new one is created.
                return OnValueTypeAnonymousRecord( monitor, nInfo, ctx, tNull, tNotNull );
            }
            if( _cache.TryGetValue( tNotNull, out var existing ) )
            {
                return new RegisterResult( existing, null, false );
            }
            if( tNotNull.IsEnum )
            {
                // New Enum (basic type).
                // There is necessary the underlying integral type.
                existing = PocoType.CreateEnum( monitor, this, tNotNull, tNull, _cache[tNotNull.GetEnumUnderlyingType()] );
                _cache.Add( tNotNull, existing );
                return new RegisterResult( existing, null, false );
            }
            // Generic value type is not supported.
            if( tNotNull.IsGenericType )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nInfo.GenericTypeArguments.Length );
                monitor.Error( $"Generic value type cannot be a Poco type: {ctx}." );
                return null;
            }
            // Last chance: may be a new "record struct".
            return OnTypedRecord( monitor, ctx, tNull, tNotNull );
        }

        RegisterResult? OnValueTypeAnonymousRecord( IActivityMonitor monitor, NullabilityInfo nInfo, MemberContext ctx, Type tNull, Type tNotNull )
        {
            var subInfos = FlattenValueTuple( nInfo ).ToList();
            var fields = ctx.GetTupleNamedFields( subInfos.Count );
            // The typeName of an anonymous record uses the
            // registered type names of its fields since a field exposes its
            // FieldTypeCSharpName that is the registered type name.
            var b = StringBuilderPool.Get();
            b.Append( '(' );
            int idx = 0;
            foreach( var sub in subInfos )
            {
                var f = fields[idx++];
                var rType = TryRegister( monitor, sub, ctx );
                if( rType == null ) return null;
                if( b.Length != 1 ) b.Append( ',' );
                b.Append( rType.Value.RegCSharpName );
                if( !f.IsUnnamed ) b.Append( ' ' ).Append( f.Name );
                f.SetType( rType.Value.PocoType, rType.Value.RegCSharpName );
            }
            b.Append( ')' );
            var typeName = StringBuilderPool.GetStringAndReturn( b );
            if( !_cache.TryGetValue( typeName, out var exists ) )
            {
                var r = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, true, fields );
                _cache.Add( typeName, exists = r );
            }
            return new RegisterResult( exists, typeName, false );

            static IEnumerable<NullabilityInfo> FlattenValueTuple( NullabilityInfo nInfo )
            {
                Debug.Assert( nInfo.Type.IsValueTuple() );
                int idx = 0;
                foreach( var info in nInfo.GenericTypeArguments )
                {
                    if( ++idx == 8 )
                    {
                        foreach( var rest in FlattenValueTuple( info ) )
                        {
                            yield return rest;
                        };
                    }
                    yield return info;
                }
            }
        }

        RegisterResult? OnTypedRecord( IActivityMonitor monitor, MemberContext ctx, Type tNull, Type tNotNull )
        {
            // C#10 record struct are not decorated by any special attribute: we treat them like any other struct.
            // Allow only fully mutable struct: all its exposed properties and fields must be mutable.
            Debug.Assert( tNotNull.IsValueType );
            // Currently allows only a single constructor. This is not good: we should allow deserialization constructor...
            // We should try to consider a constructor whose parameter names are the fields/property names only (and only them).
            var ctors = tNotNull.GetConstructors();
            if( ctors.Length > 1 )
            {
                monitor.Error( $"More than one constructor found for record struct '{tNotNull}': at most one constructor must be specified for record struct." );
                return null;
            }
            var ctorParams = ctors.Length == 1 ? ctors[0].GetParameters() : null;

            var propertyInfos = tNotNull.GetProperties();
            var fieldInfos = tNotNull.GetFields();
            var fields = new RecordField[propertyInfos.Length + fieldInfos.Length];
            for( int i = 0; i < propertyInfos.Length; i++ )
            {
                var pInfo = propertyInfos[i];
                if( !pInfo.CanWrite && !pInfo.PropertyType.IsByRef )
                {
                    monitor.Error( $"Property '{pInfo.DeclaringType}.{pInfo.Name}' is readonly. A record struct must be fully mutable." );
                    return null;
                }
                var f = CreateField( monitor, i, Register( monitor, pInfo ), pInfo, ctorParams );
                if( f == null ) return null;
                fields[i] = f;
            }
            int idx = propertyInfos.Length;
            for( int i = 0; i < fieldInfos.Length; i++ )
            {
                var fInfo = fieldInfos[i];
                if( fInfo.IsInitOnly )
                {
                    monitor.Error( $"Field '{fInfo.DeclaringType}.{fInfo.Name}' is readonly. A record struct must be fully mutable." );
                    return null;
                }
                var f = CreateField( monitor, idx, Register( monitor, fInfo ), fInfo, ctorParams );
                if( f == null ) return null;
                fields[idx++] = f;
            }
            var typeName = tNotNull.ToCSharpName();
            var r = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, false, fields );
            _cache.Add( tNotNull, r );
            return new RegisterResult( r, typeName, false );
        }

        RecordField? CreateField( IActivityMonitor monitor, int idx, RegisterResult? rField, MemberInfo fInfo, ParameterInfo[]? ctorParams )
        {
            if( rField == null ) return null;
            var defValue = FieldDefaultValue.CreateFromAttribute( monitor, StringBuilderPool, fInfo );
            if( defValue == null && ctorParams != null )
            {
                var p = ctorParams.FirstOrDefault( p => p.Name == fInfo.Name );
                if( p != null ) defValue = FieldDefaultValue.CreateFromParameter( monitor, StringBuilderPool, p );
            }
            var field = new RecordField( idx, fInfo.Name, defValue );
            field.SetType( rField.Value.PocoType, rField.Value.RegCSharpName );
            return field;
        }

        IUnionPocoType? RegisterUnionType( IActivityMonitor monitor, List<IPocoType> types )
        {
            var a = types.ToArray();
            var k = new PocoType.KeyUnionTypes( a );
            if( _cache.TryGetValue( k, out var result ) ) return (IUnionPocoType)result;
            var t = PocoType.CreateUnion( monitor, this, a );
            _cache.Add( k, t );
            return t;
        }

    }

}
