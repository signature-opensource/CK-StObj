using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

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

        internal IReadOnlyDictionary<Type, CKTypeEndpointServiceInfo> GetRegisteredEndpointServiceInfoMap( IActivityMonitor monitor )
        {
            using( monitor.OpenInfo( $"Finalizing Endpoint discovery." ) )
            {
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
        }

        /// <summary>
        /// Tries to set or extend the availability of a service to an endpoint.
        /// <para>
        /// This method is called by the assembly <see cref="EndpointScopedServiceTypeAttribute"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="serviceType">The type of the service. Must be an interface or a class and not a <see cref="IRealObject"/> nor an open generic.</param>
        /// <param name="endpointDefinition">The <see cref="EndpointDefinition"/>'s type.</param>
        /// <returns>True on success, false on error (logged into <paramref name="monitor"/>).</returns>
        public bool SetEndpointScopedService( IActivityMonitor monitor, Type serviceType, Type endpointDefinition ) => SetEndpointService( monitor, true, serviceType, endpointDefinition );

        /// <summary>
        /// Tries to define a service as a singleton managed by a <see cref="EndpointDefinition"/>.
        /// <para>
        /// This method is called by the assembly <see cref="EndpointSingletonServiceTypeAttribute"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="serviceType">The type of the service. Must be an interface or a class and not a <see cref="IRealObject"/> nor an open generic.</param>
        /// <param name="endpointDefinition">The <see cref="EndpointDefinition"/>'s type.</param>
        /// <returns>True on success, false on error (logged into <paramref name="monitor"/>).</returns>
        public bool SetEndpointSingletonService( IActivityMonitor monitor, Type serviceType, Type endpointDefinition ) => SetEndpointService( monitor, false, serviceType, endpointDefinition );

        bool SetEndpointService( IActivityMonitor monitor, bool isScoped, Type serviceType, Type endpointDefinition )
        {
            if( !CheckEndpointServiceParameters( monitor, serviceType, endpointDefinition ) ) return false;
            if( _endpointServices.TryGetValue( serviceType, out var exists ) )
            {
                return exists.CombineWith( monitor, isScoped, endpointDefinition );
            }
            // The type hasn't been registered yet. We DON'T register it here.
            // We memorize the configuration that will be handled once the type is registered.
            monitor.Info( $"Endpoint service '{serviceType}' has a {(isScoped ? "Scoped" : "Singleton")} lifetime because of the external registration in '{CKTypeEndpointServiceInfo.DefinitionName( endpointDefinition )}'." );
            _endpointServices.Add( serviceType, new CKTypeEndpointServiceInfo( isScoped, serviceType, endpointDefinition ) );
            return true;
        }

        static bool CheckEndpointServiceParameters( IActivityMonitor monitor, Type serviceType, Type endpointDefinition )
        {
            Throw.CheckNotNullArgument( serviceType );
            Throw.CheckNotNullArgument( endpointDefinition );
            bool isValidType = (serviceType.IsInterface || serviceType.IsClass)
                               && (serviceType.IsPublic || serviceType.IsNestedPublic)
                               && !typeof( IRealObject ).IsAssignableFrom( serviceType );
            if( !isValidType )
            {
                monitor.Error( $"Invalid EndpointType declaration: service type '{serviceType:C}' must be a public class or interface and not a IRealObject." );
                return false;
            }
            return CKTypeEndpointServiceInfo.CheckEndPointDefinition( monitor, endpointDefinition );
        }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>).
        /// <para>
        /// If the <see cref="AutoServiceKind.IsEndpointService"/> bit set, one of the lifetime bits mus be set
        /// (<see cref="AutoServiceKind.IsScoped"/> xor <see cref="AutoServiceKind.IsSingleton"/>) an the type
        /// is registered as an endpoint service in the <see cref="DefaultEndpointDefinition"/>.
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

            bool hasProcess = (kind & AutoServiceKind.IsProcessService) != 0;
            bool hasLifetime = (kind & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) != 0;
            bool hasMultiple = (kind & AutoServiceKind.IsMultipleService) != 0;

            CKTypeKind k = (CKTypeKind)kind;
            string? error = k.GetCombinationError( t.IsClass );
            if( error != null )
            {
                monitor.Error( $"Invalid Auto Service kind registration '{k.ToStringFlags()}' for type '{t}'." );
                return null;
            }
            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsMultipleReasonExternal;
            if( hasProcess ) k |= IsProcessServiceReasonExternal;
            if( (kind & AutoServiceKind.IsEndpointService) != 0 )
            {
                SetEndpointService( monitor, (kind & AutoServiceKind.IsScoped) != 0, t, typeof( DefaultEndpointDefinition ) );
            }
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
                //
                // We have an issue on Endpoint registration: we must not accept an internal endpoint service: this is done in
                // the SetEndpointXXX methods. So we can be sure here that if we are on a skipped interface, then we have no endpoint
                // registration to update.
                //
                bool isInternalInterface = t.IsInterface && !t.IsPublic && !t.IsNestedPublic;
                if( k == CKTypeKind.None && !isInternalInterface )
                {
                    Debug.Assert( typeof( StObjGenAttribute ).Name == "StObjGenAttribute" );
                    Debug.Assert( typeof( ExcludeCKTypeAttribute ).Name == "ExcludeCKTypeAttribute" );
                    Debug.Assert( typeof( EndpointScopedServiceAttribute ).Name == "EndpointScopedServiceAttribute" );
                    Debug.Assert( typeof( EndpointSingletonServiceAttribute ).Name == "EndpointSingletonServiceAttribute" );
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
                    // The final endpoint service info is the one from the map or a new orphan
                    // if one of our ancestors is a endpoint service and nothing else say so.
                    CKTypeEndpointServiceInfo? final = _endpointServices.GetValueOrDefault( t );
                    bool hasEndpointServiceError = false;

                    // Now process the attributes of the type. This sets the variables above
                    // but doesn't touch k except to set it to ExcudedType if a [StObjGen] or
                    // a [ExcludeCKType] is found on the type. 
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
                            case "EndpointScopedServiceAttribute":
                                {
                                    hasEndpointServiceError |= !ReadEndpointAttribute( m, t, true, a, ref final );
                                    break;
                                }
                            case "EndpointSingletonServiceAttribute":
                                {
                                    hasEndpointServiceError |= !ReadEndpointAttribute( m, t, false, a, ref final );
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
                    // - If we have an external EndpointServiceInfo set, it's better to update its Kind that sates that is HasBeenProcessed.
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
                        // Even if hasEndpointServiceError error is true, do what we can in order to update
                        // and have if possible a "final".
                        if( final != null )
                        {
                            k |= CKTypeKind.IsEndpointService | (final.IsScoped ? CKTypeKind.IsScoped : CKTypeKind.IsSingleton);
                        }
                        else
                        {
                            // No attribute, no external configuration. We may be a endpoint service because
                            // one of our ancestor is: we add an orphan. 
                            if( (k & CKTypeKind.IsEndpointService) != 0 )
                            {
                                Debug.Assert( (k & (CKTypeKind.IsScoped | CKTypeKind.IsSingleton)) != 0,
                                                "Since an ancestor set the IsEndPointService, then the lifetime is also settled." );
                                // Note that both Scoped & Singleton flags may be set here: this error will be raised below, so we don't
                                // care.
                                final = new CKTypeEndpointServiceInfo( t, (k & CKTypeKind.IsScoped) != 0 );
                                m.Info( $"Endpoint service '{t}' has a {(final.IsScoped ? "Scoped" : "Singleton")} lifetime it inherits this from one of its base types." );
                                _endpointServices.Add( t, final );
                            }
                        }
                        if( final != null )
                        {
                            // We have a final. The kind is set.
                            Debug.Assert( (k & CKTypeKind.IsEndpointService) != 0 );
                            // Clears IAutoService flag: a EndpointService is no more a IAutoService.
                            if( (k & CKTypeKind.IsAutoService) != 0 )
                            {
                                k &= ~CKTypeKind.IsAutoService;
                                m.Warn( $"Type '{t}' is a endpoint service, it is not more considered to be a IAutoService." );
                            }
                        }
                        // Final check if the type filter has not excluded the type.
                        //  - If we are a endpoint service, IAutoService has been cleared and we may have both IScoped & IsSingleton
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
                    // Always mark the endpoint registration if any as processed.
                    // (There cannot be any registration if this block has been skipped.)
                    // TO DO: Investigate what an Excluded endpoint service means. For the moment, this is ignored and since
                    // declared lifetime is settled, from the point of view of the endpoints, this shouldn't change anything.
                    final?.SetTypeProcessed( k );
                }
                // Always registers the kind whatever it is.
                // The type is registered.
                // Ite missa est.
                _cache.Add( t, k );
            }
            return k;
        }

        bool ReadEndpointAttribute( IActivityMonitor monitor, Type t, bool isScoped, System.Reflection.CustomAttributeData a, ref CKTypeEndpointServiceInfo? final )
        {
            var args = a.ConstructorArguments;
            if( args.Count != 1 )
            {
                monitor.Error( $"Invalid [{a.Constructor.Name}]( Type endpointDefinition )] on '{t:C}': expected a single argument (got {args.Count})." );
                return false;
            }
            if( args[0].Value is not Type tEndpointDefinition )
            {
                monitor.Error( $"Invalid [{a.Constructor.Name}( Type endpointDefinition )] on '{t:C}': the endpointDefinition must be a EndpointDefinition type (got '{args[0].Value}')." );
                return false;
            }
            if( !CKTypeEndpointServiceInfo.CheckEndPointDefinition( monitor, tEndpointDefinition ) ) return false;
            if( final != null )
            {
                if( !final.CombineWith( monitor, isScoped, tEndpointDefinition ) )
                {
                    monitor.Error( $"Attribute [{a.Constructor.Name}( Type endpointDefinition )] on '{t:C}' has a lifetime conflict with previously declared lifetime for this endpoint service." );
                    return false;
                }
            }
            else
            {
                monitor.Info( $"Endpoint service '{t}' has a {(isScoped ? "Scoped" : "Singleton")} lifetime because of [{a.Constructor.Name}('{CKTypeEndpointServiceInfo.DefinitionName( tEndpointDefinition )}')] attribute." );
                final = new CKTypeEndpointServiceInfo( isScoped, t, tEndpointDefinition );
                _endpointServices.Add( t, final );
            }
            return true;
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
