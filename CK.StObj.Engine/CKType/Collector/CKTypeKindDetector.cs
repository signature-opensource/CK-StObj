using CK.Core;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Detector for <see cref="CKTypeKind"/>.
    /// </summary>
    public sealed partial class CKTypeKindDetector
    {
        static readonly CKTrait _cacheTag = ActivityMonitor.Tags.Context.FindOrCreate( "CKTypeCache" );

        readonly Dictionary<Type, IInternalCachedType> _cache;
        readonly Func<IActivityLineEmitter, Type, bool>? _typeFilter;
        readonly Dictionary<Type, AutoServiceKind> _endpointServices;
        readonly List<Type> _ubiquitousInfoServices;
        readonly MonitorAlternative _monitorAlternative;

        sealed class MonitorAlternative : IActivityLineEmitter
        {
            List<ActivityMonitorExternalLogData> _cachedLogs;

            public MonitorAlternative()
            {
                _cachedLogs = new List<ActivityMonitorExternalLogData>();
            }

            public IReadOnlyList<ActivityMonitorExternalLogData> Logs => _cachedLogs;

            public CKTrait AutoTags => _cacheTag;

            public LogLevelFilter ActualFilter => LogFilter.Combine( ActivityMonitor.DefaultFilter.Line, LogLevelFilter.Error );

            public ActivityMonitorLogData CreateActivityMonitorLogData( LogLevel level, CKTrait finalTags, string? text, object? exception, string? fileName, int lineNumber, bool isOpenGroup )
            {
                return ActivityMonitor.StaticLogger.CreateActivityMonitorLogData( level, finalTags, text, exception, fileName,lineNumber, isOpenGroup );
            }

            public void UnfilteredLog( ref ActivityMonitorLogData data )
            {
                _cachedLogs.Add( data.AcquireExternalData() );
            }
        }

        /// <summary>
        /// Initializes a new detector.
        /// </summary>
        /// <param name="typeFilter">Optional type filter.</param>
        public CKTypeKindDetector( Func<IActivityLineEmitter, Type, bool>? typeFilter = null )
        {
            _cache = new Dictionary<Type, IInternalCachedType>( 1024 );
            _endpointServices = new Dictionary<Type, AutoServiceKind>();
            _typeFilter = typeFilter;
            _ubiquitousInfoServices = new List<Type>();
            _monitorAlternative = new MonitorAlternative();
        }

        public ICachedType GetCachedType( Type type ) => InternalGetCachedType( null, type );

        public ICachedType GetCachedType( IActivityLineEmitter monitor, Type type ) => InternalGetCachedType( monitor, type );

        IInternalCachedType InternalGetCachedType( IActivityLineEmitter? monitor, Type type )
        {
            return _cache.TryGetValue( type, out var c ) ? c : Create( monitor, type );
        }

        IInternalCachedType Create( IActivityLineEmitter? monitor, Type t )
        {
            IInternalCachedType result;
            monitor ??= new MonitorAlternative();
            if( t.IsGenericType && !t.IsGenericTypeDefinition )
            {
                // A Generic Type definition can be a (Super)Definer or be a multiple service definition: this
                // applies directly to the specialized type.
                // Even the IsMarshallable is kept: we consider that a generic marshaller is possible!
                // We also keep excluded type flags here: it seems appropriate that by excluding the open generics (IProcessor<T>), the intent
                // is to exclude the closed ones (IProcessor<Document>) since excluding specifically the open one has no real meaning.
                var g = InternalGetCachedType( monitor, t.GetGenericTypeDefinition() );
                result = new CachedType( t, g );
            }
            else
            {
                // Captures once for all the attributes data that is a IList<> and if the [StObjGen] appear, totally
                // skips everything: the type is like an invalid type, cached instance will be null.
                var attributesDataList = t.GetCustomAttributesData();
                if( attributesDataList.Any( static a => a.AttributeType.Name == "StObjGenAttribute" ) )
                {
                    monitor.Debug( $"Type '{t:N}' is [StObjGen]. It is ignored." );
                    result = StructurallyExcludedType.Instance;
                }
                else
                {
                    var attributesData = attributesDataList.ToImmutableArray();
                    if( !(t.IsClass || t.IsInterface) )
                    {
                        Throw.DebugAssert( t.IsValueType );
                        result = new CachedType( t, CKTypeKind.None, attributesData );
                    }
                    else
                    {
                        result = CreateClassOrInterface( monitor, t, attributesData );
                    }
                }
            }
            _cache.Add( t, result );
            return result;
        }

        IInternalCachedType CreateClassOrInterface( IActivityLineEmitter monitor, Type t, ImmutableArray<CustomAttributeData> attributesData )
        {
            var baseType = t.BaseType;
            if( baseType == typeof( object ) ) baseType = null;

            var allPublicInterfaces = t.GetInterfaces().Where( i => i.IsPublic || i.IsNestedPublic ).ToImmutableArray();
            var directBaseTypes = allPublicInterfaces.Where( i => !allPublicInterfaces.Any( baseInterface => i != baseInterface && i.IsAssignableFrom( baseInterface ) ) );
            if( baseType != null )
            {
                directBaseTypes = directBaseTypes.Where( i => !i.IsAssignableFrom( baseType ) ).Prepend( baseType );
            }
            ImmutableArray<IInternalCachedType> directBases = directBaseTypes.Select( b => InternalGetCachedType( monitor, b ) )
                                                                             .Where( b => b != null )
                                                                             .ToImmutableArray()!;

            ImmutableArray<IInternalCachedType> allBases = new ImmutableArray<IInternalCachedType>();
            Throw.DebugAssert( allBases.IsDefault );

            var k = CKTypeKind.None;
            // First handles the pure interface: this can be one of our marker interfaces.
            if( t.IsInterface )
            {
                if( t.Name == nameof( IRealObject ) ) k = CKTypeKind.RealObject | IsDefiner | IsReasonMarker;
                else if( t.Name == nameof( IAutoService ) ) k = CKTypeKind.IsAutoService | IsDefiner | IsReasonMarker;
                else if( t.Name == nameof( IScopedAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsScoped | IsDefiner | IsReasonMarker;
                else if( t.Name == nameof( ISingletonAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsSingleton | IsDefiner | IsReasonMarker;
                else if( t.Name == nameof( IProcessAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsProcessService | IsDefiner | IsReasonMarker;
                else if( t == typeof( IPoco ) ) k = CKTypeKind.IsPoco | IsDefiner | IsReasonMarker;
            }
            // If it's not one of the interface marker, we analyze it.
            if( k == CKTypeKind.None )
            {
                Throw.DebugAssert( typeof( StObjGenAttribute ).Name == "StObjGenAttribute" );
                Throw.DebugAssert( typeof( ExcludeCKTypeAttribute ).Name == "ExcludeCKTypeAttribute" );
                Throw.DebugAssert( typeof( EndpointScopedServiceAttribute ).Name == "EndpointScopedServiceAttribute" );
                Throw.DebugAssert( typeof( EndpointSingletonServiceAttribute ).Name == "EndpointSingletonServiceAttribute" );
                Throw.DebugAssert( typeof( CKTypeSuperDefinerAttribute ).Name == "CKTypeSuperDefinerAttribute" );
                Throw.DebugAssert( typeof( CKTypeDefinerAttribute ).Name == "CKTypeDefinerAttribute" );
                Throw.DebugAssert( typeof( IsMultipleAttribute ).Name == "IsMultipleAttribute" );
                Throw.DebugAssert( typeof( IsMarshallableAttribute ).Name == "IsMarshallableAttribute" );
                Throw.DebugAssert( typeof( SingletonServiceAttribute ).Name == "SingletonServiceAttribute" );
                bool hasSuperDefiner = false;
                bool hasDefiner = false;
                bool isMultipleInterface = false;
                bool hasMarshallable = false;
                bool hasSingletonService = false;
                bool isExcludedType = false;
                bool isEndpointScoped = false;
                bool isUbiquitousServiceInfo = false;
                bool isEndpointSingleton = false;

                // Now process the attributes of the type. This sets the variables above
                // but doesn't touch k except to set it to ExcudedType if a [ExcludeCKType]
                // is found on the type. 
                foreach( var a in attributesData )
                {
                    var n = a.AttributeType.Name;
                    switch( n )
                    {
                        case "ExcludeCKTypeAttribute":
                            isExcludedType = true;
                            break;
                        case "EndpointScopedServiceAttribute":
                            isEndpointScoped = true;
                            isUbiquitousServiceInfo = a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is bool b && b;
                            break;
                        case "EndpointSingletonServiceAttribute":
                            isEndpointSingleton = true;
                            break;
                        case "CKTypeDefinerAttribute":
                            hasDefiner = true;
                            break;
                        case "CKTypeSuperDefinerAttribute":
                            hasSuperDefiner = true;
                            break;
                        case "IsMarshallableAttribute":
                            hasMarshallable = true;
                            break;
                        case "SingletonServiceAttribute":
                            hasSingletonService = true;
                            break;
                        case "IsMultipleAttribute" when t.IsInterface:
                            isMultipleInterface = true;
                            break;
                    }
                }

                Throw.DebugAssert( k == CKTypeKind.None || k == CKTypeKind.IsExcludedType );
                if( k == CKTypeKind.None )
                {
                    isExcludedType |= _typeFilter != null && !_typeFilter( monitor, t );

                    // Normalizes SuperDefiner => Definer (and emits a warning).
                    if( hasSuperDefiner )
                    {
                        if( hasDefiner )
                        {
                            monitor.Warn( $"Attribute [CKTypeDefiner] defined on type '{t:N}' is useless since [CKTypeSuperDefiner] is also defined." );
                        }
                        hasDefiner = true;
                    }
                    // Type's attributes have been analyzed, IsDefiner is normalized.
                    // It's time to apply the bases.
                    foreach( var i in directBases )
                    {
                        var kI = i.InternalKind & ~(IsDefiner | CKTypeKind.IsMultipleService | CKTypeKind.IsMarshallable | CKTypeKind.IsExcludedType | IsReasonMarker);
                        if( (k & IsDefiner) == 0 // We are not yet a Definer...
                            && (kI & IsSuperDefiner) != 0 ) // ...but this base is a SuperDefiner.
                        {
                            kI |= IsDefiner;
                        }
                        k |= kI & ~IsSuperDefiner;
                    }
                    // Applying the direct flags. Any inherited combination error is cleared.
                    k &= ~CKTypeKind.HasError;
                    if( isMultipleInterface ) k |= CKTypeKind.IsMultipleService;
                    if( hasMarshallable ) k |= CKTypeKind.IsMarshallable;
                    if( isExcludedType ) k |= CKTypeKind.IsExcludedType;
                    if( hasSingletonService ) k |= CKTypeKind.IsSingleton;
                    if( isEndpointSingleton ) k |= CKTypeKind.IsEndpointService | CKTypeKind.IsSingleton;
                    if( hasSuperDefiner ) k |= IsSuperDefiner;
                    if( hasDefiner ) k |= IsDefiner;
                    if( isUbiquitousServiceInfo ) k |= CKTypeKind.UbiquitousInfo;
                    else if( isEndpointScoped ) k |= CKTypeKind.IsEndpointService | CKTypeKind.IsScoped;

                    // Final check if the type filter has not excluded the type.
                    // We may be IAutoService or a IPoco or... whatever: any combination error will be detected.
                    if( k != CKTypeKind.None && !isExcludedType )
                    {
                        // We check for errors here that cannot be checked by the central GetCombinationError method and handle
                        // IMarshaller<> only if the type is not excluded.
                        bool isPublic = t.IsPublic || t.IsNestedPublic;

                        // A type MUST be public only if it is a IAutoService.
                        // (External services definitions are not concerned by public/private access.)
                        // If the type is not a IAutoService but a EndpointService, if it is not public we
                        // ignore it. This avoids to generate a reference to the type that will fail to compile
                        // and this keeps the implementation as a "detail". 
                        if( (k & CKTypeKind.IsAutoService) != 0 )
                        {
                            if( !isPublic )
                            {
                                monitor.Error( $"Type '{t:N}' being '{(k & MaskPublicInfo).ToStringFlags()}' must be public." );
                                k |= CKTypeKind.HasError;
                            }
                        }
                        else if( (k & CKTypeKind.IsEndpointService) != 0 )
                        {
                            if( !isPublic )
                            {
                                k &= ~CKTypeKind.IsEndpointService;
                                monitor.Info( $"Type '{t:N}' is an internal EndpointService. Its kind will only be '{(k & MaskPublicInfo).ToStringFlags()}'." );
                            }
                        }
                        if( t.IsClass )
                        {
                            Throw.DebugAssert( "IsMultipleAttribute targets interface only and is not propagated.",
                                                (k & CKTypeKind.IsMultipleService) == 0 );
                            // Always use the central GetCombinationError() method when possible: this method concentrates all the checks.
                            var error = (k & MaskPublicInfo).GetCombinationError( true );
                            if( error != null )
                            {
                                monitor.Error( $"Invalid class '{t:N}' kind: {error}" );
                                k |= CKTypeKind.HasError;
                            }
                            else if( (k & CKTypeKind.IsAutoService) != 0 )
                            {
                                allBases = CachedType.CreateAllBases( directBases, allPublicInterfaces );
                                foreach( var marshaller in allBases.Where( i => i.IsGenericType && i.GenericDefinition.Type == typeof( CK.StObj.Model.IMarshaller<> ) ) )
                                {
                                    var marshallable = marshaller.InternalGenericArguments[0];
                                    monitor.Info( $"Type '{marshallable:N}' considered as a Marshallable service because a IMarshaller implementation has been found on '{t:N}' that is a IAutoService." );
                                    SetLifetimeOrProcessType( monitor, CKTypeKind.IsMarshallable | IsMarshallableReasonMarshaller, marshallable );

                                    // The marshaller interface (the closed generic) is promoted to be a IAutoService since it must be
                                    // mapped (without ambiguities) on the currently registering class (that is itself a IAutoService).
                                    if( (marshaller.InternalKind & CKTypeKind.IsAutoService) == 0 )
                                    {
                                        if( !marshaller.MergeKind( monitor, CKTypeKind.IsAutoService ) )
                                        {
                                            monitor.Error( $"Unable to promote the IMarshaller interface '{marshaller.CSharpName}' as a IAutoService: {error}" );
                                        }
                                        else
                                        {
                                            monitor.Trace( $"Interface '{marshaller.CSharpName}' is now a IAutoService." );
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Throw.DebugAssert( t.IsInterface );
                            var error = (k & MaskPublicInfo).GetCombinationError( false );
                            if( error != null )
                            {
                                monitor.Error( $"Invalid interface '{t:N}' kind: {error}" );
                                k |= CKTypeKind.HasError;
                            }
                        }
                        if( (k & CKTypeKind.IsEndpointService) != 0 )
                        {
                            _endpointServices.Add( t, k.ToAutoServiceKind() );
                            if( (k & CKTypeKind.UbiquitousInfo) == CKTypeKind.UbiquitousInfo )
                            {
                                _ubiquitousInfoServices.Add( t );
                            }
                        }
                    }
                }
            }

            if( baseType != null ) k |= HasBaseType;
            return new CachedType( t, k, attributesData, allPublicInterfaces, directBases, allBases );
        }

        /// <summary>
        /// Gets all the types that have been declared as endpoint services.
        /// <para>
        /// Some of these types may appear only here since totally external types (interfaces or classes)
        /// can be declared as endpoint services.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices => _endpointServices;

        /// <summary>
        /// Gets the ubiquitous types. These are endpoint services that are available from all endpoints
        /// and can be overridden.
        /// </summary>
        public IReadOnlyList<Type> UbiquitousInfoServices => _ubiquitousInfoServices;

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>).
        /// <para>
        /// If the <see cref="AutoServiceKind.IsEndpointService"/> bit set, one of the lifetime bits mus be set
        /// (<see cref="AutoServiceKind.IsScoped"/> xor <see cref="AutoServiceKind.IsSingleton"/>).
        /// </para>
        /// <para>
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a Endpoint or Process service).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <param name="kind">The kind of service. Must not be <see cref="AutoServiceKind.None"/>.</param>
        /// <returns>The type kind on success, null on error (errors - included combination ones - are logged).</returns>
        public CKTypeKind? SetAutoServiceKind( IActivityMonitor monitor, Type t, AutoServiceKind kind )
        {
            Throw.CheckNotNullArgument( t );
            Throw.CheckArgument( kind != AutoServiceKind.None );

            CKTypeKind k = (CKTypeKind)kind;
            string? error = k.GetCombinationError( t.IsClass );
            if( error != null )
            {
                monitor.Error( $"Invalid Auto Service kind registration '{k.ToStringFlags()}' for type '{t:C}'." );
                return null;
            }
            bool hasProcess = (kind & AutoServiceKind.IsProcessService) != 0;
            bool hasLifetime = (kind & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) != 0;
            bool hasMultiple = (kind & AutoServiceKind.IsMultipleService) != 0;
            bool hasEndpoint = (kind & AutoServiceKind.IsEndpointService) != 0;

            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsMultipleReasonExternal;
            if( hasProcess ) k |= IsProcessServiceReasonExternal;
            if( hasEndpoint ) k |= IsEndpointServiceReasonExternal;

            return SetLifetimeOrProcessType( monitor, t, k );
        }

        /// <summary>
        /// Restricts a type to be Scoped (it is better to be a singleton).
        /// This is called once whenever an external type is used in a constructor.
        /// Returns null on error.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">The type to restrict.</param>
        /// <returns>The type kind on success, null on error.</returns>
        internal CKTypeKind? RestrictToScoped( IActivityMonitor monitor, Type t )
        {
            var exist = InternalGetCachedType( monitor, t );
            if( exist == null ) return CKTypeKind.IsScoped;
            if( !exist.MergeKind( monitor, CKTypeKind.IsScoped | IsScopedReasonReference ) )
            {
                return null;
            }
            return exist.NonDefinerKind;
        }

        CKTypeKind? SetLifetimeOrProcessType( IActivityLineEmitter monitor, Type t, CKTypeKind kind )
        {
            Throw.DebugAssert( "kind MUST not be a SuperDefiner or a Definer.", (kind & (IsDefiner | IsSuperDefiner)) == 0 );
            Throw.DebugAssert( (kind & MaskPublicInfo).GetCombinationError( t.IsClass )!, (kind & MaskPublicInfo).GetCombinationError( t.IsClass ) == null );
            Throw.DebugAssert( "At least, something must be set.",
                               (kind & CKTypeKind.LifetimeMask | CKTypeKind.IsProcessService | CKTypeKind.IsMultipleService | CKTypeKind.IsMarshallable | CKTypeKind.UbiquitousInfo) != 0 );

            // This registers the type (as long as the Type detection is concerned): there is no difference between Registering first
            // and then defining lifetime or the reverse. (This is not true for the full type registration: SetLifetimeOrFrontType must
            // not be called for an already registered type.)
            var exist = InternalGetCachedType( monitor, t );
            return SetLifetimeOrProcessType( monitor, kind, exist );
        }

        private CKTypeKind? SetLifetimeOrProcessType( IActivityLineEmitter monitor, CKTypeKind kind, IInternalCachedType exist )
        {
            var kExist = exist.InternalKind;
            if( (kExist & CKTypeKind.IsExcludedType) != 0 )
            {
                monitor.Error( $"Type '{exist.CSharpName}' is excluded. It cannot be defined as {ToStringFull( kind )}." );
                return null;
            }
            if( (kExist & (IsDefiner | IsSuperDefiner)) != 0 )
            {
                monitor.Error( $"Type '{exist.CSharpName}' is a Definer or a SuperDefiner. It cannot be defined as {ToStringFull( kind )}." );
                return null;
            }
            var updated = kExist | kind;
            string? error = (updated & MaskPublicInfo).GetCombinationError( exist.Type.IsClass );
            if( error != null )
            {
                monitor.Error( $"Type '{exist.CSharpName}' is already registered as a '{ToStringFull( kExist )}'. It can not be defined as {ToStringFull( kind )}. Error: {error}" );
                return null;
            }
            if( !exist.MergeKind( monitor, updated ) )
            {
                return null;
            }
            if( (updated & CKTypeKind.IsEndpointService) != 0 )
            {
                var t = exist.Type;
                _endpointServices[t] = updated.ToAutoServiceKind();
                if( (updated & CKTypeKind.UbiquitousInfo) == CKTypeKind.UbiquitousInfo
                    && !_ubiquitousInfoServices.Contains( t ) )
                {
                    _ubiquitousInfoServices.Add( t );
                }
            }

            Throw.DebugAssert( (updated & (IsDefiner | IsSuperDefiner)) == 0 );
            Throw.DebugAssert( CKTypeKindExtension.GetCombinationError( (updated & MaskPublicInfo), exist.Type.IsClass )!, CKTypeKindExtension.GetCombinationError( (updated & MaskPublicInfo), exist.Type.IsClass ) == null );
            return updated & MaskPublicInfo;
        }

        /// <summary>
        /// Checks whether the type kind: <see cref="CKTypeKind.IsExcludedType"/> or <see cref="CKTypeKind.HasError"/> flag set.
        /// <para>
        /// Note that [CKTypeDefiner] and [CKTypeSuperDefiner] kind is always <see cref="CKTypeKind.None"/>.
        /// </para>
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type that can be an interface or a class.</param>
        /// <returns>The CK type kind (may be invalid or excluded).</returns>
        public CKTypeKind GetRawKind( IActivityMonitor m, Type t )
        {
            return GetCachedType( m, t ).NonDefinerKind;
        }

        /// <summary>
        /// Checks whether the type is a non excluded and valid CK Type.
        /// Invalid or excluded types are <see cref="CKTypeKind.None"/>.
        /// <para>
        /// Note that [CKTypeDefiner] and [CKTypeSuperDefiner] kind is always <see cref="CKTypeKind.None"/>.
        /// </para>
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type that can be an interface or a class.</param>
        /// <returns>The CK type kind.</returns>
        public CKTypeKind GetValidKind( IActivityMonitor m, Type t )
        {
            return GetCachedType( m, t ).ValidKind;
        }

    }
}
