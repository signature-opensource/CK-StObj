using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Detector for <see cref="CKTypeKind"/>.
    /// </summary>
    public class CKTypeKindDetector
    {
        const int PrivateStart = 4096;

        /// <summary>
        /// Mask for public information defined in the <see cref="CKTypeKind"/> enumeration.
        /// Internally other flags are used.
        /// </summary>
        public const CKTypeKind MaskPublicInfo = (CKTypeKind)(PrivateStart-1);

        const CKTypeKind IsDefiner = (CKTypeKind)PrivateStart;

        const CKTypeKind IsSuperDefiner = (CKTypeKind)(PrivateStart << 1);

        // The lifetime reason is the interface marker (applies to all our marker interfaces).
        const CKTypeKind IsReasonMarker = (CKTypeKind)(PrivateStart << 2);

        // The type is singleton because it is used as a:
        // - ctor parameter of a Singleton Service.
        // - property or StObjConstruct/StObjFinalize parameter of a Real Object.
        const CKTypeKind IsSingletonReasonReference = (CKTypeKind)(PrivateStart << 3);

        // The type is a singleton because nothing prevents it to be a singleton.
        const CKTypeKind IsSingletonReasonFinal = (CKTypeKind)(PrivateStart << 4);

        // The type is a service that is scoped because its ctor references a scoped service.
        const CKTypeKind IsScopedReasonReference = (CKTypeKind)(PrivateStart << 5);

        // The service is Marshallable because a IAutoService Marshaller class has been found.
        const CKTypeKind IsMarshallableReasonMarshaller = (CKTypeKind)(PrivateStart << 6);

        // The lifetime reason is an external definition (applies to IsSingleton and IsScoped).
        const CKTypeKind IsLifetimeReasonExternal = (CKTypeKind)(PrivateStart << 7);

        // The IsProcessService reason is an external definition.
        const CKTypeKind IsProcessServiceReasonExternal = (CKTypeKind)(PrivateStart << 8);

        // The IsMultiple reason is an external definition.
        const CKTypeKind IsMultipleReasonExternal = (CKTypeKind)(PrivateStart << 9);

        readonly Dictionary<Type, CKTypeKind> _cache;
        readonly Dictionary<Type, CKTypeEndpointServiceInfo> _endpointServices;
        readonly Func<IActivityMonitor, Type, bool>? _typeFilter;
        bool _hasEndpointServiceError;

        /// <summary>
        /// Initializes a new detector.
        /// </summary>
        /// <param name="typeFilter">Optional type filter.</param>
        public CKTypeKindDetector( Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            _cache = new Dictionary<Type, CKTypeKind>( 1024 );
            _endpointServices = new Dictionary<Type, CKTypeEndpointServiceInfo>();
            _typeFilter = typeFilter;
        }

        internal IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo>? GetRegisteredEndpointServiceInfoMap( IActivityMonitor monitor )
        {
            if( _hasEndpointServiceError ) return null;
            foreach( var kv in _endpointServices )
            {
                if( !kv.Value.HasBeenProcessed )
                {
                    RawGet( monitor, kv.Key );
                    Debug.Assert( kv.Value.HasBeenProcessed );
                }
            }
            return _endpointServices;
        }

        /// <summary>
        /// Tries to set or extend the availability of a service to an endpoint.
        /// <para>
        /// This method is called by the assembly <see cref="EndpointAvailableServiceTypeAttribute"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="serviceType">The type of the service. Must be an interface or a class and not a <see cref="IRealObject"/> nor an open generic.</param>
        /// <param name="endpointDefinition">The <see cref="EndpointDefinition"/>'s type.</param>
        /// <returns>True on success, false on error (logged into <paramref name="monitor"/>).</returns>
        public bool SetEndpointAvailableService( IActivityMonitor monitor, Type serviceType, Type endpointDefinition )
        {
            CheckEndpointServiceParameters( serviceType, endpointDefinition );
            if( _endpointServices.TryGetValue( serviceType, out var exists ) )
            {
                return exists.AddAvailableEndpointDefinition( monitor, endpointDefinition );
            }
            // The type hasn't been registered yet. We DON'T register it here.
            // We memorize the configuration that will be handled once the type is registered.
            _endpointServices.Add( serviceType, new CKTypeEndpointServiceInfo( serviceType, null, false, new List<Type> { endpointDefinition } ) );
            return true;
        }

        static void CheckEndpointServiceParameters( Type serviceType, Type endpointDefinition )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckArgument( (serviceType.IsInterface || serviceType.IsClass) && !typeof( IRealObject ).IsAssignableFrom( serviceType ) );
            Throw.CheckArgument( typeof( EndpointDefinition ).IsAssignableFrom( endpointDefinition ) && endpointDefinition != typeof( EndpointDefinition ) );
        }

        /// <summary>
        /// Tries to define a service as a singleton managed by a <see cref="EndpointDefinition"/>.
        /// <para>
        /// This method is called by the assembly <see cref="EndpointSingletonServiceTypeOwnerAttribute"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="serviceType">The type of the service. Must be an interface or a class and not a <see cref="IRealObject"/> nor an open generic.</param>
        /// <param name="endpointDefinition">The <see cref="EndpointDefinition"/>'s type.</param>
        /// <param name="exclusive">True to exclusively expose the <paramref name="serviceType"/> from the <paramref name="endpointDefinition"/>.</param>
        /// <returns>True on success, false on error (logged into <paramref name="monitor"/>).</returns>
        public bool SetEndpointSingletonServiceOwner( IActivityMonitor monitor, Type serviceType, Type endpointDefinition, bool exclusive )
        {
            CheckEndpointServiceParameters( serviceType, endpointDefinition );

            List<Type> endpoints = new List<Type> { endpointDefinition };
            if( _endpointServices.TryGetValue( serviceType, out var exists ) )
            {
                return exists.CombineWith( monitor, endpointDefinition, exclusive, endpoints );
            }
            // The type hasn't been registered yet. We DON'T register it here.
            // We memorize the configuration that will be handled once the type is registered.
            _endpointServices.Add( serviceType, new CKTypeEndpointServiceInfo( serviceType, endpointDefinition, exclusive, endpoints ) );
            return true;
        }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/> nor has
        /// the <see cref="AutoServiceKind.IsEndpointService"/> bit set) for a type.
        /// <para>
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a Endpoint or Process service).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <param name="kind">
        /// The kind of service. Must not be <see cref="AutoServiceKind.None"/> nor has the <see cref="AutoServiceKind.IsEndpointService"/> bit set.
        /// </param>
        /// <returns>The type kind on success, null on error (errors - included combination ones - are logged).</returns>
        public CKTypeKind? SetAutoServiceKind( IActivityMonitor monitor, Type t, AutoServiceKind kind )
        {
            Throw.CheckNotNullArgument( t );
            Throw.CheckArgument( kind != AutoServiceKind.None );
            Throw.CheckArgument( (kind&AutoServiceKind.IsEndpointService) == 0 );

            bool hasProcess = (kind & (AutoServiceKind.IsProcessService)) != 0;
            bool hasLifetime = (kind & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) != 0;
            bool hasMultiple = (kind & AutoServiceKind.IsMultipleService) != 0;

            CKTypeKind k = (CKTypeKind)kind;
            if( hasProcess )
            {
                k |= CKTypeKind.IsProcessService;
            }
            string? error = k.GetCombinationError( t.IsClass );
            if( error != null )
            {
                monitor.Error( $"Invalid Auto Service kind registration '{k.ToStringFlags()}' for type '{t}'." );
                return null;
            }
            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsMultipleReasonExternal;
            if( hasProcess ) k |= IsProcessServiceReasonExternal;
            return SetLifetimeOrProcessType( monitor, t, k );
        }

        /// <summary>
        /// Restricts a type to be Scoped (it is better to be a singleton).
        /// This is called once whenever an external type is used in a constructor.
        /// Returns null on error.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type to restrict.</param>
        /// <returns>The type kind on success, null on error.</returns>
        internal CKTypeKind? RestrictToScoped( IActivityMonitor m, Type t )
        {
            return SetLifetimeOrProcessType( m, t, CKTypeKind.IsScoped | IsScopedReasonReference );
        }

        /// <summary>
        /// Tries to set the <see cref="CKTypeKind.IsPocoClass"/> flag for a type (that must be a class).
        /// This fails if the type is already registered as another kind of type.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type to configure.</param>
        /// <returns>True on success, false on error.</returns>
        internal bool SetPocoClass( IActivityMonitor m, Type t )
        {
            Debug.Assert( t.IsClass );
            var exist = RawGet( m, t );
            if( exist == CKTypeKind.None )
            {
                m.Trace( $"Type '{t}' is now defined as a PocoClass." );
                _cache[t] = CKTypeKind.IsPocoClass;
            }
            else if( exist != CKTypeKind.IsPocoClass )
            {
                m.Error( $"Type '{t}' is already registered as a '{ToStringFull( exist )}'. It can not be defined as a PocoClass." );
                return false;
            }
            return true;
        }

        CKTypeKind? SetLifetimeOrProcessType( IActivityMonitor m, Type t, CKTypeKind kind  )
        {
            Debug.Assert( (kind & (IsDefiner | IsSuperDefiner)) == 0, "kind MUST not be a SuperDefiner or a Definer." );
            Debug.Assert( (kind & (CKTypeKind.IsEndpointService)) == 0, "No way! Endpoint service cannot be set by flag." );
            Debug.Assert( (kind & MaskPublicInfo).GetCombinationError( t.IsClass ) == null, (kind & MaskPublicInfo).GetCombinationError( t.IsClass ) );

            bool hasLifetime = (kind & CKTypeKind.LifetimeMask) != 0;
            bool isProcess = (kind & CKTypeKind.IsProcessService) != 0;
            bool isMultiple = (kind & CKTypeKind.IsMultipleService) != 0;
            bool isMarshallable = (kind & CKTypeKind.IsMarshallable) != 0;

            Debug.Assert( hasLifetime || isProcess || isMultiple || isMarshallable, "At least, something must be set." );

            // This registers the type (as long as the Type detection is concerned): there is no difference between Registering first
            // and then defining lifetime or the reverse. (This is not true for the full type registration: SetLifetimeOrFrontType must
            // not be called for an already registered type.)
            var exist = RawGet( m, t );
            if( (exist & (IsDefiner|IsSuperDefiner)) != 0 )
            {
                Throw.Exception( $"Type '{t}' is a Definer or a SuperDefiner. It cannot be defined as {ToStringFull( kind )}." );
            }
            var updated = exist | kind;
            string? error = (updated & MaskPublicInfo).GetCombinationError( t.IsClass );
            if( error != null )
            {
                m.Error( $"Type '{t}' is already registered as a '{ToStringFull( exist )}'. It can not be defined as {ToStringFull( kind )}. Error: {error}" );
                return null;
            }
            _cache[t] = updated;
            Debug.Assert( (updated & (IsDefiner | IsSuperDefiner)) == 0 );
            Debug.Assert( CKTypeKindExtension.GetCombinationError( (updated & MaskPublicInfo), t.IsClass ) == null );
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
            var k = RawGet( m, t );
            return (k & (IsDefiner | IsSuperDefiner)) == 0
                        ? k & MaskPublicInfo
                        : CKTypeKind.None;
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
            var k = RawGet( m, t );
            return (k & (IsDefiner | IsSuperDefiner | CKTypeKind.IsExcludedType | CKTypeKind.HasError)) == 0
                        ? k & MaskPublicInfo
                        : CKTypeKind.None;
        }

        CKTypeKind RawGet( IActivityMonitor m, Type t )
        {
            if( !_cache.TryGetValue( t, out CKTypeKind k )
                && t != typeof( object )
                && (t.IsClass || t.IsInterface) )
            {
                Debug.Assert( k == CKTypeKind.None );

                // This would be the code to implement "Strong Exclusion".
                // But since, for the moment, exclusion is a weak concept, we process the type as if it was not excluded.
                //
                //   if( _typeFilter != null && !_typeFilter( m, t ) )
                //   {
                //      k = IsFilteredType;
                //   }
                //   else
                //
                var baseType = t.BaseType;
                var allInterfaces = t.GetInterfaces();
                // First handles the pure interface that have no base interfaces and no members: this can be one of our marker interfaces.
                // We must also handle here interfaces that have one base because IScoped/SingletonAutoService/IProcessAutoService are extending IAutoService.
                // ...and unfortunately we must also consider the ones with 2 base interfaces because of IFrontAutoService that extends IFrontProcessAutoService
                // that extends IFrontAutoService. 
                if( t.IsInterface
                    && allInterfaces.Length <= 2
                    && t.GetMembers().Length == 0 )
                {
                    if( t.Name == nameof( IRealObject ) ) k = CKTypeKind.RealObject | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IAutoService ) ) k = CKTypeKind.IsAutoService | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IScopedAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsScoped | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( ISingletonAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsSingleton | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IProcessAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsProcessService | IsDefiner | IsReasonMarker;
                    else if( t == typeof( IPoco ) ) k = CKTypeKind.IsPoco | IsDefiner | IsReasonMarker;
                }
                // If it's not one of the interface marker and it's not an internal interface, we analyze it.
                // Any "internal interface" is simply ignored because no public interfaces can extend it (Error CS0061: Inconsistent accessibility).
                // So, "internal interfaces" are leaves, we don't need to handle "holes" in the interface hierarchy and implementations are free to
                // define and use them.
                if( k == CKTypeKind.None && !(t.IsInterface && !t.IsPublic && !t.IsNestedPublic) )
                {
                    Debug.Assert( typeof( StObjGenAttribute ).Name == "StObjGenAttribute" );
                    Debug.Assert( typeof( ExcludeCKTypeAttribute ).Name == "ExcludeCKTypeAttribute" );
                    Debug.Assert( typeof( EndpointAvailableServiceAttribute ).Name == "EndpointAvailableServiceAttribute" );
                    Debug.Assert( typeof( EndpointSingletonServiceOwnerAttribute ).Name == "EndpointSingletonServiceOwnerAttribute" );
                    Debug.Assert( typeof( CKTypeSuperDefinerAttribute ).Name == "CKTypeSuperDefinerAttribute" );
                    Debug.Assert( typeof( CKTypeDefinerAttribute ).Name == "CKTypeDefinerAttribute" );
                    Debug.Assert( typeof( IsMultipleAttribute ).Name == "IsMultipleAttribute" );
                    Debug.Assert( typeof( PocoClassAttribute ).Name == "PocoClassAttribute" );
                    Debug.Assert( typeof( IsMarshallableAttribute ).Name == "IsMarshallableAttribute" );
                    bool hasSuperDefiner = false;
                    bool hasDefiner = false;
                    bool isMultipleInterface = false;
                    bool hasMarshallable = false;
                    bool isPocoClass = false;
                    bool isExcludedType = false;
                    // As soon as endpointDefinitions is not null, this is a EndpointService either because
                    // of [EndpointServiceAvailability] or [EndpointSingletonServiceOwner].
                    List<Type>? endpointDefinitions = null;
                    Type? endpointSingletonOwner = null;
                    bool exclusiveEndpointSingletonOwner = false;
                    bool hasEndpointServiceError = false;

                    // Now process the attributes of the type. This sets the variables above.
                    foreach( var a in t.GetCustomAttributesData() )
                    {
                        var n = a.AttributeType.Name;
                        if( n == "StObjGenAttribute" )
                        {
                            // This attributes stops all subsequent analysis (it's the only one).
                            // A [StObjGen] is necessarily None.
                            k = CKTypeKind.IsExcludedType;
                            m.Trace( $"Type '{t}' is [StObjGen]. It is ignored." );
                            break;
                        }
                        switch( n )
                        {
                            case "ExcludeCKTypeAttribute":
                                isExcludedType = true;
                                break;
                            case "EndpointAvailableServiceAttribute":
                                {
                                    hasEndpointServiceError |= !ReadEndpointAvailability( m, t, a, ref endpointDefinitions );
                                    break;
                                }
                            case "EndpointSingletonServiceOwnerAttribute":
                                {
                                    hasEndpointServiceError |= !ReadEndpointSingleton( m, t, a, ref endpointDefinitions, ref endpointSingletonOwner, ref exclusiveEndpointSingletonOwner );
                                    break;
                                }
                            case "CKTypeDefinerAttribute":
                                hasDefiner = true;
                                break;
                            case "CKTypeSuperDefinerAttribute":
                                hasSuperDefiner = true;
                                break;
                            case "IsMarshallableAttribute":
                                hasMarshallable = true;
                                break;
                            case "PocoClassAttribute":
                                isPocoClass = true;
                                break;
                            case "IsMultipleAttribute" when t.IsInterface:
                                isMultipleInterface = true;
                                break;
                        }
                    }

                    Debug.Assert( k == CKTypeKind.None || k == CKTypeKind.IsExcludedType );
                    // Even if hasEndpointServiceError is true, we continue the process because:
                    // - We choose a "detect as many errors as possible" rather than a "fail fast" philosophy years ago.
                    // - If we have an external EndpointServiceInfo set, it's better to update is Kind that sates that is HasBeenProcessed.
                    if( k == CKTypeKind.None )
                    {
                        isExcludedType |= _typeFilter != null && !_typeFilter( m, t );

                        // Normalizes SuperDefiner => Definer (and emits a warning).
                        if( hasSuperDefiner )
                        {
                            if( hasDefiner )
                            {
                                m.Warn( $"Attribute [CKTypeDefiner] defined on type '{t}' is useless since [CKTypeSuperDefiner] is also defined." );
                            }
                            hasDefiner = true;
                        }
                        // Type's attributes have been analyzed, IsDefiner is normalized.
                        if( hasDefiner )
                        {
                            // Since this is a definer, we can skip any handling of potential Super Definer.
                            // We ignore the base type, we only consider its interfaces.
                            // We also clear any IsMultipleService and IsMarshallable since these flags are not transitive.
                            //
                            // (Note that [IsMultiple] may be "transmitted" here but a CKTypeDefiner for "Multiple" interfaces would not be
                            // a good idea: an explicit [IsMultiple] attribute on the interface is much more maintainable.)
                            //
                            foreach( var i in allInterfaces )
                            {
                                var kI = RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner | CKTypeKind.IsMultipleService | CKTypeKind.IsMarshallable | IsReasonMarker);
                                k |= kI;
                                Debug.Assert( (k & CKTypeKind.IsPocoClass) == 0, "PocoClass attribute can only be on class." );
                            }
                            k |= IsDefiner;
                            if( hasSuperDefiner ) k |= IsSuperDefiner;
                            // PocoClass cannot be a definer or a super definer.
                            if( isPocoClass )
                            {
                                m.Error( $"PocoClass '{t}' cannot be a [{(hasSuperDefiner ? "CKTypeSuperDefiner" : "CKTypeDefiner")}]." );
                            }
                            // We must lookup the base if any for the Endpoint service information.
                            if( baseType != null )
                            {
                                var kBase = RawGet( m, baseType );
                            }
                        }
                        else
                        {
                            Debug.Assert( k == CKTypeKind.None );
                            // If the base type is a SuperDefiner, then this is a Definer.
                            if( baseType != null )
                            {
                                // IsMarshallable and IsPocoClass is not propagated.
                                // "Weak Exclusion": an excluded (or filtered) base type doesn't exclude it specializations.
                                var kBase = RawGet( m, baseType ) & ~(CKTypeKind.IsMarshallable | CKTypeKind.IsPocoClass | CKTypeKind.IsExcludedType | IsReasonMarker) ;
                                Debug.Assert( (kBase & CKTypeKind.IsMultipleService) == 0, "IsMultipleService is for interfaces only." );
                                if( (kBase & IsSuperDefiner) != 0 )
                                {
                                    Debug.Assert( (kBase & IsDefiner) != 0 );
                                    k = kBase & ~IsSuperDefiner;
                                }
                                else k = kBase & ~IsDefiner;
                            }
                            if( (k & IsDefiner) != 0 )
                            {
                                // If the base type was a SuperDefiner, this is a definer and we can skip any handling of Super Definer.
                                foreach( var i in allInterfaces )
                                {
                                    k |= RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner | CKTypeKind.IsMultipleService | CKTypeKind.IsMarshallable | CKTypeKind.IsExcludedType | IsReasonMarker);
                                }
                            }
                            else
                            {
                                // We are not (yet?) a Definer.
                                foreach( var i in allInterfaces )
                                {
                                    var kI = RawGet( m, i ) & ~(IsDefiner | CKTypeKind.IsMultipleService | CKTypeKind.IsMarshallable | CKTypeKind.IsExcludedType);
                                    if( (k & IsDefiner) == 0 // We are not yet a Definer...
                                        && (kI & IsSuperDefiner) != 0 ) // ...but this base interface is a SuperDefiner.
                                    {
                                        // Really?
                                        bool indirect = (baseType != null && i.IsAssignableFrom( baseType ))
                                                            || allInterfaces.Any( baseInterface => i != baseInterface && i.IsAssignableFrom( baseInterface ) );
                                        kI &= ~IsSuperDefiner;
                                        if( !indirect ) kI |= IsDefiner;
                                    }
                                    k |= kI & ~IsSuperDefiner;
                                }
                            }
                        }
                        // Propagation from base and interfaces has been done.
                        // If we're still None here, we look for an open generic definition.
                        if( k == CKTypeKind.None && t.IsGenericType && !t.IsGenericTypeDefinition )
                        {
                            // A Generic Type definition can be a (Super)Definer or be a multiple service definition: this
                            // applies directly to the specialized type.
                            // Even the IsMarshallable is kept: we consider that a generic marshaller is possible!
                            // We also keep excluded type flags here: it seems appropriate that by excluding the open generics (IProcessor<T>), the intent
                            // is to exclude the closed ones (IProcessor<Document>) since excluding specifically the open one has no real meaning.
                            var tGen = t.GetGenericTypeDefinition();
                            k = RawGet( m, tGen );
                        }
                        // Applying the direct flags. Any inherited combination error is cleared.
                        k &= ~CKTypeKind.HasError;
                        if( isMultipleInterface ) k |= CKTypeKind.IsMultipleService;
                        if( hasMarshallable ) k |= CKTypeKind.IsMarshallable;
                        if( isPocoClass ) k |= CKTypeKind.IsPocoClass;
                        if( isExcludedType ) k |= CKTypeKind.IsExcludedType;
                        // The "detect as many errors as possible" rather than a "fail fast" philosophy introduces 
                        // a lot of complexity. For EndpointService error, we shortcut here.
                        // If hasEndpointServiceError is already true, we totally skip processing endpoint.
                        if( !hasEndpointServiceError )
                        {
                            // No Endpoint service error at this level.
                            // The final one (registered in the map) can be currently existing one (external configuration),
                            // a brand new one from the attributes or an orphan if one of our ancestors is a endpoint service
                            // and nothing else say so.
                            CKTypeEndpointServiceInfo? final = null;

                            // We now have up to 2 sources of EndPoint service information:
                            // - The attribute defined (in locals endpointSingletonOwner, exclusiveEndpointSingletonOwner and endpointDefinitions).
                            // - The external one in _endpointServices map.

                            // We first consider the attributes and infer what we can about the type kind.
                            if( endpointDefinitions != null )
                            {
                                k |= CKTypeKind.IsEndpointService;
                                m.Info( $"Type '{t}' is a Endpoint service because of a [EndpointSingletonServiceOwner] or [EndpointServiceAvailability] attribute." );
                                if( endpointSingletonOwner != null )
                                {
                                    Debug.Assert( endpointDefinitions.Contains( endpointSingletonOwner ) );
                                    if( (k & CKTypeKind.IsSingleton) == 0 )
                                    {
                                        k |= CKTypeKind.IsSingleton;
                                        m.Info( $"Type '{t}' is a necessarily a singleton because of the [EndpointSingletonServiceOwner] attribute." );
                                    }
                                }
                            }
                            // If we have an external configuration info, we handle it.
                            if( _endpointServices.TryGetValue( t, out var exists ) )
                            {
                                // The external info is the final.
                                // If we have no attribute, the external defines the kind else
                                // it is combined with the attributes (that already defined the kind).
                                final = exists;
                                if( endpointDefinitions == null )
                                {
                                    Debug.Assert( k == CKTypeKind.None && !hasEndpointServiceError );
                                    k |= CKTypeKind.IsEndpointService;
                                    if( exists.Owner != null )
                                    {
                                        k |= CKTypeKind.IsSingleton;
                                        m.Info( $"Type '{t}' has been externally defined as a Endpoint singleton service owned by '{exists.Owner.Name}'." );
                                    }
                                    else
                                    {
                                        m.Info( $"Type '{t}' has been externally defined as a Endpoint service." );
                                    }
                                }
                                else
                                {
                                    hasEndpointServiceError |= !exists.CombineWith( m, endpointSingletonOwner, exclusiveEndpointSingletonOwner, endpointDefinitions );
                                }
                            }
                            else
                            {
                                // No external info: final is provided by the attributes if any
                                // or this is not an endpoint service at all and final remains null... unless the kind
                                // is already set (by an ancestor).
                                if( endpointDefinitions != null )
                                {
                                    final = new CKTypeEndpointServiceInfo( t, endpointSingletonOwner, exclusiveEndpointSingletonOwner, endpointDefinitions );
                                }
                                else if( (k & CKTypeKind.IsEndpointService) != 0 )
                                {
                                    // New orphan. They will be listed while analyzing the endpoints, it is useless to warn here.
                                    final = new CKTypeEndpointServiceInfo( t );
                                }
                            }
                            if( final != null && !hasEndpointServiceError )
                            {
                                // We have a final. The kind is set.
                                Debug.Assert( (k & CKTypeKind.IsEndpointService) != 0 );
                                // Clears IAutoService flag: a EndpointService is no more a IAutoService.
                                if( (k & CKTypeKind.IsAutoService) != 0 )
                                {
                                    k &= ~CKTypeKind.IsAutoService;
                                    m.Warn( $"Type '{t}' is a endpoint service, it is not more considered to be a IAutoService." );
                                }
                                // If the final has a owner then we already are a singleton: this has been ensured above.
                                Debug.Assert( final.Owner == null || (k & CKTypeKind.IsSingleton) != 0 );
                                // But the reverse is not true, when no "EndpointSingleton" has been applied, we can still be anything
                                // and we don't want that: endpoint services settle their lifetime. Here, if we have no owner then we are
                                // a scoped service.
                                if( final.Owner == null )
                                {
                                    m.Warn( $"Type '{t}' is a endpoint service with no singleton declaration: it is considered as a Scoped service." );
                                    k |= CKTypeKind.IsScoped;
                                }
                                // This locks the owner/exclusive information but EndpointDefinitions may still be added if
                                // the EndpointServiceInfo is not locked.
                                final.SetTypeProcessed( k );
                                // Adds or updates (it may already exist when external).
                                _endpointServices[t] = final;
                            }
                        }
                        // If hasEndpointServiceError is eventually true, we update the potential external configuration
                        // with a kind HasError and sets the _hasEndpointServiceError flag: a null registered endpoint map
                        // will be returned, no endpoint analysis will be done.
                        if( hasEndpointServiceError )
                        {
                            _hasEndpointServiceError = true;
                            k |= CKTypeKind.HasError;
                            if( _endpointServices.TryGetValue( t, out var exists ) )
                            {
                                exists.SetTypeProcessed( k );
                            }
                        }
                        else
                        {
                            // No endpoint service error:
                            //  - If we are a endpoint service, IAutoService has been cleared and we may have IScoped & IsSingleton
                            //    flags: this will be a combination error.
                            //  - If we are not, we may be IAutoService or a IPoco or... whatever: any combination error will be detected.
                            if( k != CKTypeKind.None && !isExcludedType )
                            {
                                // We check for errors here that cannot be checked by the central GetCombinationError method and handle
                                // IMarshaller<> only if the type is not excluded.

                                // A type MUST be public only if it is a IAutoService.
                                // External services definitions are not concerned by public/private access!
                                if( !t.Assembly.IsDynamic
                                    && (k & CKTypeKind.IsAutoService) != 0
                                    && !(t.IsPublic || t.IsNestedPublic) )
                                {
                                    m.Error( $"Type '{t}' being '{(k & MaskPublicInfo).ToStringFlags()}' must be public." );
                                }
                                if( t.IsClass )
                                {
                                    Debug.Assert( (k & CKTypeKind.IsMultipleService) == 0, "IsMultipleAttribute targets interface only and is not propagated." );
                                    // Always use the central GetCombinationError() method when possible: this method concentrates all the checks.
                                    var error = (k & MaskPublicInfo).GetCombinationError( true );
                                    if( error != null )
                                    {
                                        m.Error( $"Invalid class '{t}' kind: {error}" );
                                        k |= CKTypeKind.HasError;
                                    }
                                    else if( (k & CKTypeKind.IsAutoService) != 0 )
                                    {
                                        foreach( var marshaller in allInterfaces.Where( i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof( CK.StObj.Model.IMarshaller<> ) ) )
                                        {
                                            Type marshallable = marshaller.GetGenericArguments()[0];
                                            m.Info( $"Type '{marshallable}' considered as a Marshallable service because a IMarshaller implementation has been found on '{t}' that is a IAutoService." );
                                            SetLifetimeOrProcessType( m, marshallable, CKTypeKind.IsMarshallable | IsMarshallableReasonMarshaller );

                                            // The marshaller interface (the closed generic) is promoted to be a IAutoService since it must be
                                            // mapped (without ambiguities) on the currently registering class (that is itself a IAutoService).
                                            var exists = RawGet( m, marshaller );
                                            if( (exists & CKTypeKind.IsAutoService) == 0 )
                                            {
                                                exists |= CKTypeKind.IsAutoService;
                                                error = exists.GetCombinationError( false );
                                                if( error != null ) m.Error( $"Unable to promote the IMarshaller interface {marshaller.Name} as a IAutoService: {error}" );
                                                else
                                                {
                                                    m.Trace( $"Interface {marshaller.Name} is now a IAutoService." );
                                                    _cache[marshaller] = exists;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.Assert( t.IsInterface );
                                    var error = (k & MaskPublicInfo).GetCombinationError( false );
                                    if( error != null )
                                    {
                                        m.Error( $"Invalid interface '{t}' kind: {error}" );
                                        k |= CKTypeKind.HasError;
                                    }
                                }
                            }
                        }
                    }
                }
                _cache.Add( t, k );
            }
            return k;

            static bool ReadEndpointSingleton( IActivityMonitor monitor,
                                               Type t,
                                               System.Reflection.CustomAttributeData a,
                                               ref List<Type>? endpointDefinitions,
                                               ref Type? endpointSingletonOwner,
                                               ref bool exclusiveEndpointSingletonOwner )
            {
                var args = a.ConstructorArguments;
                if( args.Count != 2 )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type endpointDefinition, bool exclusiveEndpoint )] on '{t}': expected 2 arguments (got {args.Count})." );
                    return false;
                }
                if( args[0].Value is not Type tEndpointDefinition
                    || !typeof( EndpointDefinition ).IsAssignableFrom( tEndpointDefinition ) )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type endpointDefinition, bool exclusiveEndpoint )] on '{t}': the endpointDefinition must be a EndpointDefinition (got '{args[0].Value}')." );
                    return false;
                }
                if( args[1].Value is not bool exclusive )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type endpointDefinition, bool exclusiveEndpoint )] on '{t}': the exclusiveEndpoint must be a boolean (got '{args[0].ArgumentType}')." );
                    return false;
                }
                if( endpointSingletonOwner != null && endpointSingletonOwner != tEndpointDefinition )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type, bool )] on '{t}': already bound to owner '{endpointSingletonOwner}'." );
                    return false;
                }
                endpointSingletonOwner = tEndpointDefinition;
                exclusiveEndpointSingletonOwner = exclusive;
                endpointDefinitions = AddEndpoint( endpointDefinitions, tEndpointDefinition );
                return true;

            }

            static bool ReadEndpointAvailability( IActivityMonitor monitor,
                                                  Type t,
                                                  System.Reflection.CustomAttributeData a,
                                                  ref List<Type>? endpointDefinitions )
            {
                var args = a.ConstructorArguments;
                if( args.Count != 1 )
                {
                    monitor.Error( $"Invalid [EndpointServiceAvailability( Type endpointDefinition )] on '{t}': expected a single argument (got {args.Count})." );
                    return false;
                }
                if( args[0].Value is not Type tEndpointDefinition
                    || !typeof( EndpointDefinition ).IsAssignableFrom( tEndpointDefinition ) )
                {
                    monitor.Error( $"Invalid [EndpointServiceAvailability( Type endpointDefinition )] on '{t}': the endpointDefinition must be a EndpointDefinition (got '{args[0].Value}')." );
                    return false;
                }
                endpointDefinitions = AddEndpoint( endpointDefinitions, tEndpointDefinition );
                return true;
            }

            static List<Type> AddEndpoint( List<Type>? endpointDefinitions, Type tEndpointDefinition )
            {
                if( endpointDefinitions == null )
                {
                    endpointDefinitions = new List<Type>() { tEndpointDefinition };
                }
                else
                {
                    // Just in case both EndpointServiceAvailabilityAttribute and EndpointSingletonServiceOwner are used
                    // or duplicated types with EndpointServiceAvailabilityAttribute.
                    if( !endpointDefinitions.Contains( tEndpointDefinition ) ) endpointDefinitions.Add( tEndpointDefinition );
                }
                return endpointDefinitions;
            }
        }

        static string ToStringFull( CKTypeKind t )
        {
            var c = (t & MaskPublicInfo).ToStringFlags();
            if( (t & IsDefiner) != 0 ) c += " [IsDefiner]";
            if( (t & IsSuperDefiner) != 0 ) c += " [IsSuperDefiner]";
            if( (t & IsReasonMarker) != 0 ) c += " [IsMarkerInterface]";
            if( (t & IsLifetimeReasonExternal) != 0 ) c += " [Lifetime:External]";
            if( (t & IsSingletonReasonReference) != 0 ) c += " [Lifetime:ReferencedBySingleton]";
            if( (t & IsSingletonReasonFinal) != 0 ) c += " [Lifetime:OpimizedAsSingleton]";
            if( (t & IsScopedReasonReference) != 0 ) c += " [Lifetime:UsesScoped]";
            if( (t & IsMarshallableReasonMarshaller) != 0 ) c += " [Marshallable:MarshallerExists]";
            if( (t & IsProcessServiceReasonExternal) != 0 ) c += " [ProcessService:External]";
            if( (t & IsMultipleReasonExternal) != 0 ) c += " [Multiple:External]";
            return c;
        }

    }

}
