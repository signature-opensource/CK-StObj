using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Implementation of <see cref="IPocoTypeSystemBuilder"/>.
    /// </summary>
    public sealed partial class PocoTypeSystemBuilder : IPocoTypeSystemBuilder
    {
        readonly IExtMemberInfoFactory _memberInfoFactory;
        // The type caches has 2 types of keys:
        // - Type:
        //      - A type is mapped to the oblivious IPocoType
        // - String:
        //      - A string key indexes the non nullable IPocoType.CSharpName (oblivious or not, nullabilities appear in the key).
        //      - Not all types need to be indexed by name.
        readonly Dictionary<object, IPocoType> _typeCache;
        // Opne generic types collector.
        readonly Dictionary<Type, PocoType.PocoGenericTypeDefinition> _typeDefinitions;
        // Contains the not nullable types (PocoType instances are the non nullable types).
        readonly List<PocoType> _nonNullableTypes;
        readonly WithNullTypeList _allTypes;
        readonly Stack<StringBuilder> _stringBuilderPool;
        readonly Dictionary<string, PocoRequiredSupportType> _requiredSupportTypes;
        // The Poco directory can be EmptyPocoDirectory.Default for testing only 
        readonly IPocoDirectory _pocoDirectory;
        // Types marked with [NotSerializable] or flagged by SetNotSerializable if any.
        HashSet<IPocoType>? _notSerializable;
        // Types marked with [NonExchangeable] or flagged by SetExchangeable if any.
        // The actual set of NonExchangeable is a super set of the NotSerializable.
        HashSet<IPocoType>? _notExchangeable;
        // Not null when Locked.
        IPocoTypeSystem? _result;

        /// <summary>
        /// Initializes a new type system with only the basic types registered.
        /// <para>
        /// Optional automatic registration of <see cref="List{T}"/> is done when <see cref="Lock(IActivityMonitor)"/> is called.
        /// </para>
        /// </summary>
        /// <param name="memberInfoFactory">Required factory that cached nullable reference type information.</param>
        /// <param name="pocoDirectory">Optional Poco directory (null for test only: <see cref="EmptyPocoDirectory.EmptyPocoDirectory"/> is used).</param>
        public PocoTypeSystemBuilder( IExtMemberInfoFactory memberInfoFactory,
                                      IPocoDirectory? pocoDirectory = null )
        {
            _stringBuilderPool = new Stack<StringBuilder>();
            _memberInfoFactory = memberInfoFactory;
            _pocoDirectory = pocoDirectory ?? EmptyPocoDirectory.Default;
            _nonNullableTypes = new List<PocoType>( 4096 );
            _allTypes = new WithNullTypeList( _nonNullableTypes );
            _requiredSupportTypes = new Dictionary<string, PocoRequiredSupportType>();
            _typeDefinitions = new Dictionary<Type, PocoType.PocoGenericTypeDefinition>();
            _typeCache = new Dictionary<object, IPocoType>( 8192 );
        }

        public bool IsLocked => _result != null;

        public IPocoTypeSystem Lock( IActivityMonitor monitor )
        {
            if( _result == null )
            {
                Throw.DebugAssert( "All Oblivious types are indexed by type and are either value types or nullable reference type.",
                                   _nonNullableTypes.Where( t => ((IPocoType)t).IsOblivious ).All( t => _typeCache.ContainsKey( t.Type ) && t.Type.IsValueType || t.IsNullable ) );
                Throw.DebugAssert( "All types that are indexed are the oblivious ones.",
                                   _typeCache.Where( kv => kv.Key is Type ).All( kv => kv.Value.IsOblivious ) );
                Throw.DebugAssert( "Any abstract basic ref type must have specialization (currently all basic ref types are concrete).",
                                   _typeCache.Values.OfType<IBasicRefPocoType>().All( t => t.Nullable.IsFinalType || t.Nullable.Specializations.Any( n => n.IsFinalType ) ) );

                //monitor.Info( _allTypes.Where( t => !(t.IsPolymorphic || t.StructuralFinalType != null) )
                //                       .Select( t => $"{t} - {t.IsPolymorphic}, {t.StructuralFinalType?.ToString() ?? "null"}" )
                //                       .Concatenate( Environment.NewLine ) );

                Throw.DebugAssert( "(StructuralFinalType == null) => IsPolymorphic " +
                                   "(and contraposition) !IsPolymorphic => StructuralFinalType != null.",
                                   _allTypes.All( t => t.IsPolymorphic || t.StructuralFinalType != null ) );

                monitor.Info( _allTypes.Where( t => !t.IsRegular && (t.IsStructuralFinalType && (t is not ICollectionPocoType c || !c.IsAbstractCollection) ) )
                                       .Select( t => $"{t} - {t.IsRegular}, {t.IsStructuralFinalType && (t is not ICollectionPocoType c || !c.IsAbstractCollection)}" )
                                       .Concatenate( Environment.NewLine ) );
                Throw.DebugAssert( "IsStructuralFinalType && !Abstract collection => IsRegularType",
                                   _allTypes.All( t => !(t.IsStructuralFinalType && (t is not ICollectionPocoType c || !c.IsAbstractCollection)) || t.IsRegular ) );

                var finalTypeBuilder = ImmutableArray.CreateBuilder<IPocoType>();
                int initialCount = _nonNullableTypes.Count;
                for( int i = 0; i < initialCount; i++ )
                {
                    PocoType? t = _nonNullableTypes[i];
                    Throw.DebugAssert( !t.IsNullable );
                    // Locate the Oblivious type only if it is this type or its nullable.
                    var tO = t.ObliviousType;
                    if( tO != t )
                    {
                        if( tO != t.Nullable ) continue;
                        tO = t.Nullable;
                    }
                    // An oblivious type can be a final type.
                    if( tO.IsFinalType ) finalTypeBuilder.Add( tO );
                }
                _result = new PocoTypeSystem( _pocoDirectory,
                                              _allTypes,
                                              _nonNullableTypes,
                                              finalTypeBuilder.ToImmutableArray(),
                                              _typeCache,
                                              _typeDefinitions,
                                              _notSerializable,
                                              _notExchangeable );
            }
            return _result;
        }

        public IPocoDirectory PocoDirectory => _pocoDirectory;

        public int Count => _nonNullableTypes.Count << 1;

        public IReadOnlyCollection<IPocoRequiredSupportType> RequiredSupportTypes => _requiredSupportTypes.Values;

        internal void AddNew( PocoType t )
        {
            Throw.CheckState( !IsLocked );
            Throw.DebugAssert( !t.IsNullable );
            Throw.DebugAssert( t.Index == _nonNullableTypes.Count * 2 );
            _nonNullableTypes.Add( t );
        }

        public IPocoType? FindByType( Type type )
        {
            return _typeCache.GetValueOrDefault( type );
        }

        public T? FindByType<T>( Type type ) where T : class, IPocoType
        {
            return _typeCache.GetValueOrDefault( type ) as T;
        }

        public IPocoGenericTypeDefinition? FindGenericTypeDefinition( Type type )
        {
            return _typeDefinitions.GetValueOrDefault( type );
        }

        public void SetNotSerializable( IActivityMonitor monitor, IPocoType type )
        {
            Throw.CheckState( !IsLocked );
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckArgument( type != null && type.Index < _allTypes.Count && _allTypes[type.Index] == type );
            monitor.Info( $"Poco '{type}' is declared to be non serializable. It will also be non exchangeable." );
            DoSetNotSerializable( type );
        }

        internal void DoSetNotSerializable( IPocoType type )
        {
            _notSerializable ??= new HashSet<IPocoType>();
            _notSerializable.Add( type.NonNullable );
        }

        public void SetNotExchangeable( IActivityMonitor monitor, IPocoType type )
        {
            Throw.CheckState( !IsLocked );
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckArgument( type != null && type.Index < _allTypes.Count && _allTypes[type.Index] == type );
            monitor.Info( $"Poco '{type}' is declared to be non exchangeable." );
            DoSetNotExchangeable( type );
        }

        internal void DoSetNotExchangeable( IPocoType type )
        {
            _notExchangeable ??= new HashSet<IPocoType>();
            _notExchangeable.Add( type.NonNullable );
        }

        void HandleNotSerializableAndNotExchangeableAttributes( IActivityMonitor monitor, IPocoType t )
        {
            if( t.Type.CustomAttributes.Any( a => a.AttributeType == typeof( NotSerializableAttribute ) ) )
            {
                monitor.Info( $"Poco '{t}' is [NotSerializable]." );
                DoSetNotSerializable( t );
            }
            if( t.Type.CustomAttributes.Any( a => a.AttributeType == typeof( NotExchangeableAttribute ) ) )
            {
                monitor.Info( $"Poco '{t}' is [NotExchangeable]." );
                DoSetNotExchangeable( t );
            }
        }

        public IPocoType? RegisterOblivious( IActivityMonitor monitor, Type t ) => Register( monitor, _memberInfoFactory.CreateNullOblivious( t ) );

        public IPocoType? Register( IActivityMonitor monitor, PropertyInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        public IPocoType? Register( IActivityMonitor monitor, FieldInfo f ) => Register( monitor, _memberInfoFactory.Create( f ) );

        public IPocoType? Register( IActivityMonitor monitor, ParameterInfo p ) => Register( monitor, _memberInfoFactory.Create( p ) );

        public IPocoType? Register( IActivityMonitor monitor, IExtMemberInfo memberInfo )
        {
            Throw.CheckState( !IsLocked );
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
            Throw.DebugAssert( !nInfo.Type.IsByRef );
            var result = nInfo.Type.IsValueType
                                  ? OnValueType( monitor, nInfo, ctx )
                                  : OnReferenceType( monitor, nInfo, ctx );
            Throw.DebugAssert( result == null || result.IsNullable == nInfo.IsNullable );
            return result;
        }

        IPocoType? OnReferenceType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            Type t = nType.Type;
            if( t.IsSZArray )
            {
                return RegisterArray( monitor, nType, ctx );
            }
            if( t.IsGenericType )
            {
                return OnGenericReferenceType( monitor, nType, ctx );
            }
            if( _typeCache.TryGetValue( t, out var result ) )
            {
                Throw.DebugAssert( "Reference type oblivious is the nullable.", result.IsNullable );
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
                return nType.IsNullable ? result : result.NonNullable;
            }
            // Not in cache. It may be a basic type.
            var basic = TryRegisterBasicRefType( t );
            if( basic != null ) return nType.IsNullable ? basic.Nullable : basic;

            // If it's a IPoco we should have found it: it has been excluded or not registered...
            // ...OR it's a IPoco interface that has NO implementation but appears (otherwise we won't be here)
            // as a property or a generic argument.
            // Instead of using the TypeDetector to check whether this is an orphan abstract (and not an excluded one),
            // we consider it to be an ImplementationLess Abstract. This has side effect: this may "transform" a real property
            // into an abstract one... And this is perfectly fine: if everything is evetually successfully resolved
            // the system is valid.
            // If the implementation less type is used at a place that requires an instance, this will fail.
            if( typeof( IPoco ).IsAssignableFrom( t ) )
            {
                if( t.IsInterface )
                {
                    return OnAbstractPoco( monitor, nType, null );
                }
                monitor.Error( $"IPoco '{t}' has been excluded or not registered." );
            }
            else
            {
                monitor.Error( $"Unsupported type: '{t}'." );
            }
            return null;
        }

        IPocoType? TryRegisterBasicRefType( Type t )
        {
            if( t == typeof( object ) )
            {
                var o = PocoType.CreateObject( this );
                Throw.DebugAssert( o.ObliviousType == o.Nullable && o.StructuralFinalType == null );
                _typeCache.Add( "object", o );
                _typeCache.Add( t, o.Nullable );
                return o;
            }
            if( t == typeof( string ) )
            {
                return RegBasicRefType( this, _typeCache, t, "string", FieldDefaultValue.StringDefault, isReadOnlyCompliant: true, isPolymorphic: false, null );
            }
            if( t == typeof( MCString ) )
            {
                Throw.DebugAssert( t.Name == "MCString" );
                return RegBasicRefType( this, _typeCache, t, "MCString", FieldDefaultValue.MCStringDefault, isReadOnlyCompliant: false, isPolymorphic: false, null );
            }
            if( t == typeof( CodeString ) )
            {
                Throw.DebugAssert( t.Name == "CodeString" );
                return RegBasicRefType( this, _typeCache, typeof( CodeString ), "CodeString", FieldDefaultValue.CodeStringDefault, isReadOnlyCompliant: false, isPolymorphic: false, null );
            }
            //
            // To avoid a [RegisterPocoType] here, ExtendedCultureInfo triggers a registration of NormalizedCultureInfo.
            // This is optional but this simplifies life and as IsPolymorphic is structural (it doesn't depend on the type set),
            // this has no impact.
            //
            if( t == typeof( ExtendedCultureInfo ) || t == typeof( NormalizedCultureInfo ) )
            {
                // We need the ExtendedCultureInfo base type to register the NormalizedCultureInfo.
                Throw.DebugAssert( !_typeCache.TryGetValue( typeof( ExtendedCultureInfo ), out var e ) || e is IBasicRefPocoType );
                if( !_typeCache.TryGetValue( typeof( ExtendedCultureInfo ), out var extCInfo ) )
                {
                    extCInfo = RegisterExtendedCultureInfo( this, _typeCache );
                }
                var nCInfo = RegBasicRefType( this,
                                              _typeCache,
                                              typeof( NormalizedCultureInfo ),
                                              "NormalizedCultureInfo",
                                              FieldDefaultValue.CultureDefault,
                                              isReadOnlyCompliant: true,
                                              isPolymorphic: false,
                                              Unsafe.As<IBasicRefPocoType>( extCInfo ) );
                return t == typeof( ExtendedCultureInfo )
                        ? extCInfo
                        : nCInfo;
            }

            static IBasicRefPocoType RegisterExtendedCultureInfo( PocoTypeSystemBuilder s, Dictionary<object, IPocoType> c )
            {
                Throw.DebugAssert( typeof( ExtendedCultureInfo ).Name == "ExtendedCultureInfo" );
                return RegBasicRefType( s,
                                        c, typeof( ExtendedCultureInfo ),
                                        "ExtendedCultureInfo",
                                        FieldDefaultValue.CultureDefault,
                                        isReadOnlyCompliant: true,
                                        isPolymorphic: true,
                                        baseType: null );
            }

            static IBasicRefPocoType RegBasicRefType( PocoTypeSystemBuilder s,
                                                      Dictionary<object, IPocoType> c,
                                                      Type t,
                                                      string name,
                                                      FieldDefaultValue defaultValue,
                                                      bool isReadOnlyCompliant,
                                                      bool isPolymorphic,
                                                      IBasicRefPocoType? baseType )
            {
                var x = PocoType.CreateBasicRef( s, t, name, defaultValue, isReadOnlyCompliant, isPolymorphic, baseType );
                Throw.DebugAssert( !x.IsNullable && x.ObliviousType == x.Nullable
                                   && !x.IsStructuralFinalType && x.StructuralFinalType == x.Nullable );
                c.Add( t, x.Nullable );
                c.Add( name, x );
                return x;
            }
            return null;
        }

        IPocoType? OnGenericReferenceType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            var t = nType.Type;
            var tGen = t.GetGenericTypeDefinition();
            bool isRegular = tGen == typeof( List<> );
            if( isRegular || tGen == typeof( IList<> ) )
            {
                return RegisterListOrSet( monitor, true, nType, ctx, isRegular );
            }
            isRegular = tGen == typeof( HashSet<> );
            if( isRegular || tGen == typeof( ISet<> ) )
            {
                return RegisterListOrSet( monitor, false, nType, ctx, isRegular );
            }
            isRegular = tGen == typeof( Dictionary<,> );
            if( isRegular || tGen == typeof( IDictionary<,> ) )
            {
                return RegisterDictionary( monitor, nType, ctx, isRegular );
            }
            // "Abstract Read Only" handling.
            if( tGen == typeof( IReadOnlyList<> ) )
            {
                return RegisterReadOnlyListOrSet( monitor, true, nType, ctx );
            }
            if( tGen == typeof( IReadOnlySet<> ) )
            {
                return RegisterReadOnlyListOrSet( monitor, false, nType, ctx );
            }
            if( tGen == typeof( IReadOnlyDictionary<,> ) )
            {
                return RegisterReadOnlyDictionary( monitor, nType, ctx );
            }
            // Generic AbstractPoco is the last chance...
            if( t.IsInterface && typeof(IPoco).IsAssignableFrom( t ) )
            {
                return OnAbstractPoco( monitor, nType, tGen );
            }
            monitor.Error( $"{ctx}: Unsupported Poco generic type '{t:C}'." );
            return null;
        }

        IPocoType? OnAbstractPoco( IActivityMonitor monitor, IExtNullabilityInfo nType, Type? tGen )
        {
            var result = OnAbstractPoco( monitor, nType.Type, tGen );
            return result == null
                    ? null
                    : nType.IsNullable
                        ? result.Nullable
                        : result;
        }

        // This may be an already registered one because there's at least one implementation for
        // it or an orphan one.
        IPocoType? OnAbstractPoco( IActivityMonitor monitor, Type t, Type? tGen )
        {
            // If the interface is not registered, it means that there's no implementation
            // for this. It is a warning, not an error otherwise we'll prevent "partial system"
            // to run.
            // However, the type may be a secondary IPoco interface that has been excluded/not registered
            // and in such case, it's an error. We can detect this case if a parent interface is a ISecondary or IPrimaryPoco.
            if( !_typeCache.TryGetValue( t, out var result ) )
            {
                monitor.Warn( $"Abstract IPoco interface '{t:N}' is 'ImplementationLess' (not implemented by any registered Poco)." );
                bool success = true;
                var generalizations = new List<IAbstractPocoType>();
                foreach( var type in t.GetInterfaces() )
                {
                    // IAbstractPocoType.Generalizations must not contain the IPoco base.
                    if( type != typeof( IPoco ) && typeof( IPoco ).IsAssignableFrom( type ) )
                    {
                        var g = OnAbstractPoco( monitor, type, type.IsGenericType ? type.GetGenericTypeDefinition() : null );
                        if( g != null )
                        {
                            if( g is not IAbstractPocoType a )
                            {
                                monitor.Error( $"IPoco Type '{t:N}' has been excluded or is not registered." );
                                return null;
                            }
                            generalizations.Add( a.NonNullable );
                        }
                        else success = false;
                    }
                }
                PocoType.PocoGenericTypeDefinition? typeDefinition = null; 
                (IPocoGenericParameter Parameter, IPocoType Type)[]? arguments = null;
                if( tGen != null )
                {
                    typeDefinition = EnsureTypeDefinition( tGen );
                    arguments = typeDefinition.CreateArguments( monitor, this, t );
                    success &= arguments != null;
                }
                if( !success ) return null;
                result = PocoType.CreateImplementationLessAbstractPoco( this, t, generalizations, typeDefinition, arguments );
                Throw.DebugAssert( !result.IsNullable && result.Nullable.IsOblivious );
                // Don't call HandleNotSerializableAndNotExchangeableAttributes( monitor, result ) since this
                // type is not implemented.
                _typeCache.Add( t, result.Nullable );
            }
            Throw.DebugAssert( result is IAbstractPocoType or ISecondaryPocoType or IPrimaryPocoType );
            return result;
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
                Throw.DebugAssert( obliviousType.Kind == PocoTypeKind.Record || (obliviousType.IsOblivious && !obliviousType.IsNullable) );
                if( obliviousType.Kind == PocoTypeKind.Basic
                    || obliviousType.Kind == PocoTypeKind.Enum
                    || obliviousType.Kind == PocoTypeKind.Record )
                {
                    return nType.IsNullable ? obliviousType.Nullable : obliviousType.NonNullable;
                }
            }
            if( tNotNull.IsEnum )
            {
                if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalName ) )
                {
                    return null;
                }
                // Register the underlying integral type.
                var underlyingType = RegisterOblivious( monitor, tNotNull.GetEnumUnderlyingType() );
                if( underlyingType == null ) return null;

                tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
                obliviousType = PocoType.CreateEnum( monitor,
                                                     this,
                                                     tNotNull,
                                                     tNull,
                                                     underlyingType,
                                                     externalName );
                Throw.DebugAssert( !obliviousType.IsNullable );
                // Even if the enum is invalid, register it correctly to preserve
                // the invariants.
                HandleNotSerializableAndNotExchangeableAttributes( monitor, obliviousType );
                // It is useless to register an enum by its name.
                _typeCache.Add( tNotNull, obliviousType );
                _typeCache.Add( tNull, obliviousType.Nullable );
                if( obliviousType.DefaultValueInfo.IsDisallowed )
                {
                    // An error has been logged.
                    return null;
                }
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
                // May be a basic value type.
                var basic = TryRegisterBasicValueType( tNotNull );
                Throw.DebugAssert( basic == null || !basic.IsNullable );
                if( basic != null ) return nType.IsNullable ? basic.Nullable : basic;

                // Last chance: a new "record struct".
                tNull ??= typeof( Nullable<> ).MakeGenericType( tNotNull );
                record = OnTypedRecord( monitor, ctx, nType, tNotNull, tNull );
            }
            Throw.DebugAssert( record is null or IRecordPocoType );
            return record;
        }

        IPocoType? TryRegisterBasicValueType( Type tNotNull )
        {
            if( tNotNull == typeof( bool ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( bool? ), "bool" );
            }
            if( tNotNull == typeof( int ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( int? ), "int" );
            }
            if( tNotNull == typeof( long ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( long? ), "long" );
            }
            if( tNotNull == typeof( short ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( short? ), "short" );
            }
            if( tNotNull == typeof( byte ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( byte? ), "byte" );
            }
            if( tNotNull == typeof( double ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( double? ), "double" );
            }
            if( tNotNull == typeof( float ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( float? ), "float" );
            }
            if( tNotNull == typeof( DateTime ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( DateTime? ), "DateTime" );
            }
            if( tNotNull == typeof( DateTimeOffset ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( DateTimeOffset? ), "DateTimeOffset" );
            }
            if( tNotNull == typeof( TimeSpan ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( TimeSpan? ), "TimeSpan" );
            }
            if( tNotNull == typeof( Guid ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( Guid? ), "Guid" );
            }
            if( tNotNull == typeof( Decimal ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( decimal? ), "decimal" );
            }
            if( tNotNull == typeof( System.Numerics.BigInteger ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( System.Numerics.BigInteger? ), "System.Numerics.BigInteger" );
            }
            if( tNotNull == typeof( uint ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( uint? ), "uint" );
            }
            if( tNotNull == typeof( ulong ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( ulong? ), "ulong" );
            }
            if( tNotNull == typeof( ushort ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( ushort? ), "ushort" );
            }
            if( tNotNull == typeof( sbyte ) )
            {
                return RegValueType( this, _typeCache, tNotNull, typeof( sbyte? ), "sbyte" );
            }
            if( tNotNull == typeof( SimpleUserMessage ) )
            {
                return Reg( _typeCache, PocoType.CreateNoDefaultBasicValue( this, tNotNull, typeof( SimpleUserMessage? ), "SimpleUserMessage" ) );
            }
            if( tNotNull == typeof( UserMessage ) )
            {
                return Reg( _typeCache, PocoType.CreateNoDefaultBasicValue( this, tNotNull, typeof( UserMessage? ), "UserMessage" ) );
            }
            if( tNotNull == typeof( FormattedString ) )
            {
                return Reg( _typeCache, PocoType.CreateNoDefaultBasicValue( this, typeof( FormattedString ), typeof( FormattedString? ), "FormattedString" ) );
            }
            return null;

            static IPocoType RegValueType( PocoTypeSystemBuilder s, Dictionary<object, IPocoType> c, Type tNotNull, Type tNull, string name )
            {
                return Reg( c, PocoType.CreateBasicValue( s, tNotNull, tNull, name ) );

            }

            static IPocoType Reg( Dictionary<object, IPocoType> typeCache, IPocoType x )
            {
                Throw.DebugAssert( !x.IsNullable
                                   && Nullable.GetUnderlyingType( x.Type ) == null
                                   && Nullable.GetUnderlyingType( x.Nullable.Type ) == x.Type );
                typeCache.Add( x.Type, x );
                typeCache.Add( x.Nullable.Type, x.Nullable );
                return x;
            }
        }

        IPocoType? OnValueTypeAnonymousRecord( IActivityMonitor monitor,
                                               MemberContext ctx,
                                               IExtNullabilityInfo nType,
                                               Type tNotNull,
                                               Type tNull,
                                               IRecordPocoType? obliviousType )
        {
            Throw.DebugAssert( "Value tuples implement IEquatable<TSelf>.",
                                tNotNull.GetInterfaces().Any( i => i.IsGenericType
                                                                   && i.Namespace == "System"
                                                                   && i.Name == "IEquatable`1"
                                                                   && i.GetGenericArguments()[0] == tNotNull ) );
            var subInfos = FlattenValueTuple( nType ).ToList();
            var fields = ctx.EnterValueTuple( subInfos.Count, out var state );
            // Here we can resolve the field types without fear of infinite recursion: value tuples
            // cannot be recursive by design.
            // We can detect that this is the unnamed or oblivious one by looking at the field names and types.
            // We also compute whether this is a read only type.
            bool isReadOnlyCompliant = true;
            bool isUnnamed = true;
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
                if( !f.IsUnnamed )
                {
                    isUnnamed = false;
                    isOblivious = false;
                    b.Append( ' ' ).Append( f.Name );
                }
                else isOblivious &= fType.IsOblivious;
                f.SetType( fType );
                isReadOnlyCompliant &= f.Type.IsReadOnlyCompliant;
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
                    obliviousType = PocoType.CreateAnonymousRecord( this,
                                                                    tNotNull,
                                                                    tNull,
                                                                    obliviousName,
                                                                    fields,
                                                                    isReadOnlyCompliant,
                                                                    null,
                                                                    null );
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
                    obliviousFields[f.Index] = new RecordAnonField( f, isOblivious: true );
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
                    obliviousType = PocoType.CreateAnonymousRecord( this,
                                                                    tNotNullOblivious,
                                                                    tNullOblivious,
                                                                    obliviousName,
                                                                    obliviousFields,
                                                                    isReadOnlyCompliant,
                                                                    null,
                                                                    null );
                    _typeCache.Add( tNotNullOblivious, obliviousType );
                    _typeCache.Add( tNullOblivious, obliviousType.Nullable );
                    _typeCache.Add( obliviousName, obliviousType );
                }
            }
            // We now have the oblivious. Now handle the unnamed.
            // If this is the unnamed that we are created, we are done.
            if( isUnnamed )
            {
                // Don't need the buffer anymore.
                StringBuilderPool.GetStringAndReturn( b );
                result = PocoType.CreateAnonymousRecord( this, tNotNull, tNull, typeName, fields, isReadOnlyCompliant, null, obliviousType );
                _typeCache.Add( typeName, result );
                return nType.IsNullable ? result.Nullable : result;
            }
            // Else we must ensure the Unnamed.
            var unnamedFields = new RecordAnonField[fields.Length];
            b.Clear();
            b.Append( '(' );
            foreach( var f in fields )
            {
                unnamedFields[f.Index] = new RecordAnonField( f, isOblivious: false );
                if( b.Length != 1 ) b.Append( ',' );
                b.Append( f.Type.CSharpName );
            }
            b.Append( ')' );
            // Don't need the buffer anymore.
            var unnamedTypeName = StringBuilderPool.GetStringAndReturn( b );
            if( !_typeCache.TryGetValue( unnamedTypeName, out var unnamedType ) )
            {
                unnamedType = PocoType.CreateAnonymousRecord( this,
                                                              tNotNull,
                                                              tNull,
                                                              unnamedTypeName,
                                                              unnamedFields,
                                                              isReadOnlyCompliant,
                                                              null,
                                                              obliviousType );
                _typeCache.Add( unnamedTypeName, unnamedType );
            }
            // 
            result = PocoType.CreateAnonymousRecord( this, tNotNull, tNull, typeName, fields, isReadOnlyCompliant, unnamedType, obliviousType );
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
            Throw.DebugAssert( tNotNull.IsValueType );
            Throw.DebugAssert( "OnValueType didn't find it.", !_typeCache.ContainsKey( tNotNull ) );

            // Named record can have an [ExternalName].
            if( !TypeExtensions.TryGetExternalNames( monitor, tNotNull, tNotNull.GetCustomAttributesData(), out var externalName ) )
            {
                return null;
            }
            if( !tNotNull.GetInterfaces().Any( i => i.IsGenericType
                                                    && i.Namespace == "System"
                                                    && i.Name == "IEquatable`1"
                                                    && i.GetGenericArguments()[0] == tNotNull ) )
            {
                monitor.Error( $"Struct '{tNotNull:N}' must implement 'IEquatable<{tNotNull.Name}>'." );
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
                monitor.Error( $"More than one constructor found for record struct '{tNotNull:N}': at most one constructor must be specified for record struct." );
                return null;
            }
            var ctorParams = ctors.Length == 1 ? ctors[0].GetParameters() : null;

            bool isReadOnlyCompliant = true;
            var propertyInfos = tNotNull.GetProperties();
            var fieldInfos = tNotNull.GetFields();
            var fields = new RecordNamedField[propertyInfos.Length + fieldInfos.Length];
            bool success = true;
            for( int i = 0; i < propertyInfos.Length; i++ )
            {
                var pInfo = _memberInfoFactory.Create( propertyInfos[i] );
                if( !pInfo.PropertyInfo.CanWrite && !pInfo.Type.IsByRef )
                {
                    monitor.Error( $"'{pInfo}' is readonly. A record struct must be fully mutable." );
                    success = false;
                }
                if( success )
                {
                    var f = CreateField( monitor, r, i, Register( monitor, pInfo ), pInfo, ctorParams, StringBuilderPool );
                    if( f == null )
                    {
                        success = false;
                    }
                    else
                    {
                        isReadOnlyCompliant &= f.Type.IsReadOnlyCompliant;
                        fields[i] = f;
                    }
                }
            }
            int idx = propertyInfos.Length;
            for( int i = 0; i < fieldInfos.Length; i++ )
            {
                var fInfo = _memberInfoFactory.Create( fieldInfos[i] );
                if( fInfo.FieldInfo.IsInitOnly )
                {
                    monitor.Error( $"Field '{fInfo.DeclaringType}.{fInfo.Name}' is readonly. A record struct must be fully mutable." );
                    success = false;
                }
                else
                {
                    var f = CreateField( monitor, r, idx, Register( monitor, fInfo ), fInfo, ctorParams, StringBuilderPool );
                    if( f == null )
                    {
                        success = false;
                    }
                    else
                    {
                        isReadOnlyCompliant &= f.Type.IsReadOnlyCompliant;
                        fields[idx++] = f;
                    }
                }
            }
            if( !success ) return null;

            r.SetFields( monitor, this, isReadOnlyCompliant, fields );
            HandleNotSerializableAndNotExchangeableAttributes( monitor, r );
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
                object? originator = null;
                FieldDefaultValue? defValue = null;
                if( ctorParams != null )
                {
                    var p = ctorParams.FirstOrDefault( p => p.Name == fInfo.Name );
                    if( p != null )
                    {
                        originator = p;
                        defValue = FieldDefaultValue.CreateFromParameter( monitor, stringPool, p );
                    }
                }
                defValue ??= FieldDefaultValue.CreateFromAttribute( monitor, stringPool, fInfo );
                originator ??= fInfo.UnderlyingObject;
                return new RecordNamedField( r, idx, fInfo.Name, tField, defValue, originator );
            }
        }

        IUnionPocoType RegisterUnionType( IActivityMonitor monitor, List<IPocoType> types )
        {
            var a = types.ToImmutableArray();
            var k = new PocoType.KeyUnionTypes( a, out bool isOblivious );
            IPocoType? obliviousType = null;
            if( !isOblivious )
            {
                var bO = ImmutableArray.CreateBuilder<IPocoType>( a.Length );
                for( int i = 0; i < a.Length; i++ )
                {
                    bO.Add( a[i].ObliviousType );
                }
                var kO = new PocoType.KeyUnionTypes( bO.MoveToImmutable(), out var _ );
                Throw.DebugAssert( !kO.Equals( k ) );
                if( !_typeCache.TryGetValue( kO, out obliviousType ) )
                {
                    obliviousType = PocoType.CreateUnion( monitor, this, kO, null ).ObliviousType;
                    Throw.DebugAssert( "Union type Oblivious is the nullable.", obliviousType.IsNullable );
                    _typeCache.Add( kO, obliviousType );
                }
            }
            if( !_typeCache.TryGetValue( k, out var result ) )
            {
                result = PocoType.CreateUnion( monitor, this, k, obliviousType );
                _typeCache.Add( k, result );
            }
            return (IUnionPocoType)result;
        }
    }

}
