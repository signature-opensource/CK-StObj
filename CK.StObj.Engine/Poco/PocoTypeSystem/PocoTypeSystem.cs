using CK.CodeGen;
using CK.Core;
using OneOf.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using NullabilityInfo = System.Reflection.TEMPNullabilityInfo;
using NullabilityInfoContext = System.Reflection.TEMPNullabilityInfoContext;

namespace CK.Setup
{
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
            _regNominalCollections = new Dictionary<string, RegularAndNominalInfo>();
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
            Throw.CheckArgument( type.Kind != PocoTypeKind.Any );
            var t = (PocoType)type.NonNullable;
            if( t.IsExchangeable )
            {
                t.SetNotExchangeable( monitor, "TypeSystem external call." );
            }
        }

        public IPocoType? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo )
        {
            var nType = memberInfo.GetHomogeneousNullabilityInfo( monitor );
            if( nType == null ) return null;
            return Register( monitor, new MemberContext( memberInfo ), nType );
        }

        public IPocoType? Register( IActivityMonitor monitor, PropertyInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        public IPocoType? Register( IActivityMonitor monitor, FieldInfo f ) => Register( monitor, _memberInfoFactory.Create( f ) );

        public IPocoType? Register( IActivityMonitor monitor, ParameterInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        IPocoType? Register( IActivityMonitor monitor,
                                  MemberContext ctx,
                                  IExtNullabilityInfo nInfo )
        {
            Debug.Assert( !nInfo.Type.IsByRef );
            var result = nInfo.Type.IsValueType
                                  ? OnValueType( monitor, nInfo, ctx )
                                  : OnReferenceType( monitor, nInfo, ctx );
            Debug.Assert( result == null || result.IsNullable == nInfo.IsNullable );
            return result;
        }

        IPocoType? OnReferenceType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
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
                return nType.IsNullable ? result.Nullable : result;
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

        IPocoType? OnCollection( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
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

            IPocoType? RegisterListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx, Type t, bool isRegular )
            {
                var tI = Register( monitor, ctx, nType.GenericTypeArguments[0] );
                if( tI == null ) return null;
                var listOrHashSet = isList ? "List" : "HashSet";

                string? nominalAndRegularName = null;
                var csharpName = isRegular
                                    ? $"{listOrHashSet}<{tI.CSharpName}>"
                                    : $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
                if( !_cache.TryGetValue( csharpName, out var result ) )
                {
                    string? requiredNominalCSharpName = null;
                    string? typeName = null;
                    if( !isRegular )
                    {
                        // IList or ISet
                        if( tI.Type.IsValueType )
                        {
                            // For value type item, use our covariant implementations.
                            Debug.Assert( tI.CSharpName == tI.ImplTypeName );
                            if( tI.IsNullable )
                            {
                                typeName = $"CovariantHelpers.CovNullableValue{listOrHashSet}<{tI.NonNullable.CSharpName}>";
                                t = (isList ? typeof( CovariantHelpers.CovNullableValueList<> ) : typeof( CovariantHelpers.CovNullableValueHashSet<> ))
                                    .MakeGenericType( tI.NonNullable.Type );
                            }
                            else
                            {
                                typeName = $"CovariantHelpers.CovNotNullValue{listOrHashSet}<{tI.CSharpName}>";
                                t = (isList ? typeof( CovariantHelpers.CovNotNullValueList<> ) : (typeof( CovariantHelpers.CovNotNullValueHashSet<> )))
                                    .MakeGenericType( tI.Type );
                            }
                            // These are the nominal implementations: we let the null requiredNominalCSharpName.
                            // The nominal and regular type name includes the nullability of the value type.
                            nominalAndRegularName = $"{listOrHashSet}<{tI.CSharpName}>";
                        }
                        else if( tI.Kind == PocoTypeKind.IPoco )
                        {
                            // For IPoco, use generated covariant implementations.
                            // We choose the nullable item type to follow the C# "oblivious nullable reference type". 
                            var cType = EnsurePocoListOrHashSetType( monitor, (IPrimaryPocoType)tI.Nullable, isList, listOrHashSet );
                            if( cType == null ) return default;
                            typeName = cType;
                            t = IDynamicAssembly.PurelyGeneratedType;
                            // This implementation is the nominal one if the item type is nullable because of the C# "oblivious nullable reference type".
                            // If once we generate implementations that guaranty non nullable items, them these will be both nominal.
                            // By choosing the nullable here, we expose that it's a IList<IThing?> that actually is implemented and not a
                            // (buggy) IList<IThing>.
                            if( !tI.IsNullable )
                            {
                                requiredNominalCSharpName = $"{(isList ? "IList" : "ISet")}<{tI.Nullable.CSharpName}>";
                            }
                            // Since we are on a reference type, the nominal and regular uses the nullable.
                            nominalAndRegularName = $"{listOrHashSet}<{tI.Nullable.CSharpName}>";
                        }
                    }
                    if( typeName == null )
                    {
                        // It's not an abstraction for which we have a dedicated implementation or it's explicitly a regular List/HashSet.
                        typeName = isRegular && (tI.Type.IsValueType || tI.IsNullable) ? csharpName : $"{listOrHashSet}<{tI.Nullable.CSharpName}>";
                        t = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                        // This is the nominal implementation if:
                        //   - the regular type has been requested
                        //   - AND
                        //      - the item type is a value type (nullability is handled at the type level)
                        //      - OR the item type is nullable.
                        // Same as above here: we expose that it's a List<T?> that actually is implemented and not a
                        // (buggy) IList<T>.
                        if( !(isRegular && (tI.Type.IsValueType || tI.IsNullable)) )
                        {
                            requiredNominalCSharpName = nominalAndRegularName = $"{listOrHashSet}<{tI.Nullable.CSharpName}>";
                        }
                        else
                        {
                            nominalAndRegularName = tI.Type.IsValueType
                                                        ? $"{listOrHashSet}<{tI.CSharpName}>"
                                                        : $"{listOrHashSet}<{tI.Nullable.CSharpName}>";
                        }
                    }
                    Debug.Assert( nominalAndRegularName != null );
                    // Handle the nominal implementation: if are here with a null implNominalType then
                    // the nominal type is about to be created.
                    IPocoType? implNominalType = null;
                    if( requiredNominalCSharpName != null )
                    {
                        // The type we are about to create is not the implementation nominal one.
                        // However we have everything here to create it:
                        // - It has the same t, typeName and kind.
                        // - Its item type is tI.Nullable.
                        // - We used the requiredNominalCSharpName != null as a flag, so we have it.
                        if( !_cache.TryGetValue( requiredNominalCSharpName, out implNominalType ) )
                        {
                            implNominalType = PocoType.CreateCollection( monitor,
                                                                         this,
                                                                         t,
                                                                         requiredNominalCSharpName,
                                                                         typeName,
                                                                         isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                         tI.Nullable,
                                                                         null,
                                                                         nominalAndRegularName );
                            _cache.Add( requiredNominalCSharpName, implNominalType );
                        }
                    }
                    result = PocoType.CreateCollection( monitor,
                                                        this,
                                                        t,
                                                        csharpName,
                                                        typeName,
                                                        isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                        tI,
                                                        (ICollectionPocoType?)implNominalType,
                                                        nominalAndRegularName );
                    _cache.Add( csharpName, result );
                }
                Debug.Assert( result.ImplTypeName == result.ImplNominalType.ImplTypeName );
                return nType.IsNullable ? result.Nullable : result;
            }

            IPocoType? RegisterDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, Type t, bool isRegular )
            {
                var tK = Register( monitor, ctx, nType.GenericTypeArguments[0] );
                if( tK == null ) return null;
                if( tK.IsNullable )
                {
                    monitor.Error( $"{ctx}: '{nType.Type:C}' key cannot be nullable. Nullable type '{tK.CSharpName}' cannot be a key." );
                    return null;
                }
                var tV = Register( monitor, ctx, nType.GenericTypeArguments[1] );
                if( tV == null ) return null;

                string? nominalAndRegularName = null;
                var csharpName = isRegular
                                        ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                        : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
                if( !_cache.TryGetValue( csharpName, out var result ) )
                {
                    string? requiredNominalCSharpName = null;
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
                            nominalAndRegularName = $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                        }
                        else if( tV.Kind == PocoTypeKind.IPoco )
                        {
                            var cType = EnsurePocoDictionaryType( monitor, tK, (IPrimaryPocoType)tV.Nullable );
                            if( cType == null ) return default;
                            typeName = cType;
                            t = IDynamicAssembly.PurelyGeneratedType;
                            if( !tV.IsNullable )
                            {
                                requiredNominalCSharpName = $"IDictionary<{tK.CSharpName},{tV.Nullable.CSharpName}>";
                            }
                            nominalAndRegularName = $"Dictionary<{tK.CSharpName},{tV.Nullable.CSharpName}>";
                        }
                    }
                    if( typeName == null )
                    {
                        typeName = isRegular && (tV.Type.IsValueType || tV.IsNullable)
                                    ? csharpName
                                    : $"Dictionary<{tK.CSharpName},{tV.Nullable.CSharpName}>";
                        t = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        if( !(isRegular && (tV.Type.IsValueType || tV.IsNullable)) )
                        {
                            requiredNominalCSharpName = nominalAndRegularName = $"Dictionary<{tK.CSharpName},{tV.Nullable.CSharpName}>";
                        }
                        else
                        {
                            nominalAndRegularName = tV.Type.IsValueType
                                                        ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                                        : $"Dictionary<{tK.CSharpName},{tV.Nullable.CSharpName}>";
                        }
                    }
                    Debug.Assert( nominalAndRegularName != null );
                    IPocoType? implNominalType = null;
                    if( requiredNominalCSharpName != null )
                    {
                        if( !_cache.TryGetValue( requiredNominalCSharpName, out implNominalType ) )
                        {
                            implNominalType = PocoType.CreateDictionary( monitor,
                                                                         this,
                                                                         t,
                                                                         requiredNominalCSharpName,
                                                                         typeName,
                                                                         tK,
                                                                         tV.Nullable,
                                                                         null,
                                                                         nominalAndRegularName );
                            _cache.Add( requiredNominalCSharpName, implNominalType );
                        }
                    }
                    result = PocoType.CreateDictionary( monitor,
                                                        this,
                                                        t,
                                                        csharpName,
                                                        typeName,
                                                        tK,
                                                        tV,
                                                        (ICollectionPocoType?)implNominalType,
                                                        nominalAndRegularName );
                    _cache.Add( csharpName, result );
                }
                return nType.IsNullable ? result.Nullable : result;
            }

            string? EnsurePocoListOrHashSetType( IActivityMonitor monitor, IPrimaryPocoType tI, bool isList, string listOrHasSet )
            {
                Debug.Assert( tI.IsNullable );
                var genTypeName = $"Poco{listOrHasSet}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoListOrHashSetRequiredSupport( tI, genTypeName, isList ) );
                }
                return g.FullName;
            }

            string? EnsurePocoDictionaryType( IActivityMonitor monitor, IPocoType key, IPrimaryPocoType tI )
            {
                Debug.Assert( tI.IsNullable );
                var genTypeName = $"PocoDictionary_{key.Index}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoDictionaryRequiredSupport( key, tI, genTypeName ) );
                }
                return g.FullName;
            }
        }

        IPocoType? OnArray( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            Debug.Assert( nType.ElementType != null );

            // The nominal array type is the array of its value type item
            // or the array of its nullable reference type.
            var tItem = Register( monitor, ctx, nType.ElementType );
            if( tItem == null ) return null;
            // If the item is a value type OR a nullable reference type then, it is the nominal.
            bool isNominal = tItem.Type.IsValueType || tItem.IsNullable;

            if( !_cache.TryGetValue( nType.Type, out var nominalType ) )
            {
                // The nominal array is not registered.
                var tItemForNominal = isNominal ? tItem : tItem.Nullable;
                var chsarpNameNominal = tItemForNominal.CSharpName + "[]";
                nominalType = PocoType.CreateCollection( monitor,
                                                         this,
                                                         nType.Type,
                                                         chsarpNameNominal,
                                                         chsarpNameNominal,
                                                         PocoTypeKind.Array,
                                                         tItemForNominal,
                                                         null,
                                                         null );
                _cache.Add( nType.Type, nominalType );
            }
            if( isNominal ) return nType.IsNullable ? nominalType.Nullable : nominalType;

            var chsarpName = tItem.CSharpName + "[]";
            if( !_cache.TryGetValue( chsarpName, out var result ) )
            {
                result = PocoType.CreateCollection( monitor,
                                                    this,
                                                    nType.Type,
                                                    chsarpName,
                                                    chsarpName,
                                                    PocoTypeKind.Array,
                                                    tItem,
                                                    (ICollectionPocoType)nominalType,
                                                    null );
                _cache.Add( chsarpName, result );
            }
            return nType.IsNullable ? result.Nullable : result;
        }

        IPocoType? OnValueType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
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
                return nType.IsNullable ? existing.Nullable : existing;
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
                return nType.IsNullable ? existing.Nullable : existing;
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

        IPocoType? OnValueTypeAnonymousRecord( IActivityMonitor monitor, MemberContext ctx, IExtNullabilityInfo nType, Type tNotNull, Type tNull )
        {
            var subInfos = FlattenValueTuple( nType ).ToList();
            var fields = ctx.GetTupleNamedFields( subInfos.Count );
            // Here we can resolve the field types without fear of infinite recursion: value tuples
            // cannot be recursive by design.
            var b = StringBuilderPool.Get();
            var bNominal = StringBuilderPool.Get();
            b.Append( '(' );
            bNominal.Append( '(' );
            int idx = 0;
            foreach( var sub in subInfos )
            {
                var f = fields[idx++];
                var tF = Register( monitor, ctx, sub );
                if( tF == null ) return null;
                if( b.Length != 1 )
                {
                    b.Append( ',' );
                    bNominal.Append( ',' );
                }
                // We use the CSharpName, not the PocoType's ImplTypeName.
                // This enables the record's type name to be used as the implementation type name and we have not
                // really the choice here: there is no way to generate an adapter that could handle for instance
                // the support of IPoco interface family.
                b.Append( tF.CSharpName );
                bNominal.Append( tF.CSharpName );
                if( !f.IsUnnamed ) b.Append( ' ' ).Append( f.Name );
                f.SetType( tF );
            }
            b.Append( ')' );
            bNominal.Append( ')' );

            var nominalTypeName = StringBuilderPool.GetStringAndReturn( bNominal );
            if( !_cache.TryGetValue( nominalTypeName, out var implNominalType ) )
            {
                RecordField[] nominalFields = new RecordField[fields.Length];
                for( int i = 0; i < nominalFields.Length; i++ )
                {
                    var f  = new RecordField( i, null );
                    f.SetType( fields[i].Type );
                    nominalFields[i] = f;
                }
                implNominalType = PocoType.CreateRecord( monitor, this, tNotNull, tNull, nominalTypeName, nominalFields, null, null );
                _cache.Add( nominalTypeName, implNominalType );
            }

            var typeName = StringBuilderPool.GetStringAndReturn( b );
            var result = implNominalType;
            if( typeName != nominalTypeName )
            {
                if( !_cache.TryGetValue( typeName, out result ) )
                {
                    result = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, fields, null, implNominalType );
                    _cache.Add( typeName, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result;

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

        IPocoType? OnTypedRecord( IActivityMonitor monitor, MemberContext ctx, IExtNullabilityInfo nType, Type tNotNull, Type tNull )
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
            var r = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, null, externalNames, null );
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
            return nType.IsNullable ? r.Nullable : r;
        }

        RecordField? CreateField( IActivityMonitor monitor, int idx, IPocoType? tField, IExtMemberInfo fInfo, ParameterInfo[]? ctorParams )
        {
            if( tField == null ) return null;
            var defValue = FieldDefaultValue.CreateFromAttribute( monitor, StringBuilderPool, fInfo );
            if( defValue == null && ctorParams != null )
            {
                var p = ctorParams.FirstOrDefault( p => p.Name == fInfo.Name );
                if( p != null ) defValue = FieldDefaultValue.CreateFromParameter( monitor, StringBuilderPool, p );
            }
            var field = new RecordField( idx, fInfo.Name, defValue );
            field.SetType( tField );
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
