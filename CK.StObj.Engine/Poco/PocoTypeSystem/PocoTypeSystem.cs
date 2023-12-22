using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Implementation of <see cref="IPocoTypeSystem"/>.
    /// </summary>
    public sealed partial class PocoTypeSystem : IPocoTypeSystem
    {
        readonly IExtMemberInfoFactory _memberInfoFactory;
        // The oblivious caches has 2 types of keys:
        // - Type:
        //      - A type is mapped to the oblivious IPocoType
        //      - Or is secondary IPoco interface type mapped to its ISecondaryPocoType (that is not oblivious).
        // - String:
        //      - A string key indexes the IPocoType.CSharpName for all types (oblivious or not): nullabilties appear in the key.
        readonly Dictionary<object, IPocoType> _typeCache;
        readonly IPocoType _objectType;
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
            _typeCache = new Dictionary<object, IPocoType>();
            RegValueType( this, _typeCache, typeof( bool ), typeof( bool? ), "bool" );
            RegValueType( this, _typeCache, typeof( int ), typeof( int? ), "int" );
            RegValueType( this, _typeCache, typeof( long ), typeof( long? ), "long" );
            RegValueType( this, _typeCache, typeof( short ), typeof( short? ), "short" );
            RegValueType( this, _typeCache, typeof( byte ), typeof( byte? ), "byte" );
            RegValueType( this, _typeCache, typeof( double ), typeof( double? ), "double" );
            RegValueType( this, _typeCache, typeof( float ), typeof( float? ), "float" );
            RegValueType( this, _typeCache, typeof( DateTime ), typeof( DateTime? ), "DateTime" );
            RegValueType( this, _typeCache, typeof( DateTimeOffset ), typeof( DateTimeOffset? ), "DateTimeOffset" );
            RegValueType( this, _typeCache, typeof( TimeSpan ), typeof( TimeSpan? ), "TimeSpan" );
            RegValueType( this, _typeCache, typeof( Guid ), typeof( Guid? ), "Guid" );
            RegValueType( this, _typeCache, typeof( decimal ), typeof( decimal? ), "decimal" );
            RegValueType( this, _typeCache, typeof( System.Numerics.BigInteger ), typeof( System.Numerics.BigInteger? ), "System.Numerics.BigInteger" );
            RegValueType( this, _typeCache, typeof( uint ), typeof( uint? ), "uint" );
            RegValueType( this, _typeCache, typeof( ulong ), typeof( ulong? ), "ulong" );
            RegValueType( this, _typeCache, typeof( ushort ), typeof( ushort? ), "ushort" );
            RegValueType( this, _typeCache, typeof( sbyte ), typeof( sbyte? ), "sbyte" );
            RegValueType( this, _typeCache, typeof( SimpleUserMessage ), typeof( SimpleUserMessage? ), "SimpleUserMessage" );
            RegValueType( this, _typeCache, typeof( UserMessage ), typeof( UserMessage? ), "UserMessage" );
            RegValueType( this, _typeCache, typeof( FormattedString ), typeof( FormattedString? ), "FormattedString" );
            
            static void RegValueType( PocoTypeSystem s, Dictionary<object, IPocoType> c, Type tNotNull, Type tNull, string name )
            {
                var x = PocoType.CreateBasicValue( s, tNotNull, tNull, name );
                c.Add( tNotNull, x );
                c.Add( tNull, x.Nullable );
            }

            _objectType = PocoType.CreateObject( this );
            _typeCache.Add( "object", _objectType );
            _typeCache.Add( _objectType.Type, _objectType );

            RegReferenceType( this, _typeCache, typeof( string ), "string", FieldDefaultValue.StringDefault );
            RegReferenceType( this, _typeCache, typeof( ExtendedCultureInfo ), "ExtendedCultureInfo", FieldDefaultValue.CultureDefault );
            RegReferenceType( this, _typeCache, typeof( NormalizedCultureInfo ), "NormalizedCultureInfo", FieldDefaultValue.CultureDefault );
            RegReferenceType( this, _typeCache, typeof( MCString ), "MCString", FieldDefaultValue.MCStringDefault );
            RegReferenceType( this, _typeCache, typeof( CodeString ), "CodeString", FieldDefaultValue.CodeStringDefault );

            static void RegReferenceType( PocoTypeSystem s, Dictionary<object, IPocoType> c, Type t, string name, FieldDefaultValue defaultValue )
            {
                var x = PocoType.CreateBasicRef( s, t, name, defaultValue );
                c.Add( t, x );
                c.Add( name, x );
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
            Throw.DebugAssert( t.Index == _allTypes.Count * 2 );
            _allTypes.Add( t );
        }

        public IPocoType? FindByType( Type type )
        {
            return _typeCache.GetValueOrDefault( type );
        }

        public T? FindByType<T>( Type type ) where T : class, IPocoType
        {
            return _typeCache.GetValueOrDefault( type ) as T;
        }

        public void SetNotExchangeable( IActivityMonitor monitor, IPocoType type )
        {
            Throw.CheckState( !IsLocked );
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckArgument( type != null && type.Index < _exposedAllTypes.Count && _exposedAllTypes[type.Index] == type );
            Throw.CheckArgument( type.Kind != PocoTypeKind.Any );
            Throw.CheckArgument( "Only the PrimaryPoco can be set to be not exchangeable.", type.Kind != PocoTypeKind.SecondaryPoco );
            var t = (PocoType)type.NonNullable;
            if( t.IsExchangeable )
            {
                t.SetNotExchangeable( monitor, "TypeSystem external call." );
            }
        }

        public IPocoType? RegisterNullOblivious( IActivityMonitor monitor, Type t ) => Register( monitor, _memberInfoFactory.CreateNullOblivious( t ) );

        public IPocoType? Register( IActivityMonitor monitor, PropertyInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        public IPocoType? Register( IActivityMonitor monitor, FieldInfo f ) => Register( monitor, _memberInfoFactory.Create( f ) );

        public IPocoType? Register( IActivityMonitor monitor, ParameterInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        public IPocoType? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo )
        {
            var nType = memberInfo.GetHomogeneousNullabilityInfo( monitor );
            if( nType == null ) return null;
            return Register( monitor, new MemberContext( memberInfo ), nType );
        }

        internal IPocoType? RegisterPocoField( IActivityMonitor monitor, IExtMemberInfo memberInfo )
        {
            var nType = memberInfo.GetHomogeneousNullabilityInfo( monitor );
            if( nType == null ) return null;
            return Register( monitor, new MemberContext( memberInfo, true ), nType );
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
            if( _typeCache.TryGetValue( t, out var result ) )
            {
                Throw.DebugAssert( !result.IsNullable );
                Throw.DebugAssert( result.Kind == PocoTypeKind.Any
                                   || result.Kind == PocoTypeKind.PrimaryPoco
                                   || result.Kind == PocoTypeKind.AbstractPoco
                                   || result.Kind == PocoTypeKind.SecondaryPoco
                                   // Allowed BasicTypes that are reference types.
                                   || result.Type == typeof( string )
                                   || result.Type == typeof( ExtendedCultureInfo )
                                   || result.Type == typeof( NormalizedCultureInfo )
                                   || result.Type == typeof( MCString )
                                   || result.Type == typeof( CodeString ) );
                return nType.IsNullable ? result.Nullable : result;
            }
            // If it's a IPoco we should have found it: it has been excluded or not registered.
            if( typeof( IPoco ).IsAssignableFrom( t ) )
            {
                monitor.Error( $"IPoco '{t}' has been excluded or not registered." );
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
                var listOrHashSet = isList ? "List" : "HashSet";
                bool valid = ctx.EnterListSetOrDictionary( monitor, nType, isRegular, listOrHashSet );

                var tI = Register( monitor, ctx, nType.GenericTypeArguments[0] );
                if( tI == null || !valid ) return null;

                var csharpName = isRegular
                                    ? $"{listOrHashSet}<{tI.CSharpName}>"
                                    : $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
                if( !_typeCache.TryGetValue( csharpName, out var result ) )
                {
                    Type? tOblivious = null;
                    string? typeName = null;
                    if( !isRegular )
                    {
                        // IList or ISet
                        if( tI.Type.IsValueType )
                        {
                            Throw.DebugAssert( "Value types are implemented by themselves.", tI.CSharpName == tI.ImplTypeName );
                            // For value type item, use our covariant implementations.
                            // We use the Oblivious type name as a minor optimization for Roslyn here when the item
                            // is an anonymous record: instead of using the CSharName with its field names that will
                            // create useless TupleNamesAttribute, the oblivious has no field names.
                            if( tI.IsNullable )
                            {
                                typeName = $"CovariantHelpers.CovNullableValue{listOrHashSet}<{tI.NonNullable.ObliviousType.ImplTypeName}>";
                                t = (isList ? typeof( CovariantHelpers.CovNullableValueList<> ) : typeof( CovariantHelpers.CovNullableValueHashSet<> ))
                                    .MakeGenericType( tI.NonNullable.Type );
                            }
                            else
                            {
                                typeName = $"CovariantHelpers.CovNotNullValue{listOrHashSet}<{tI.ObliviousType.ImplTypeName}>";
                                t = (isList ? typeof( CovariantHelpers.CovNotNullValueList<> ) : (typeof( CovariantHelpers.CovNotNullValueHashSet<> )))
                                    .MakeGenericType( tI.Type );
                            }
                            // We are obviously not the oblivious.
                            tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                        }
                        else if( tI.Kind == PocoTypeKind.AbstractPoco )
                        {
                            // HashSet<> is not natively covariant. We support it here.
                            if( !isList )
                            {
                                typeName = EnsurePocoHashSetOfAbstractType( monitor, (IAbstractPocoType)tI.NonNullable );
                                t = IDynamicAssembly.PurelyGeneratedType;
                                // We are obviously not the oblivious.
                                tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                            }
                        }
                        else
                        {
                            bool isSecondary = tI.Kind == PocoTypeKind.SecondaryPoco;
                            if( isSecondary || tI.Kind == PocoTypeKind.PrimaryPoco )
                            {
                                // For IPoco, use generated covariant implementations only if needed:
                                // - For list only if more than one Poco interface exist in the family. When the family contains only one interface
                                //   (the primary one), the oblivious List<PrimaryInterface> is fine.
                                // - But it's not the case for Set because IReadOnlySet<T> is NOT covariant. We need the object and abstract adaptations...
                                var poco = isSecondary
                                            ? ((ISecondaryPocoType)tI.NonNullable).PrimaryPocoType
                                            : (IPrimaryPocoType)tI.NonNullable;
                                if( !isList || isSecondary || poco.FamilyInfo.Interfaces.Count > 1 )
                                {
                                    // We choose the non nullable item type to follow the C# "oblivious nullable reference type" that is non nullable. 
                                    typeName = EnsurePocoListOrHashSetType( monitor, poco, isList, listOrHashSet );
                                    t = IDynamicAssembly.PurelyGeneratedType;
                                    // Since we are on a reference type, the oblivious is the non nullable.
                                    Debug.Assert( poco.IsOblivious );
                                    // We are obviously not the oblivious.
                                    tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                                }
                            }
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
                        // - It has the same kind.
                        // - Its typeName is its obliviousTypeName.
                        // - Its item type is tI.ObliviousType.
                        // - We used the tOblivious != null as a flag, so we have it.
                        if( !_typeCache.TryGetValue( tOblivious, out obliviousType ) )
                        {
                            var obliviousTypeName = $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>";
                            Throw.DebugAssert( "The only way for the typeName to be the oblivious one here is if a IList<> or ISet<> is requested.",
                                               typeName != obliviousTypeName || !isRegular );
                            obliviousType = PocoType.CreateCollection( monitor,
                                                                       this,
                                                                       tOblivious,
                                                                       obliviousTypeName,
                                                                       obliviousTypeName,
                                                                       isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                       itemType: tI.ObliviousType,
                                                                       obliviousType: null );
                            _typeCache.Add( tOblivious, obliviousType );
                            _typeCache.Add( obliviousTypeName, obliviousType );
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
                    _typeCache.Add( csharpName, result );
                    // If we have built the oblivious, register it.
                    if( obliviousType == null )
                    {
                        Throw.DebugAssert( result.IsOblivious && csharpName == typeName );
                        _typeCache.Add( result.Type, result );
                    }
                }
                return nType.IsNullable ? result.Nullable : result;

            }

            IPocoType? RegisterDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, Type t, bool isRegular )
            {
                bool valid = ctx.EnterListSetOrDictionary( monitor, nType, isRegular, "Dictionary" );
                var tK = Register( monitor, ctx, nType.GenericTypeArguments[0] );
                if( tK == null ) return null;
                if( tK.IsNullable )
                {
                    monitor.Error( $"{ctx}: '{nType.Type:C}' key cannot be nullable. Nullable type '{tK.CSharpName}' cannot be a key." );
                    return null;
                }
                var tV = Register( monitor, ctx, nType.GenericTypeArguments[1] );
                if( tV == null || !valid ) return null;

                var csharpName = isRegular
                                    ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                    : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
                if( !_typeCache.TryGetValue( csharpName, out var result ) )
                {
                    Type? tOblivious = null;
                    string? typeName = null;
                    if( !isRegular )
                    {
                        if( tV.Type.IsValueType )
                        {
                            Throw.DebugAssert( "Value types are implemented by themselves.", tV.ImplTypeName == tV.CSharpName );
                            if( tV.IsNullable )
                            {
                                typeName = $"CovariantHelpers.CovNullableValueDictionary<{tK.ImplTypeName},{tV.NonNullable.ObliviousType.ImplTypeName}>";
                                t = typeof( CovariantHelpers.CovNullableValueDictionary<,> ).MakeGenericType( tK.Type, tV.NonNullable.Type );
                            }
                            else
                            {
                                typeName = $"CovariantHelpers.CovNotNullValueDictionary<{tK.ImplTypeName},{tV.ObliviousType.ImplTypeName}>";
                                t = typeof( CovariantHelpers.CovNotNullValueDictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                            }
                            tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        }
                        else if( tV.Kind == PocoTypeKind.AbstractPoco )
                        {
                            // IReadOnlyDictionary<TKey,TValue> is NOT convariant on TValue: we always need an adapter.
                            typeName = EnsurePocoDictionaryOfAbstractType( monitor, tK, (IAbstractPocoType)tV.NonNullable );
                            t = IDynamicAssembly.PurelyGeneratedType;
                            tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        }
                        else 
                        {
                            bool isSecondary = tV.Kind == PocoTypeKind.SecondaryPoco;
                            if( isSecondary || tV.Kind == PocoTypeKind.PrimaryPoco )
                            {
                                // The adapter enables Primary and Secondary inputs and AbstractPoco outputs.
                                var poco = isSecondary
                                                ? ((ISecondaryPocoType)tV.NonNullable).PrimaryPocoType
                                                : (IPrimaryPocoType)tV.NonNullable;
                                typeName = EnsurePocoDictionaryType( monitor, tK, poco );
                                t = IDynamicAssembly.PurelyGeneratedType;
                                tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                            }
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
                        if( !_typeCache.TryGetValue( tOblivious, out obliviousType ) )
                        {
                            var obliviousTypeName = $"Dictionary<{tK.CSharpName},{tV.ObliviousType.CSharpName}>";
                            Throw.DebugAssert( "The only way for the typeName to be the oblivious one here is if a IDictionary<,> is requested.",
                                               typeName != obliviousTypeName || !isRegular );
                            obliviousType = PocoType.CreateDictionary( monitor,
                                                                       this,
                                                                       tOblivious,
                                                                       obliviousTypeName,
                                                                       obliviousTypeName,
                                                                       tK,
                                                                       tV.ObliviousType,
                                                                       null );
                            _typeCache.Add( tOblivious, obliviousType );
                            _typeCache.Add( obliviousTypeName, obliviousType );
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
                    _typeCache.Add( csharpName, result );
                    if( obliviousType == null )
                    {
                        Throw.DebugAssert( result.IsOblivious && csharpName == typeName );
                        _typeCache.Add( result.Type, result );
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

            string EnsurePocoHashSetOfAbstractType( IActivityMonitor monitor, IAbstractPocoType tI )
            {
                Debug.Assert( !tI.IsNullable );
                var genTypeName = $"PocoHashSet_{tI.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoHashSetOfAbstractRequiredSupport( tI, genTypeName ) );
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

            string EnsurePocoDictionaryOfAbstractType( IActivityMonitor monitor, IPocoType tK, IAbstractPocoType tV )
            {
                Debug.Assert( !tV.IsNullable );
                var genTypeName = $"PocoDictionary_{tK.Index}_{tV.Index}_CK";
                if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
                {
                    _requiredSupportTypes.Add( genTypeName, g = new PocoDictionaryOfAbstractRequiredSupport( tK, tV, genTypeName ) );
                }
                return g.FullName;
            }
        }

        IPocoType? OnArray( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            Debug.Assert( nType.ElementType != null );

            bool valid = ctx.EnterArray( monitor, nType );

            // The oblivious array type is the array of its oblivious item type
            // (value type or nullable reference type).
            var tItem = Register( monitor, ctx, nType.ElementType );
            if( tItem == null || !valid ) return null;

            var chsarpName = tItem.CSharpName + "[]";
            if( !_typeCache.TryGetValue( chsarpName, out var result ) )
            {
                if( !_typeCache.TryGetValue( nType.Type, out var obliviousType ) )
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
                    _typeCache.Add( nType.Type, obliviousType );
                    _typeCache.Add( oName, obliviousType );
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
                _typeCache.Add( chsarpName, result );
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
            if( _typeCache.TryGetValue( tNotNull, out var obliviousType ) )
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
                                                     _typeCache[tNotNull.GetEnumUnderlyingType()],
                                                     externalName );
                _typeCache.Add( tNotNull, obliviousType );
                _typeCache.Add( tNull, obliviousType.Nullable );
                return nType.IsNullable ? obliviousType.Nullable : obliviousType;
            }
            IPocoType? record;
            // We first handle ValueTuple since we can easily detect them.
            if( tNotNull.IsValueTuple() )
            {
                Debug.Assert( tNotNull.GetGenericArguments().Length == nType.GenericTypeArguments.Count );
                Debug.Assert( obliviousType == null || obliviousType.Kind == PocoTypeKind.AnonymousRecord );
                // We may be on the oblivious type... But we have to check (and we may be on an already registered
                // anonymous record anyway: field names are the keys).
                tNull ??= obliviousType?.Nullable.Type ?? typeof( Nullable<> ).MakeGenericType( tNotNull );
                record = OnValueTypeAnonymousRecord( monitor, ctx, nType, tNotNull, tNull, (IRecordPocoType?)obliviousType );
            }
            else
            {
                // Other generic value types are not supported.
                if( tNotNull.IsGenericType )
                {
                    Debug.Assert( tNotNull.GetGenericArguments().Length == nType.GenericTypeArguments.Count );
                    monitor.Error( $"Generic value type cannot be a Poco type: {ctx}." );
                    return null;
                }
                // Last chance: may be a new "record struct".
                tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
                record = OnTypedRecord( monitor, ctx, nType, tNotNull, tNull );
            }
            Throw.DebugAssert( record is null or IRecordPocoType );
            return record;
        }

        IPocoType? OnValueTypeAnonymousRecord( IActivityMonitor monitor,
                                               MemberContext ctx,
                                               IExtNullabilityInfo nType,
                                               Type tNotNull,
                                               Type tNull,
                                               IRecordPocoType? obliviousType )
        {
            var subInfos = FlattenValueTuple( nType ).ToList();
            var fields = ctx.EnterValueTuple( subInfos.Count, out var state );
            // Here we can resolve the field types without fear of infinite recursion: value tuples
            // cannot be recursive by design.
            // We can detect that this is the oblivious one: it must have no field names and its fields are oblivious.
            // We also compute whether this is a read only type.
            bool isReadOnlyCompliant = true;
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
                isReadOnlyCompliant &= MemberContext.IsReadOnlyCompliant( f );
            }
            b.Append( ')' );
            ctx.LeaveValueTuple( state );
            // If this happens to be the oblivious type... 
            if( isOblivious )
            {
                Debug.Assert( obliviousType == null || b.ToString() == obliviousType.CSharpName );
                if( obliviousType == null )
                {
                    // We build it.
                    var obliviousName = b.ToString();
                    obliviousType = PocoType.CreateAnonymousRecord( monitor, this, tNotNull, tNull, obliviousName, fields, isReadOnlyCompliant, null );
                    _typeCache.Add( tNotNull, obliviousType );
                    _typeCache.Add( tNull, obliviousType.Nullable );
                }
                // We are done.
                StringBuilderPool.GetStringAndReturn( b );
                return nType.IsNullable ? obliviousType.Nullable : obliviousType;
            }
            // We have the registered type name.
            var typeName = b.ToString();
            // Check the cache for it.
            if( _typeCache.TryGetValue( typeName, out var result ) )
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
                if( _typeCache.TryGetValue( tNotNullOblivious, out var exist ) )
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
                    obliviousType = PocoType.CreateAnonymousRecord( monitor, this, tNotNullOblivious, tNullOblivious, obliviousName, obliviousFields, isReadOnlyCompliant, null );
                    _typeCache.Add( tNotNullOblivious, obliviousType );
                    _typeCache.Add( tNullOblivious, obliviousType.Nullable );
                }
            }
            // Don't need the buffer anymore.
            StringBuilderPool.GetStringAndReturn( b );

            result = PocoType.CreateAnonymousRecord( monitor, this, tNotNull, tNull, typeName, fields, isReadOnlyCompliant, obliviousType );
            _typeCache.Add( typeName, result );
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
            Debug.Assert( !_typeCache.ContainsKey( tNotNull ), "OnValueType found it." );

            // Named record can have an [ExternalName].
            if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalName ) )
            {
                return null;
            }

            // We first create the type and register it without its fields: fields
            // may recursively use this type.
            var typeName = tNotNull.ToCSharpName();
            var r = PocoType.CreateNamedRecord( monitor, this, tNotNull, tNull, typeName, externalName );
            _typeCache.Add( tNotNull, r );
            _typeCache.Add( tNull, r.Nullable );

            // Currently allows only a single constructor. This is not good: we should allow for instance deserialization constructor...
            // We should try to consider a constructor whose parameter names are the fields/property names only (and only them).
            var ctors = tNotNull.GetConstructors();
            if( ctors.Length > 1 )
            {
                monitor.Error( $"More than one constructor found for record struct '{tNotNull}': at most one constructor must be specified for record struct." );
                return null;
            }
            var ctorParams = ctors.Length == 1 ? ctors[0].GetParameters() : null;

            bool isReadOnlyCompliant = true;
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
                var f = CreateField( monitor, r, i, Register( monitor, pInfo ), pInfo, ctorParams, StringBuilderPool );
                if( f == null ) return null;
                isReadOnlyCompliant &= MemberContext.IsReadOnlyCompliant( f );
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
                var f = CreateField( monitor, r, idx, Register( monitor, fInfo ), fInfo, ctorParams, StringBuilderPool );
                if( f == null ) return null;
                isReadOnlyCompliant &= MemberContext.IsReadOnlyCompliant( f );
                fields[idx++] = f;
            }
            r.SetFields( monitor, this, isReadOnlyCompliant, fields );
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
                if( !_typeCache.TryGetValue( kO, out obliviousType ) )
                {
                    obliviousType = PocoType.CreateUnion( monitor, this, aO, null );
                    _typeCache.Add( kO, obliviousType );
                }
            }
            if( !_typeCache.TryGetValue( k, out var result ) )
            {
                result = PocoType.CreateUnion( monitor, this, a, obliviousType );
                _typeCache.Add( k, result );
            }
            return (IUnionPocoType)result;
        }

    }

}
