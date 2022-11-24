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
        readonly IExtMemberInfoFactory _memberInfoFactory;
        // Indexed by:
        //  - Type: for value types (non nullable types only), record struct, IPoco (the interface types)
        //          and non generic and non collection types.
        //  - CSharpName: for anonymous records (Value Tuples) and collection types (because of nullabilities:
        //                the ? marker does the job).
        readonly Dictionary<object, IPocoType> _cache;
        readonly PocoType _objectType;
        readonly PocoType _stringType;
        // Contains the not nullable types (PocoType instances are the non nullable types).
        readonly List<PocoType> _allTypes;
        readonly HalfTypeList _exposedAllTypes;
        readonly Stack<StringBuilder> _stringBuilderPool;
        readonly Dictionary<string, PocoRequiredSupportType> _requiredSupportTypes;

        /// <summary>
        /// Initializes a new type system with only the basic types registered.
        /// </summary>
        public PocoTypeSystem( IExtMemberInfoFactory memberInfoFactory )
        {
            _stringBuilderPool = new Stack<StringBuilder>();
            _memberInfoFactory = memberInfoFactory;
            _allTypes = new List<PocoType>( 8192 );
            _exposedAllTypes = new HalfTypeList( _allTypes );
            _requiredSupportTypes = new Dictionary<string, PocoRequiredSupportType>();
            _cache = new Dictionary<object, IPocoType>()
            {
                { typeof(bool), PocoType.CreateBasicValue(this, typeof(bool), typeof(bool?), "bool") },
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

        public IPocoType ObjectType => _objectType;

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

        public void SetNotExchangeable( IActivityMonitor monitor, IPocoType type )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckArgument( type != null && type.Index < _exposedAllTypes.Count && _exposedAllTypes[type.Index] == type );
            var t = (PocoType)type.NonNullable;
            if( t.IsExchangeable )
            {
                t.SetNotExchangeable( monitor, "TypeSystem external call." );
            }
        }

        public RegisterResult? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo )
        {
            var nType = memberInfo.GetHomogeneousNullabilityInfo( monitor );
            if( nType == null ) return null;
            return Register( monitor, new MemberContext( memberInfo ), nType );
        }

        public RegisterResult? Register( IActivityMonitor monitor, PropertyInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        public RegisterResult? Register( IActivityMonitor monitor, FieldInfo f ) => Register( monitor, _memberInfoFactory.Create( f ) );

        public RegisterResult? Register( IActivityMonitor monitor, ParameterInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        RegisterResult? Register( IActivityMonitor monitor,
                                  MemberContext ctx,
                                  IExtNullabilityInfo nInfo )
        {
            Debug.Assert( !nInfo.Type.IsByRef );
            var result = nInfo.Type.IsValueType
                                  ? OnValueType( monitor, nInfo, ctx )
                                  : OnReferenceType( monitor, nInfo, ctx );
            Debug.Assert( result == null || result.Value.PocoType.IsNullable == nInfo.IsNullable );
            return result;
        }

        RegisterResult? OnReferenceType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            Type t = nType.Type;
            if( t.IsSZArray )
            {
                return OnArray( monitor, nType, ctx );
            }
            if( t.IsGenericType )
            {
                return OnCollection( monitor, nType, ctx );
            }
            if( _cache.TryGetValue( t, out var result ) )
            {
                Debug.Assert( !result.IsNullable );
                Debug.Assert( result.Kind == PocoTypeKind.Any
                              || result.Type == typeof( string )
                              || result.Kind == PocoTypeKind.IPoco
                              || result.Kind == PocoTypeKind.AbstractIPoco );
                return new RegisterResult( nType.IsNullable ? result.Nullable : result, null );
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

        RegisterResult? OnCollection( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            var t = nType.Type;
            var tGen = t.GetGenericTypeDefinition();
            bool isRegular = tGen == typeof( List<> );
            if( isRegular || tGen == typeof( IList<> ) )
            {
                return RegisterListOrSet( monitor, true, nType, ctx, t, isRegular );
            }
            isRegular = tGen == typeof( HashSet<> );
            if( isRegular || tGen == typeof( ISet<> ) )
            {
                return RegisterListOrSet( monitor, false, nType, ctx, t, isRegular );
            }
            isRegular = tGen == typeof( Dictionary<,> );
            if( isRegular || tGen == typeof( IDictionary<,> ) )
            {
                return RegisterDictionary( monitor, nType, ctx, t, isRegular );
            }
            // The end...
            monitor.Error( $"{ctx}: Unsupported Poco generic type: '{t:C}'." );
            return null;

            RegisterResult? RegisterListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx, Type t, bool isRegular )
            {
                var rtI = Register( monitor, ctx, nType.GenericTypeArguments[0] );
                if( rtI == null ) return null;
                var listOrHashSet = isList ? "List" : "HashSet";
                var tI = rtI.Value.PocoType;
                var csharpName = isRegular
                                    ? $"{listOrHashSet}<{tI.CSharpName}>"
                                    : $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
                string? typeName = null;
                if( !isRegular )
                {
                    if( tI.Type.IsValueType )
                    {
                        Debug.Assert( tI.NonNullable.CSharpName == tI.ImplTypeName );
                        if( tI.IsNullable )
                        {
                            typeName = $"CovariantHelpers.CovNullableValue{listOrHashSet}<{tI.NonNullable.CSharpName}>";
                            t = (isList ? typeof( CovariantHelpers.CovNullableValueList<> ) : typeof( CovariantHelpers.CovNullableValueHashSet<> ))
                                .MakeGenericType( tI.NonNullable.Type );
                        }
                        else
                        {
                            typeName = $"CovariantHelpers.CovNotNullValue{listOrHashSet}<{tI.CSharpName}>";
                            t = (isList ? typeof( CovariantHelpers.CovNotNullValueList<> ) : (typeof(CovariantHelpers.CovNotNullValueHashSet<>)))
                                .MakeGenericType( tI.Type );
                        }
                    }
                    else if( tI.Kind == PocoTypeKind.IPoco )
                    {
                        var cType = EnsurePocoListOrHashSetType( monitor, (IPrimaryPocoType)tI.NonNullable, isList, listOrHashSet );
                        if( cType == null ) return default;
                        typeName = cType;
                        t = IDynamicAssembly.PurelyGeneratedType;
                    }
                }
                if( typeName == null )
                {
                    typeName = isRegular ? csharpName : $"{listOrHashSet}<{tI.CSharpName}>";
                    t = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                }
                if( !_cache.TryGetValue( csharpName, out var result ) )
                {
                    result = PocoType.CreateCollection( monitor, this, t, csharpName, typeName, PocoTypeKind.List, tI );
                    _cache.Add( csharpName, result );
                }
                return new RegisterResult( nType.IsNullable ? result.Nullable : result,
                                           nType.IsNullable ? csharpName + "?" : csharpName );
            }

            RegisterResult? RegisterDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, Type t, bool isRegular )
            {
                var rtK = Register( monitor, ctx, nType.GenericTypeArguments[0] );
                if( rtK == null ) return null;
                var tK = rtK.Value.PocoType;
                if( tK.IsNullable )
                {
                    monitor.Error( $"{ctx}: '{nType.Type:C}' key cannot be nullable. Nullable type '{rtK.Value.RegCSharpName}' cannot be a key." );
                    return null;
                }
                var rtV = Register( monitor, ctx, nType.GenericTypeArguments[1] );
                if( rtV == null ) return null;
                var tV = rtV.Value.PocoType;
                var csharpName = isRegular
                                        ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                        : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
                string? typeName = null;
                if( !isRegular )
                {
                    if( tV.Type.IsValueType )
                    {
                        if( tV.IsNullable )
                        {
                            typeName = $"CovariantHelpers.CovNullableValueDictionary<{tK.CSharpName},{tV.NonNullable.CSharpName}>";
                            t = typeof( CovariantHelpers.CovNullableValueDictionary<,> ).MakeGenericType( tK.Type, tV.NonNullable.Type );
                        }
                        else
                        {
                            typeName = $"CovariantHelpers.CovNotNullValueDictionary<{tK.CSharpName},{tV.CSharpName}>";
                            t = typeof( CovariantHelpers.CovNotNullValueDictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        }
                    }
                    else if( tV.Kind == PocoTypeKind.IPoco )
                    {
                        var cType = EnsurePocoDictionaryType( monitor, tK, (IPrimaryPocoType)tV );
                        if( cType == null ) return default;
                        typeName = cType;
                        t = IDynamicAssembly.PurelyGeneratedType;
                    }
                }
                if( typeName == null )
                {
                    typeName = isRegular ? csharpName : $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                    t = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                }
                if( !_cache.TryGetValue( typeName, out var result ) )
                {
                    result = PocoType.CreateDictionary( monitor, this, t, csharpName, typeName, tK, tV );
                    _cache.Add( typeName, result );
                }
                return new RegisterResult( nType.IsNullable ? result.Nullable : result,
                                           nType.IsNullable ? csharpName + "?" : csharpName );
            }

            string? EnsurePocoListOrHashSetType( IActivityMonitor monitor, IPrimaryPocoType tI, bool isList, string listOrHasSet )
            {
                Debug.Assert( !tI.IsNullable );
                var genTypeName = $"Poco{listOrHasSet}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoListOrHashSetRequiredSupport( tI, genTypeName, isList ) );
                }
                return g.FullName;
            }

            string? EnsurePocoDictionaryType( IActivityMonitor monitor, IPocoType key, IPrimaryPocoType tI )
            {
                // There is no difference between Dictionary of nullable and Dictionary of non nullable types for Dictionary.
                tI = tI.NonNullable;
                var genTypeName = $"PocoDictionary_{key.Index}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoDictionaryRequiredSupport( key, tI, genTypeName ) );
                }
                return g.FullName;
            }
        }

        RegisterResult? OnArray( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            Debug.Assert( nType.ElementType != null );
            var rtE = Register( monitor, ctx, nType.ElementType );
            if( rtE == null ) return null;
            var tE = rtE.Value.PocoType;

            string? chsarpName = tE.CSharpName + "[]";
            if( !_cache.TryGetValue( chsarpName, out var result ) )
            {
                result = PocoType.CreateCollection( monitor, this, nType.Type, chsarpName, chsarpName, PocoTypeKind.Array, tE );
                _cache.Add( chsarpName, result );
            }
            return new RegisterResult( nType.IsNullable ? result.Nullable : result,
                                       nType.IsNullable ? chsarpName + "?" : chsarpName );
        }

        RegisterResult? OnValueType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            // Unwrap the nullable value type (or wrap): we reason only on non nullable types.
            Type? tNull;
            Type tNotNull;
            if( nType.IsNullable )
            {
                tNull = nType.Type;
                tNotNull = Nullable.GetUnderlyingType( tNull )!;
                Debug.Assert( tNotNull != null );
            }
            else
            {
                tNotNull = nType.Type;
                tNull = null;
            }
            // We first handle ValueTuple since the cache key must be computed.
            // For other value types, the key is the (non null) type: this avoids a lookup in the cache for basic types.
            if( tNotNull.IsValueTuple() )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nType.GenericTypeArguments.Count );
                tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
                // Anonymous record: the CSharpName is the key and it can
                // be found in the cache or a new one is created.
                return OnValueTypeAnonymousRecord( monitor, ctx, nType, tNotNull, tNull );
            }
            // Basic types and enums are oblivious of the readonly/mutable context.
            // We handle them first before trying the record (fully mutable struct).
            if( _cache.TryGetValue( tNotNull, out var existing ) )
            {
                Debug.Assert( !existing.IsNullable );
                return new RegisterResult( nType.IsNullable ? existing.Nullable : existing, null );
            }
            if( tNotNull.IsEnum )
            {
                if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalNames ) )
                {
                    return null;
                }
                // New Enum (basic type).
                // There is necessary the underlying integral type.
                tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
                existing = PocoType.CreateEnum( monitor,
                                                this,
                                                tNotNull,
                                                tNull,
                                                _cache[tNotNull.GetEnumUnderlyingType()],
                                                externalNames );
                _cache.Add( tNotNull, existing );
                return new RegisterResult( nType.IsNullable ? existing.Nullable : existing, null );
            }
            // Generic value type is not supported.
            if( tNotNull.IsGenericType )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nType.GenericTypeArguments.Count );
                monitor.Error( $"Generic value type cannot be a Poco type: {ctx}." );
                return null;
            }
            // Last chance: may be a new "record struct".
            tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
            return OnTypedRecord( monitor, ctx, nType, tNotNull, tNull );
        }

        RegisterResult? OnValueTypeAnonymousRecord( IActivityMonitor monitor, MemberContext ctx, IExtNullabilityInfo nType, Type tNotNull, Type tNull )
        {
            var subInfos = FlattenValueTuple( nType ).ToList();
            var fields = ctx.GetTupleNamedFields( subInfos.Count );
            // The typeName of an anonymous record uses the
            // registered type names of its fields since a field exposes its
            // FieldTypeCSharpName that is the registered type name.
            //
            // Here we can resolve the field types without fear of infinite recursion: value tuples
            // cannot be recursive by design.
            //
            var b = StringBuilderPool.Get();
            b.Append( '(' );
            int idx = 0;
            foreach( var sub in subInfos )
            {
                var f = fields[idx++];
                var rType = Register( monitor, ctx, sub );
                if( rType == null ) return null;
                Debug.Assert( rType.Value.PocoType != null, "Mutable context: no ReadOnlyAbstraction below." );
                if( b.Length != 1 ) b.Append( ',' );
                // We use the RegCSharpName, not the PocoType's implementation name.
                // This enables the record's type name to be used as the implementation type name and we have not
                // really the choice here: there is no way to generate an adapter that could handle for instance
                // the support of IPoco interface family.
                b.Append( rType.Value.RegCSharpName );
                if( !f.IsUnnamed ) b.Append( ' ' ).Append( f.Name );
                f.SetType( rType.Value.PocoType, rType.Value.RegCSharpName );
            }
            b.Append( ')' );
            var typeName = StringBuilderPool.GetStringAndReturn( b );
            if( !_cache.TryGetValue( typeName, out var exists ) )
            {
                var r = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, fields, null );
                _cache.Add( typeName, exists = r );
            }
            return new RegisterResult( nType.IsNullable ? exists.Nullable : exists,
                                       nType.IsNullable ? typeName + "?" : typeName );

            static IEnumerable<IExtNullabilityInfo> FlattenValueTuple( IExtNullabilityInfo nType )
            {
                Debug.Assert( nType.IsNullable || nType.Type.IsValueTuple() );
                int idx = 0;
                foreach( var info in nType.GenericTypeArguments )
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

        RegisterResult? OnTypedRecord( IActivityMonitor monitor, MemberContext ctx, IExtNullabilityInfo nType, Type tNotNull, Type tNull )
        {
            // C#10 record struct are not decorated by any special attribute: we treat them like any other struct.
            // Allow only fully mutable struct: all its exposed properties and fields must be mutable.
            Debug.Assert( tNotNull.IsValueType );
            Debug.Assert( !_cache.ContainsKey( tNotNull ), "OnValueType found it." );

            if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalNames ) )
            {
                return null;
            }

            var typeName = tNotNull.ToCSharpName();
            var r = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, null, externalNames );
            _cache.Add( tNotNull, r );

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
                var pInfo = _memberInfoFactory.Create( propertyInfos[i] );
                if( !pInfo.PropertyInfo.CanWrite && !pInfo.Type.IsByRef )
                {
                    monitor.Error( $"'{pInfo}' is readonly. A record struct must be fully mutable." );
                    return null;
                }
                var f = CreateField( monitor, i, Register( monitor, pInfo ), pInfo, ctorParams );
                if( f == null ) return null;
                fields[i] = f;
            }
            int idx = propertyInfos.Length;
            for( int i = 0; i < fieldInfos.Length; i++ )
            {
                var fInfo = _memberInfoFactory.Create( fieldInfos[i] );
                if( fInfo.FieldInfo.IsInitOnly )
                {
                    monitor.Error( $"Field '{fInfo.DeclaringType}.{fInfo.Name}' is readonly. A record struct must be fully mutable." );
                    return null;
                }
                var f = CreateField( monitor, idx, Register( monitor, fInfo ), fInfo, ctorParams );
                if( f == null ) return null;
                fields[idx++] = f;
            }
            r.SetFields( monitor, this, fields );
            return new RegisterResult( nType.IsNullable ? r.Nullable : r,
                                       nType.IsNullable ? typeName + "?" : typeName );
        }

        RecordField? CreateField( IActivityMonitor monitor, int idx, RegisterResult? rField, IExtMemberInfo fInfo, ParameterInfo[]? ctorParams )
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
