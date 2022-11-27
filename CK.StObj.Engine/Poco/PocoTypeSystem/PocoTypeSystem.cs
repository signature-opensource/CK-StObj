using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.CSharp;
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

                // When we are on a recursive named record like:
                //
                //  public record struct Rec( IList<Rec> R, int A );
                //
                // The Rec.ObliviousType is null (still unknown).
                if( tI.ObliviousType == null )
                {
                    Debug.Assert( tI.Kind == PocoTypeKind.Record );
                    // We need a trampoline here: this is not currently supported.
                    monitor.Error( $"{ctx}: '{t:C}' Recursive named record definition is currently not supported." );
                    return null;
                }

                var listOrHashSet = isList ? "List" : "HashSet";
                var csharpName = isRegular
                                    ? $"{listOrHashSet}<{tI.CSharpName}>"
                                    : $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
                if( !_cache.TryGetValue( csharpName, out var result ) )
                {
                    string? obliviousCSharpName = null;
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
                            // The oblivious type name includes the nullability of the value type.
                            obliviousCSharpName = $"{listOrHashSet}<{tI.CSharpName}>";
                        }
                        else if( tI.Kind == PocoTypeKind.IPoco )
                        {
                            // For IPoco, use generated covariant implementations.
                            // We choose the nullable item type to follow the C# "oblivious nullable reference type". 
                            typeName = EnsurePocoListOrHashSetType( monitor, (IPrimaryPocoType)tI.Nullable, isList, listOrHashSet );
                            t = IDynamicAssembly.PurelyGeneratedType;
                            // Since we are on a reference type, the oblivious uses the nullable.
                            Debug.Assert( tI.Nullable.IsOblivious, "Non composed nullable reference types are their own oblivious type." );
                            obliviousCSharpName = $"{listOrHashSet}<{tI.Nullable.CSharpName}>";
                        }
                    }
                    if( typeName == null )
                    {
                        // It's not an abstraction for which we have a dedicated implementation or it's
                        // explicitly a regular List/HashSet: use the regular collection type.
                        t = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                        // This is the oblivious implementation if:
                        //   - the regular type has been requested
                        //   - AND the item type is the oblivious one.
                        // Same as above here: we expose that it's a List<T?> that actually is implemented and not a
                        // (buggy) IList<T>.
                        if( isRegular && tI.IsOblivious )
                        {
                            // We are building the oblivious: let the obliviousCSharpName be null.
                            typeName = csharpName;
                        }
                        else
                        {
                            obliviousCSharpName = $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>";
                            typeName = obliviousCSharpName;
                            Debug.Assert( typeName != csharpName, "This is why this is not the oblivious type." );
                        }
                    }
                    // Handle the oblivious implementation: if we are here with a null obliviousCSharpName then
                    // the oblivious type is about to be created.
                    IPocoType? obliviousType = null;
                    if( obliviousCSharpName != null )
                    {
                        // The type we are about to create is not the implementation oblivious one.
                        // However we have everything here to create it:
                        // - It has the same t, typeName and kind.
                        // - Its item type is tI.ObliviousType.
                        // - We used the nominalCSharpName != null as a flag, so we have it.
                        if( !_cache.TryGetValue( obliviousCSharpName, out obliviousType ) )
                        {
                            obliviousType = PocoType.CreateCollection( monitor,
                                                                         this,
                                                                         t,
                                                                         obliviousCSharpName,
                                                                         typeName,
                                                                         isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                         tI.ObliviousType,
                                                                         null );
                            _cache.Add( obliviousCSharpName, obliviousType );
                        }
                        // The oblivious reference type is the nullable.
                        obliviousType = obliviousType.Nullable;
                    }
                    result = PocoType.CreateCollection( monitor,
                                                        this,
                                                        t,
                                                        csharpName,
                                                        typeName,
                                                        isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                        tI,
                                                        obliviousType );
                    _cache.Add( csharpName, result );

                }
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

                // When we are on a recursive named record like:
                //
                //  public record struct Rec( Dictionary<int,Rec> R, int A );
                //
                // The Rec.ObliviousType is null (still unknown).
                if( tK.ObliviousType == null )
                {
                    Debug.Assert( tK.Kind == PocoTypeKind.Record );
                    // We need a trampoline here: this is not currently supported.
                    monitor.Error( $"{ctx}: '{t:C}' Recursive named record definition is currently not supported." );
                    return null;
                }
                if( tV.ObliviousType == null )
                {
                    Debug.Assert( tV.Kind == PocoTypeKind.Record );
                    // We need a trampoline here: this is not currently supported.
                    monitor.Error( $"{ctx}: '{t:C}' Recursive named record definition is currently not supported." );
                    return null;
                }

                var csharpName = isRegular
                                    ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                    : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
                if( !_cache.TryGetValue( csharpName, out var result ) )
                {
                    string? obliviousCSharpName = null;
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
                            Debug.Assert( tV.IsOblivious, "Value types are their own oblivious type." );
                            obliviousCSharpName = $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                        }
                        else if( tV.Kind == PocoTypeKind.IPoco )
                        {
                            typeName = EnsurePocoDictionaryType( monitor, tK, (IPrimaryPocoType)tV.Nullable );
                            t = IDynamicAssembly.PurelyGeneratedType;
                            Debug.Assert( tV.Nullable.IsOblivious, "Non composed nullable reference types are their own oblivious type." );
                            obliviousCSharpName = $"Dictionary<{tK.CSharpName},{tV.Nullable.CSharpName}>";
                        }
                    }
                    if( typeName == null )
                    {
                        t = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        if( isRegular && tV.IsOblivious )
                        {
                            typeName = csharpName;
                        }
                        else
                        {
                            obliviousCSharpName = $"Dictionary<{tK.CSharpName},{tV.ObliviousType.CSharpName}>";
                            typeName = obliviousCSharpName;
                            Debug.Assert( typeName != csharpName, "This is why this is not the nominal type." );
                        }
                    }
                    IPocoType? obliviousType = null;
                    if( obliviousCSharpName != null )
                    {
                        if( !_cache.TryGetValue( obliviousCSharpName, out obliviousType ) )
                        {
                            obliviousType = PocoType.CreateDictionary( monitor,
                                                                         this,
                                                                         t,
                                                                         obliviousCSharpName,
                                                                         typeName,
                                                                         tK,
                                                                         tV.ObliviousType,
                                                                         null );
                            _cache.Add( obliviousCSharpName, obliviousType );
                        }
                        obliviousType = obliviousType.Nullable;
                    }
                    result = PocoType.CreateDictionary( monitor,
                                                        this,
                                                        t,
                                                        csharpName,
                                                        typeName,
                                                        tK,
                                                        tV,
                                                        obliviousType );
                    _cache.Add( csharpName, result );
                }
                return nType.IsNullable ? result.Nullable : result;
            }

            string EnsurePocoListOrHashSetType( IActivityMonitor monitor, IPrimaryPocoType tI, bool isList, string listOrHasSet )
            {
                Debug.Assert( tI.IsNullable );
                var genTypeName = $"Poco{listOrHasSet}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoListOrHashSetRequiredSupport( tI, genTypeName, isList ) );
                }
                return g.FullName;
            }

            string EnsurePocoDictionaryType( IActivityMonitor monitor, IPocoType tK, IPrimaryPocoType tV )
            {
                Debug.Assert( tV.IsNullable );
                var genTypeName = $"PocoDictionary_{tK.Index}_{tV.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoDictionaryRequiredSupport( tK, tV, genTypeName ) );
                }
                return g.FullName;
            }
        }

        IPocoType? OnArray( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            Debug.Assert( nType.ElementType != null );

            // The oblivious array type is the array of its oblivious item type
            // (value type or nullable reference type).
            var tItem = Register( monitor, ctx, nType.ElementType );
            if( tItem == null ) return null;

            // When we are on a recursive named record like:
            //
            //  public record struct Rec( Rec[] R, int A );
            //
            // The Rec.ObliviousType is null (still unknown).
            if( tItem.ObliviousType == null )
            {
                Debug.Assert( tItem.Kind == PocoTypeKind.Record );
                // We need a trampoline here: this is not currently supported.
                monitor.Error( $"{ctx}: '{nType.Type:C}' Recursive named record definition is currently not supported." );
                return null;
            }

            if( !_cache.TryGetValue( nType.Type, out var obliviousType ) )
            {
                // The oblivious array is not registered.
                var oName = tItem.ObliviousType.CSharpName + "[]";
                obliviousType = PocoType.CreateCollection( monitor,
                                                         this,
                                                         nType.Type,
                                                         oName,
                                                         oName,
                                                         PocoTypeKind.Array,
                                                         tItem.ObliviousType,
                                                         null );
                _cache.Add( nType.Type, obliviousType );
            }
            // If the item is oblivious then, it is the oblivious array.
            if( tItem.IsOblivious ) return nType.IsNullable ? obliviousType.Nullable : obliviousType;

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
                                                    obliviousType );
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
            // The not nullable value type is registered in the cache and it is
            // necessarily the oblivious type (except for name record) that is registered (others are registered by name).
            // We are done for basic, enum and named record types but for anonymous record we must ensure
            // the registration of non oblivious types.
            if( _cache.TryGetValue( tNotNull, out var obliviousType ) )
            {
                Debug.Assert( !obliviousType.IsNullable );
                Debug.Assert( obliviousType.Type == tNotNull );
                Debug.Assert( obliviousType.Kind == PocoTypeKind.Record || obliviousType.IsOblivious && obliviousType.Nullable.IsOblivious );
                if( obliviousType.Kind == PocoTypeKind.Basic
                    || obliviousType.Kind == PocoTypeKind.Enum
                    || obliviousType.Kind == PocoTypeKind.Record )
                {
                    return nType.IsNullable ? obliviousType.Nullable : obliviousType;
                }
            }
            if( tNotNull.IsEnum )
            {
                if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalName ) )
                {
                    return null;
                }
                // There is necessary the underlying integral type.
                tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
                obliviousType = PocoType.CreateEnum( monitor,
                                                     this,
                                                     tNotNull,
                                                     tNull,
                                                     _cache[tNotNull.GetEnumUnderlyingType()],
                                                     externalName );
                _cache.Add( tNotNull, obliviousType );
                return nType.IsNullable ? obliviousType.Nullable : obliviousType;
            }
            // We first handle ValueTuple since we can easily detect them.
            if( tNotNull.IsValueTuple() )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nType.GenericTypeArguments.Count );
                Debug.Assert( obliviousType == null || obliviousType.Kind == PocoTypeKind.AnonymousRecord );
                // We may be on the oblivious type... But we have to check (and we may be on an already registered
                // anonymous record anyway: field names and types are the key).
                tNull ??= obliviousType?.Nullable.Type ?? typeof( Nullable<> ).MakeGenericType( tNotNull );
                return OnValueTypeAnonymousRecord( monitor, ctx, nType, tNotNull, tNull, (IRecordPocoType?)obliviousType );
            }
            // Other generic value types are not supported.
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

        IPocoType? OnValueTypeAnonymousRecord( IActivityMonitor monitor,
                                               MemberContext ctx,
                                               IExtNullabilityInfo nType,
                                               Type tNotNull,
                                               Type tNull,
                                               IRecordPocoType? obliviousType )
        {
            var subInfos = FlattenValueTuple( nType ).ToList();
            var fields = ctx.GetTupleNamedFields( subInfos.Count );
            // Here we can resolve the field types without fear of infinite recursion: value tuples
            // cannot be recursive by design.
            var b = StringBuilderPool.Get();
            b.Append( '(' );
            int idx = 0;
            foreach( var sub in subInfos )
            {
                var f = fields[idx++];
                var tF = Register( monitor, ctx, sub );
                if( tF == null ) return null;
                if( b.Length != 1 ) b.Append( ',' );
                b.Append( tF.CSharpName );
                if( !f.IsUnnamed ) b.Append( ' ' ).Append( f.Name );
                f.SetType( tF );
            }
            b.Append( ')' );
            // We have the registered type name.
            var typeName = b.ToString();
            if( obliviousType == null )
            {
                b.Clear();
                b.Append( '(' );
                RecordField[] obliviousFields = new RecordField[fields.Length];
                for( int i = 0; i < obliviousFields.Length; i++ )
                {
                    var f = new RecordField( i, null );
                    f.SetType( fields[i].Type );
                    obliviousFields[i] = f;
                    if( b.Length != 1 ) b.Append( ',' );
                    b.Append( f.Type.ObliviousType.CSharpName );
                }
                b.Append( ')' );
                var obliviousTypeName = b.ToString();
                obliviousType = PocoType.CreateRecord( monitor, this, tNotNull, tNull, obliviousTypeName, obliviousFields, null, null );
                _cache.Add( tNotNull, obliviousType );
                // If there's no field name and field types are the same as the oblivious ones,
                // we are done.
                if( typeName == obliviousTypeName )
                {
                    return nType.IsNullable ? obliviousType.Nullable : obliviousType;
                }
            }
            // Don't need the buffer anymore.
            StringBuilderPool.GetStringAndReturn( b );
            // Registers the non oblivious type if not already registered.
            if( !_cache.TryGetValue( typeName, out var result ) )
            {
                result = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, fields, null, obliviousType );
                _cache.Add( typeName, result );
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

        IPocoType? OnTypedRecord( IActivityMonitor monitor,
                                  MemberContext ctx,
                                  IExtNullabilityInfo nType,
                                  Type tNotNull,
                                  Type tNull )
        {
            // C#10 record struct are not decorated by any special attribute: we treat them like any other struct.
            // Allow only fully mutable struct: all its exposed properties and fields must be mutable.
            Debug.Assert( tNotNull.IsValueType );
            Debug.Assert( !_cache.ContainsKey( tNotNull ), "OnValueType found it." );

            // Named record can have an [ExternalName].
            if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalName ) )
            {
                return null;
            }

            var typeName = tNotNull.ToCSharpName();
            var r = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, null, externalName, null );
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
            bool hasNonObliviousFieldType = false;
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
                if( !f.Type.IsOblivious ) hasNonObliviousFieldType = true;
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
                if( !f.Type.IsOblivious ) hasNonObliviousFieldType = true;
                fields[idx++] = f;
            }
            PocoType.RecordType? obliviousType = null;
            if( hasNonObliviousFieldType )
            {
                var obliviousFields = new RecordField[fields.Length];
                for( int i = 0; i < fields.Length; i++ )
                {
                    obliviousFields[i] = new RecordField( fields[i] );
                }
                obliviousType = PocoType.CreateRecord( monitor, this, tNotNull, tNull, typeName, null, externalName, null );
                obliviousType.SetFields( monitor, this, obliviousFields, obliviousType );
            }
            r.SetFields( monitor, this, fields, obliviousType );
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
