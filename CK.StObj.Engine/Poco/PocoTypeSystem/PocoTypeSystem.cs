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
        // The oblivious caches has 2 keys:
        // - A Type is mapped to the oblivious IPocoType.
        // - A string key indexes the IPocoType.CSharpName for non oblivious types.
        readonly Dictionary<object, IPocoType> _obliviousCache;
        readonly IPocoType _objectType;
        readonly IPocoType _stringType;
        // Contains the not nullable types (PocoType instances are the non nullable types).
        readonly List<PocoType> _allTypes;
        readonly HalfTypeList _exposedAllTypes;
        readonly Stack<StringBuilder> _stringBuilderPool;
        readonly Dictionary<string, PocoRequiredSupportType> _requiredSupportTypes;
        bool _locked;

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
            _obliviousCache = new Dictionary<object, IPocoType>();
            RegValueType( this, _obliviousCache, typeof( bool ), typeof( bool? ), "bool" );
            RegValueType( this, _obliviousCache, typeof( int ), typeof( int? ), "int" );
            RegValueType( this, _obliviousCache, typeof( long ), typeof( long? ), "long" );
            RegValueType( this, _obliviousCache, typeof( short ), typeof( short? ), "short" );
            RegValueType( this, _obliviousCache, typeof( byte ), typeof( byte? ), "byte" );
            RegValueType( this, _obliviousCache, typeof( double ), typeof( double? ), "double" );
            RegValueType( this, _obliviousCache, typeof( float ), typeof( float? ), "float" );
            RegValueType( this, _obliviousCache, typeof( DateTime ), typeof( DateTime? ), "DateTime" );
            RegValueType( this, _obliviousCache, typeof( DateTimeOffset ), typeof( DateTimeOffset? ), "DateTimeOffset" );
            RegValueType( this, _obliviousCache, typeof( TimeSpan ), typeof( TimeSpan? ), "TimeSpan" );
            RegValueType( this, _obliviousCache, typeof( Guid ), typeof( Guid? ), "Guid" );
            RegValueType( this, _obliviousCache, typeof( decimal ), typeof( decimal? ), "decimal" );
            RegValueType( this, _obliviousCache, typeof( System.Numerics.BigInteger ), typeof( System.Numerics.BigInteger? ), "System.Numerics.BigInteger" );
            RegValueType( this, _obliviousCache, typeof( uint ), typeof( uint? ), "uint" );
            RegValueType( this, _obliviousCache, typeof( ulong ), typeof( ulong? ), "ulong" );
            RegValueType( this, _obliviousCache, typeof( ushort ), typeof( ushort? ), "ushort" );
            RegValueType( this, _obliviousCache, typeof( sbyte ), typeof( sbyte? ), "sbyte" );

            _objectType = PocoType.CreateBasicRef( this, typeof( object ), "object", PocoTypeKind.Any );
            _obliviousCache.Add( "object", _objectType );
            _obliviousCache.Add( _objectType.Type, _objectType );

            _stringType = PocoType.CreateBasicRef( this, typeof( string ), "string", PocoTypeKind.Basic );
            _obliviousCache.Add( "string", _stringType );
            _obliviousCache.Add( _stringType.Type, _stringType );

            static void RegValueType( PocoTypeSystem s, Dictionary<object, IPocoType> c, Type tNotNull, Type tNull, string name )
            {
                var x = PocoType.CreateBasicValue( s, tNotNull, tNull, name );
                c.Add( tNotNull, x );
                c.Add( tNull, x.Nullable );
            }
        }

        public bool IsLocked => _locked;

        public void Lock( IActivityMonitor monitor )
        {
            if( _locked )
            {
                monitor.Warn( $"TypeSystem is already locked with {_exposedAllTypes.Count} types." );
            }
            else
            {
                _locked = true;
                monitor.Warn( $"Locking TypeSystem with {_exposedAllTypes.Count} types." );
            }
        }

        public IPocoType ObjectType => _objectType;

        public IReadOnlyList<IPocoType> AllTypes => _exposedAllTypes;

        public IReadOnlyList<IPocoType> AllNonNullableTypes => _allTypes;

        public IReadOnlyCollection<PocoRequiredSupportType> RequiredSupportTypes => _requiredSupportTypes.Values;

        internal void AddNew( PocoType t )
        {
            Throw.CheckState( !IsLocked );
            Debug.Assert( t.Index == _allTypes.Count * 2 );
            _allTypes.Add( t );
        }

        public IPocoType? FindObliviousType( Type type )
        {
            return _obliviousCache.GetValueOrDefault( type );
        }

        public IPrimaryPocoType? GetPrimaryPocoType( Type i )
        {
            if( _obliviousCache.TryGetValue( i, out var result ) )
            {
                return result as IPrimaryPocoType;
            }
            return null;
        }

        public void SetNotExchangeable( IActivityMonitor monitor, IPocoType type )
        {
            Throw.CheckState( !IsLocked );
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckArgument( type != null && type.Index < _exposedAllTypes.Count && _exposedAllTypes[type.Index] == type );
            Throw.CheckArgument( type.Kind != PocoTypeKind.Any );
            var t = (PocoType)type.NonNullable;
            if( t.IsExchangeable )
            {
                t.SetNotExchangeable( monitor, "TypeSystem external call." );
            }
        }

        public IPocoType? RegisterNullOblivious( IActivityMonitor monitor, Type t ) => DoRegister( monitor, _memberInfoFactory.CreateNullOblivious( t ) );

        public IPocoType? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo ) => DoRegister( monitor, memberInfo );

        public IPocoType? Register( IActivityMonitor monitor, PropertyInfo p ) => DoRegister( monitor, _memberInfoFactory.Create( p ) );

        public IPocoType? Register( IActivityMonitor monitor, FieldInfo f ) => DoRegister( monitor, _memberInfoFactory.Create( f ) );

        public IPocoType? Register( IActivityMonitor monitor, ParameterInfo p ) => DoRegister( monitor, _memberInfoFactory.Create( p ) );

        IPocoType? DoRegister( IActivityMonitor monitor, IExtMemberInfo memberInfo )
        {
            var nType = memberInfo.GetHomogeneousNullabilityInfo( monitor );
            if( nType == null ) return null;
            return Register( monitor, new MemberContext( memberInfo ), nType );
        }

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
            if( _obliviousCache.TryGetValue( t, out var result ) )
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
                monitor.Error( $"Unsupported type: '{t}'." );
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
                var csharpName = isRegular
                                    ? $"{listOrHashSet}<{tI.CSharpName}>"
                                    : $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
                if( !_obliviousCache.TryGetValue( csharpName, out var result ) )
                {
                    Type? tOblivious = null;
                    string? typeName = null;
                    if( !isRegular )
                    {
                        // IList or ISet
                        if( tI.Type.IsValueType )
                        {
                            Debug.Assert( tI.CSharpName == tI.ImplTypeName, "Value types are implemented by themselves." );
                            // For value type item, use our covariant implementations.
                            // We use the Oblivious type name as a minor optimization for Roslyn here when the item
                            // is an anonymous record: instead of using the CSharName with its field names that will
                            // create useless TupleNamesAttribute, the oblivious has no field names.
                            if( tI.IsNullable )
                            {
                                typeName = $"CovariantHelpers.CovNullableValue{listOrHashSet}<{tI.NonNullable.ObliviousType.CSharpName}>";
                                t = (isList ? typeof( CovariantHelpers.CovNullableValueList<> ) : typeof( CovariantHelpers.CovNullableValueHashSet<> ))
                                    .MakeGenericType( tI.NonNullable.Type );
                            }
                            else
                            {
                                typeName = $"CovariantHelpers.CovNotNullValue{listOrHashSet}<{tI.ObliviousType.CSharpName}>";
                                t = (isList ? typeof( CovariantHelpers.CovNotNullValueList<> ) : (typeof( CovariantHelpers.CovNotNullValueHashSet<> )))
                                    .MakeGenericType( tI.Type );
                            }
                            // We are obviously not the oblivious.
                            tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                        }
                        else if( tI.Kind == PocoTypeKind.IPoco )
                        {
                            // For IPoco, use generated covariant implementations.
                            // We choose the non nullable item type to follow the C# "oblivious nullable reference type" that is non nullable. 
                            typeName = EnsurePocoListOrHashSetType( monitor, (IPrimaryPocoType)tI.NonNullable, isList, listOrHashSet );
                            t = IDynamicAssembly.PurelyGeneratedType;
                            // Since we are on a reference type, the oblivious is the non nullable.
                            Debug.Assert( tI.NonNullable.IsOblivious, "Non composed nullable reference types are their own oblivious type." );
                            // We are obviously not the oblivious.
                            tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
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
                        if( isRegular && tI.IsOblivious )
                        {
                            // We are building the oblivious: let the tOblivious be null.
                            typeName = csharpName;
                        }
                        else
                        {
                            tOblivious = t;
                            typeName = isRegular ? csharpName : $"{listOrHashSet}<{tI.CSharpName}>";
                        }
                    }
                    // Ensure that the obliviousType is registered if we are not instantiating it.
                    IPocoType? obliviousType = null;
                    if( tOblivious != null )
                    {
                        // The type we are about to create is not the oblivious one.
                        // However we have everything here to create it:
                        // - It has the same t, typeName and kind.
                        // - Its item type is tI.ObliviousType.
                        // - We used the tOblivious != null as a flag, so we have it.
                        if( !_obliviousCache.TryGetValue( tOblivious, out obliviousType ) )
                        {
                            var obliviousTypeName = $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>";
                            Debug.Assert( typeName != obliviousTypeName || !isRegular, "The only way for the typeName to be the oblivious one is if a IList<> is requested." );
                            obliviousType = PocoType.CreateCollection( monitor,
                                                                       this,
                                                                       tOblivious,
                                                                       obliviousTypeName,
                                                                       obliviousTypeName,
                                                                       isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                       tI.ObliviousType,
                                                                       null );
                            _obliviousCache.Add( tOblivious, obliviousType );
                            _obliviousCache.Add( obliviousTypeName, obliviousType );
                        }
                        Debug.Assert( obliviousType.IsOblivious && obliviousType.CSharpName == $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>" );
                    }
                    Debug.Assert( obliviousType != null || typeName == csharpName, "We have the oblivious type or we are creating it." );
                    result = PocoType.CreateCollection( monitor,
                                                        this,
                                                        t,
                                                        csharpName,
                                                        typeName,
                                                        isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                        tI,
                                                        obliviousType );
                    _obliviousCache.Add( csharpName, result );
                    // If we have built the oblivious, register it.
                    if( obliviousType == null )
                    {
                        Debug.Assert( result.IsOblivious );
                        Debug.Assert( csharpName == typeName );
                        _obliviousCache.Add( result.Type, result );
                    }
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

                var csharpName = isRegular
                                    ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                    : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
                if( !_obliviousCache.TryGetValue( csharpName, out var result ) )
                {
                    Type? tOblivious = null;
                    string? typeName = null;
                    if( !isRegular )
                    {
                        if( tV.Type.IsValueType )
                        {
                            if( tV.IsNullable )
                            {
                                typeName = $"CovariantHelpers.CovNullableValueDictionary<{tK.CSharpName},{tV.NonNullable.ObliviousType.CSharpName}>";
                                t = typeof( CovariantHelpers.CovNullableValueDictionary<,> ).MakeGenericType( tK.Type, tV.NonNullable.Type );
                            }
                            else
                            {
                                typeName = $"CovariantHelpers.CovNotNullValueDictionary<{tK.CSharpName},{tV.ObliviousType.CSharpName}>";
                                t = typeof( CovariantHelpers.CovNotNullValueDictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                            }
                            tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        }
                        else if( tV.Kind == PocoTypeKind.IPoco )
                        {
                            typeName = EnsurePocoDictionaryType( monitor, tK, (IPrimaryPocoType)tV.NonNullable );
                            t = IDynamicAssembly.PurelyGeneratedType;
                            tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
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
                            tOblivious = t;
                            typeName = isRegular ? csharpName : $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                        }
                    }
                    IPocoType? obliviousType = null;
                    if( tOblivious != null )
                    {
                        if( !_obliviousCache.TryGetValue( tOblivious, out obliviousType ) )
                        {
                            var obliviousTypeName = $"Dictionary<{tK.CSharpName},{tV.ObliviousType.CSharpName}>";
                            Debug.Assert( typeName != obliviousTypeName || !isRegular );
                            obliviousType = PocoType.CreateDictionary( monitor,
                                                                       this,
                                                                       tOblivious,
                                                                       obliviousTypeName,
                                                                       obliviousTypeName,
                                                                       tK,
                                                                       tV.ObliviousType,
                                                                       null );
                            _obliviousCache.Add( tOblivious, obliviousType );
                            _obliviousCache.Add( obliviousTypeName, obliviousType );
                        }
                        Debug.Assert( obliviousType.IsOblivious && obliviousType.CSharpName == $"Dictionary<{tK.CSharpName},{tV.ObliviousType.CSharpName}>" );
                    }
                    Debug.Assert( obliviousType != null || typeName == csharpName, "We have the oblivious type or we are creating it." );
                    result = PocoType.CreateDictionary( monitor,
                                                        this,
                                                        t,
                                                        csharpName,
                                                        typeName,
                                                        tK,
                                                        tV,
                                                        obliviousType );
                    _obliviousCache.Add( csharpName, result );
                    if( obliviousType == null )
                    {
                        Debug.Assert( result.IsOblivious );
                        Debug.Assert( csharpName == typeName );
                        _obliviousCache.Add( result.Type, result );
                    }
                }
                return nType.IsNullable ? result.Nullable : result;
            }

            string EnsurePocoListOrHashSetType( IActivityMonitor monitor, IPrimaryPocoType tI, bool isList, string listOrHasSet )
            {
                Debug.Assert( !tI.IsNullable );
                var genTypeName = $"Poco{listOrHasSet}_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoListOrHashSetRequiredSupport( tI, genTypeName, isList ) );
                }
                return g.FullName;
            }

            string EnsurePocoDictionaryType( IActivityMonitor monitor, IPocoType tK, IPrimaryPocoType tV )
            {
                Debug.Assert( !tV.IsNullable );
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

            var chsarpName = tItem.CSharpName + "[]";
            if( !_obliviousCache.TryGetValue( chsarpName, out var result ) )
            {
                if( !_obliviousCache.TryGetValue( nType.Type, out var obliviousType ) )
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
                    _obliviousCache.Add( nType.Type, obliviousType );
                    _obliviousCache.Add( oName, obliviousType );
                }
                // If the item is oblivious then, it is the oblivious array.
                if( tItem.IsOblivious ) return nType.IsNullable ? obliviousType.Nullable : obliviousType;

                result = PocoType.CreateCollection( monitor,
                                                    this,
                                                    nType.Type,
                                                    chsarpName,
                                                    chsarpName,
                                                    PocoTypeKind.Array,
                                                    tItem,
                                                    obliviousType );
                _obliviousCache.Add( chsarpName, result );
            }
            return nType.IsNullable ? result.Nullable : result;
        }

        IPocoType? OnValueType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            // Unwrap the nullable value type (or wrap): we reason only on non nullable value types.
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
            // necessarily the oblivious type (except for named record).
            // If we found it, we are done for basic, enum and named record types but for anonymous
            // record we must handle the field names.
            if( _obliviousCache.TryGetValue( tNotNull, out var obliviousType ) )
            {
                Debug.Assert( !obliviousType.IsNullable );
                Debug.Assert( obliviousType.Type == tNotNull );
                Debug.Assert( obliviousType.Kind == PocoTypeKind.Record || (obliviousType.IsOblivious && obliviousType.Nullable.IsOblivious) );
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
                                                     _obliviousCache[tNotNull.GetEnumUnderlyingType()],
                                                     externalName );
                _obliviousCache.Add( tNotNull, obliviousType );
                _obliviousCache.Add( tNull, obliviousType.Nullable );
                return nType.IsNullable ? obliviousType.Nullable : obliviousType;
            }
            // We first handle ValueTuple since we can easily detect them.
            if( tNotNull.IsValueTuple() )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nType.GenericTypeArguments.Count );
                Debug.Assert( obliviousType == null || obliviousType.Kind == PocoTypeKind.AnonymousRecord );
                // We may be on the oblivious type... But we have to check (and we may be on an already registered
                // anonymous record anyway: field names are the keys).
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
            // We can detect that this is the oblivious one: it must have no field names.
            bool isOblivious = true;
            var b = StringBuilderPool.Get();
            b.Append( '(' );
            int idx = 0;
            foreach( var sub in subInfos )
            {
                var f = fields[idx++];
                var fType = Register( monitor, ctx, sub );
                if( fType == null ) return null;
                if( b.Length != 1 ) b.Append( ',' );
                b.Append( fType.CSharpName );
                if( !f.IsUnnamed ) b.Append( ' ' ).Append( f.Name );
                isOblivious &= f.IsUnnamed && fType.IsOblivious;
                f.SetType( fType );
            }
            b.Append( ')' );
            // If this happens to be the oblivious type... 
            if( isOblivious )
            {
                Debug.Assert( obliviousType == null || b.ToString() == obliviousType.CSharpName );
                if( obliviousType == null )
                {
                    // We build it.
                    var obliviousName = b.ToString();
                    obliviousType = PocoType.CreateAnonymousRecord( monitor, this, tNotNull, tNull, obliviousName, fields, null );
                    _obliviousCache.Add( tNotNull, obliviousType );
                    _obliviousCache.Add( tNull, obliviousType.Nullable );
                }
                // We are done.
                StringBuilderPool.GetStringAndReturn( b );
                return nType.IsNullable ? obliviousType.Nullable : obliviousType;
            }
            // We have the registered type name.
            var typeName = b.ToString();
            // Check the cache for it.
            if( _obliviousCache.TryGetValue( typeName, out var result ) )
            {
                StringBuilderPool.GetStringAndReturn( b );
                return nType.IsNullable ? result.Nullable : result;
            }
            // We now must ensure the oblivious type since we are not instantiating it.
            if( obliviousType == null )
            {
                var obliviousFields = new RecordAnonField[fields.Length];
                foreach( var f in fields )
                {
                    obliviousFields[f.Index] = new RecordAnonField( f );
                    Debug.Assert( obliviousFields[f.Index].IsUnnamed && obliviousFields[f.Index].Type.IsOblivious );
                }
                var tNotNullOblivious = CreateValueTuple( obliviousFields, 0 );
                if( _obliviousCache.TryGetValue( tNotNullOblivious, out var exist ) )
                {
                    obliviousType = (IRecordPocoType)exist;
                }
                else
                {
                    b.Clear();
                    b.Append( '(' );
                    foreach( var f in obliviousFields )
                    {
                        if( b.Length != 1 ) b.Append( ',' );
                        b.Append( f.Type.CSharpName );
                    }
                    b.Append( ')' );
                    var obliviousName = b.ToString();
                    var tNullOblivious = typeof( Nullable<> ).MakeGenericType( tNotNullOblivious );
                    Debug.Assert( obliviousName != typeName );
                    obliviousType = PocoType.CreateAnonymousRecord( monitor, this, tNotNullOblivious, tNullOblivious, obliviousName, obliviousFields, null );
                    _obliviousCache.Add( tNotNullOblivious, obliviousType );
                    _obliviousCache.Add( tNullOblivious, obliviousType.Nullable );
                }
            }
            // Don't need the buffer anymore.
            StringBuilderPool.GetStringAndReturn( b );

            result = PocoType.CreateAnonymousRecord( monitor, this, tNotNull, tNull, typeName, fields, obliviousType );
            _obliviousCache.Add( typeName, result );
            return nType.IsNullable ? result.Nullable : result;

            static IEnumerable<IExtNullabilityInfo> FlattenValueTuple( IExtNullabilityInfo nType )
            {
                Debug.Assert( nType.Type.IsValueType && (nType.IsNullable || nType.Type.IsValueTuple()) );
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
                    else
                    {
                        yield return info;
                    }
                }
            }

            static Type CreateValueTuple( IReadOnlyList<IPocoField> fields, int offset )
            {
                return (fields.Count - offset) switch
                {
                    1 => typeof( ValueTuple<> ).MakeGenericType( fields[offset].Type.Type ),
                    2 => typeof( ValueTuple<,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type ),
                    3 => typeof( ValueTuple<,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type ),
                    4 => typeof( ValueTuple<,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type ),
                    5 => typeof( ValueTuple<,,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type, fields[offset + 4].Type.Type ),
                    6 => typeof( ValueTuple<,,,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type, fields[offset + 4].Type.Type, fields[offset + 5].Type.Type ),
                    7 => typeof( ValueTuple<,,,,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type, fields[offset + 4].Type.Type, fields[offset + 5].Type.Type, fields[offset + 6].Type.Type ),
                    >= 8 => typeof( ValueTuple<,,,,,,,> ).MakeGenericType( fields[offset].Type.Type,
                                                                           fields[offset + 1].Type.Type,
                                                                           fields[offset + 2].Type.Type,
                                                                           fields[offset + 3].Type.Type,
                                                                           fields[offset + 4].Type.Type,
                                                                           fields[offset + 5].Type.Type,
                                                                           fields[offset + 6].Type.Type,
                                                                           CreateValueTuple( fields, offset + 7 ) ),
                    _ => Throw.NotSupportedException<Type>()
                };
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
            Debug.Assert( !_obliviousCache.ContainsKey( tNotNull ), "OnValueType found it." );

            // Named record can have an [ExternalName].
            if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalName ) )
            {
                return null;
            }

            // We first create the type and register it without its fields: fields
            // may recursively use this type.
            var typeName = tNotNull.ToCSharpName();
            var r = PocoType.CreateNamedRecord( monitor, this, tNotNull, tNull, typeName, externalName );
            _obliviousCache.Add( tNotNull, r );
            _obliviousCache.Add( tNull, r.Nullable );

            // Currently allows only a single constructor. This is not good: we should allow for instance deserialization constructor...
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
            var fields = new RecordNamedField[propertyInfos.Length + fieldInfos.Length];
            for( int i = 0; i < propertyInfos.Length; i++ )
            {
                var pInfo = _memberInfoFactory.Create( propertyInfos[i] );
                if( !pInfo.PropertyInfo.CanWrite && !pInfo.Type.IsByRef )
                {
                    monitor.Error( $"'{pInfo}' is readonly. A record struct must be fully mutable." );
                    return null;
                }
                var f = CreateField( monitor, r, i, DoRegister( monitor, pInfo ), pInfo, ctorParams, StringBuilderPool );
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
                var f = CreateField( monitor, r, idx, DoRegister( monitor, fInfo ), fInfo, ctorParams, StringBuilderPool );
                if( f == null ) return null;
                fields[idx++] = f;
            }
            r.SetFields( monitor, this, fields );
            return nType.IsNullable ? r.Nullable : r;


            static RecordNamedField? CreateField( IActivityMonitor monitor,
                                                  PocoType.RecordNamedType r,
                                                  int idx,
                                                  IPocoType? tField,
                                                  IExtMemberInfo fInfo,
                                                  ParameterInfo[]? ctorParams,
                                                  IStringBuilderPool stringPool )
            {
                if( tField == null ) return null;
                var defValue = FieldDefaultValue.CreateFromAttribute( monitor, stringPool, fInfo );
                if( defValue == null && ctorParams != null )
                {
                    var p = ctorParams.FirstOrDefault( p => p.Name == fInfo.Name );
                    if( p != null ) defValue = FieldDefaultValue.CreateFromParameter( monitor, stringPool, p );
                }
                return new RecordNamedField( r, idx, fInfo.Name, tField, defValue );
            }

        }

        IUnionPocoType RegisterUnionType( IActivityMonitor monitor, List<IPocoType> types )
        {
            var a = types.ToArray();
            var k = new PocoType.KeyUnionTypes( a, out bool isOblivious );
            IPocoType? obliviousType = null;
            if( !isOblivious )
            {
                var aO = new IPocoType[a.Length];
                for( int i = 0; i < a.Length; i++ )
                {
                    aO[i] = a[i].ObliviousType;
                }
                var kO = new PocoType.KeyUnionTypes( aO, out var _ );
                Debug.Assert( !kO.Equals( k ) );
                if( !_obliviousCache.TryGetValue( kO, out obliviousType ) )
                {
                    obliviousType = PocoType.CreateUnion( monitor, this, aO, null );
                    _obliviousCache.Add( kO, obliviousType );
                }
            }
            if( !_obliviousCache.TryGetValue( k, out var result ) )
            {
                result = PocoType.CreateUnion( monitor, this, a, obliviousType );
                _obliviousCache.Add( k, result );
            }
            return (IUnionPocoType)result;
        }

    }

}
