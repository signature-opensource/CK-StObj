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

        // The front type reason is an external definition (applies to IsMarshallable and IsFrontOnly).
        const CKTypeKind IsFrontTypeReasonExternal = (CKTypeKind)(PrivateStart << 8);

        // The IsMultiple reason is an external definition.
        const CKTypeKind IsMultipleReasonExternal = (CKTypeKind)(PrivateStart << 9);

        readonly Dictionary<Type, CKTypeKind> _cache;
        readonly Dictionary<Type, EndpointServiceInfo> _endpointServices;
        readonly Func<IActivityMonitor, Type, bool>? _typeFilter;

        /// <summary>
        /// Initializes a new detector.
        /// </summary>
        /// <param name="typeFilter">Optional type filter.</param>
        public CKTypeKindDetector( Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            _cache = new Dictionary<Type, CKTypeKind>( 1024 );
            _endpointServices = new Dictionary<Type, EndpointServiceInfo>();
            _typeFilter = typeFilter;
        }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/> nor has
        /// the <see cref="AutoServiceKind.IsEndpointService"/> bit set) for a type.
        /// <para>
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a endpoint service).
        /// </para>
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <param name="kind">
        /// The kind of service. Must not be <see cref="AutoServiceKind.None"/> nor has the <see cref="AutoServiceKind.IsEndpointService"/> bit set.
        /// </param>
        /// <returns>The type kind on success, null on error (errors - included combination ones - are logged).</returns>
        public CKTypeKind? SetAutoServiceKind( IActivityMonitor m, Type t, AutoServiceKind kind )
        {
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
                m.Error( $"Invalid Auto Service kind registration '{k.ToStringFlags()}' for type '{t}'." );
                return null;
            }
            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsMultipleReasonExternal;
            if( hasProcess ) k |= IsFrontTypeReasonExternal;
            return SetLifetimeOrFrontType( m, t, k );
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
            return SetLifetimeOrFrontType( m, t, CKTypeKind.IsScoped | IsScopedReasonReference );
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

        CKTypeKind? SetLifetimeOrFrontType( IActivityMonitor m, Type t, CKTypeKind kind  )
        {
            bool hasLifetime = (kind & CKTypeKind.LifetimeMask) != 0;
            bool hasFrontType = (kind & CKTypeKind.FrontTypeMask) != 0;
            bool isMultiple = (kind & CKTypeKind.IsMultipleService) != 0;
            bool isMarshallable = (kind & CKTypeKind.IsMarshallable) != 0;

            Debug.Assert( (kind & (IsDefiner | IsSuperDefiner)) == 0, "kind MUST not be a SuperDefiner or a Definer." );
            Debug.Assert( hasLifetime || hasFrontType || isMultiple || isMarshallable, "At least, something must be set." );
            Debug.Assert( (kind&MaskPublicInfo).GetCombinationError( t.IsClass ) == null, (kind&MaskPublicInfo).GetCombinationError( t.IsClass ) );

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
        /// Checks whether the type kind: <see cref="CKTypeKind.IsExcludedType"/> or <see cref="CKTypeKind.HasCombinationError"/> flag set.
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
            return (k & (IsDefiner | IsSuperDefiner | CKTypeKind.IsExcludedType | CKTypeKind.HasCombinationError)) == 0
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
                    Debug.Assert( typeof( EndpointServiceAvailabilityAttribute ).Name == "EndpointServiceAvailabilityAttribute" );
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
                    // As soon as endpointTypes is not null, this is a EndpointService either because
                    // of [EndpointServiceAvailability] or [EndpointSingletonServiceOwner].
                    List<Type>? endpointTypes = null;
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
                            case "EndpointServiceAvailabilityAttribute":
                                {
                                    hasEndpointServiceError |= !ReadEndpointAvailability( m, t, a, ref endpointTypes );
                                    break;
                                }
                            case "EndpointSingletonServiceOwnerAttribute":
                                {
                                    hasEndpointServiceError |= !ReadEndpointSingleton( m, t, a, ref endpointTypes, ref endpointSingletonOwner, ref exclusiveEndpointSingletonOwner );
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
                    if( k == CKTypeKind.None && !hasEndpointServiceError )
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
                        // We now inspect the inherited information: this sets k based on the ancestors.
                        // Since we analyze the ancestors (baseType and allInterfaces), we compute a inheritedEndpointInfo
                        // by combining all base information. When a base EndpointServiceInfo is used, it is locked: it cannot
                        // be extended anymore.
                        EndpointServiceInfo? inheritedEndpointInfo = null;

                        if( hasDefiner )
                        {
                            // Since this is a definer, we can skip any handling of potential Super Definer.
                            // We ignore the base type, we only consider its interfaces (except for EndpointServiceInfo).
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

                                // Handles EndpointService: if i is a EndpointService, then this one is also a endpoint service.
                                if( (kI & CKTypeKind.IsEndpointService) != 0 )
                                {
                                    // The EndpointService info necessarily exists: setting IsEndpointService through SetAutoServiceKind is forbidden.
                                    hasEndpointServiceError |= !BuildInheritedEndpointInfo( m, ref inheritedEndpointInfo, t, _endpointServices[i] );
                                }
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
                                // Handles EndpointService: if base is a EndpointService, then this one is also a endpoint service.
                                if( (kBase & CKTypeKind.IsEndpointService) != 0 )
                                {
                                    // The EndpointService info necessarily exists: setting IsEndpointService through SetAutoServiceKind is forbidden.
                                    hasEndpointServiceError |= !BuildInheritedEndpointInfo( m, ref inheritedEndpointInfo, t, _endpointServices[baseType] );
                                }
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

                                // Handles EndpointService: if base is a EndpointService, then this one is also a endpoint service.
                                if( (kBase & CKTypeKind.IsEndpointService) != 0 )
                                {
                                    // The EndpointService info necessarily exists: setting IsEndpointService through SetAutoServiceKind is forbidden.
                                    hasEndpointServiceError |= !BuildInheritedEndpointInfo( m, ref inheritedEndpointInfo, t, _endpointServices[baseType] );
                                }
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
                                    // Handles EndpointService: if i is a EndpointService, then this one is also a endpoint service.
                                    if( (kI & CKTypeKind.IsEndpointService) != 0 )
                                    {
                                        // The EndpointService info necessarily exists: setting IsEndpointService through SetAutoServiceKind is forbidden.
                                        hasEndpointServiceError |= !BuildInheritedEndpointInfo( m, ref inheritedEndpointInfo, t, _endpointServices[i] );
                                    }
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
                        k &= ~CKTypeKind.HasCombinationError;
                        if( isMultipleInterface ) k |= CKTypeKind.IsMultipleService;
                        if( hasMarshallable ) k |= CKTypeKind.IsMarshallable;
                        if( isPocoClass ) k |= CKTypeKind.IsPocoClass;
                        if( isExcludedType ) k |= CKTypeKind.IsExcludedType;
                        // If hasEndpointServiceError, let k be None (no more processing).
                        if( hasEndpointServiceError ) k = CKTypeKind.None;
                        else
                        {
                            // No Endpoint service error at this level. IsEndpointService from above
                            // has been propagated. If k has the bit set, we have at least one of our ancestor
                            // that is a endpoint service: we are a endpoint service and we have a inheritedEndpointInfo.
                            Debug.Assert( inheritedEndpointInfo == null || inheritedEndpointInfo.ServiceType == t );
                            Debug.Assert( (inheritedEndpointInfo == null) == ((k & CKTypeKind.IsEndpointService) == 0) );

                            // We now have up to 2 source of EndPoint service information:
                            // - The inherited one (inheritedEndpointInfo).
                            // - The attribute defined (in endpointSingletonOwner, exclusiveEndpointSingletonOwner, endpointTypes).
                            // - The external one in _endpointServices.
                            //
                            // We first consider the attributes and infer what we can about the type kind.
                            if( endpointTypes != null )
                            {
                                k |= CKTypeKind.IsEndpointService | CKTypeKind.IsProcessService;
                                m.Info( $"Type '{t}' is a Endpoint service because of a [EndpointSingletonServiceOwner] or [EndpointServiceAvailability] attribute." );
                                if( endpointSingletonOwner != null )
                                {
                                    Debug.Assert( endpointTypes.Contains( t ) );
                                    if( (k & CKTypeKind.IsSingleton) == 0 )
                                    {
                                        k |= CKTypeKind.IsSingleton;
                                        m.Info( $"Type '{t}' is a necessarily a singleton because of the [EndpointSingletonServiceOwner] attribute." );
                                    }
                                }
                            }
                            //
                            EndpointServiceInfo? final = null;
                            // Then we consider the inherited information.
                            // The inheritedEndpointInfo will become the final one: this enables us
                            // to forbid here any IAutoService in inheritance.
                            if( inheritedEndpointInfo != null )
                            {
                                if( endpointTypes == null )
                                {
                                    k |= CKTypeKind.IsEndpointService | CKTypeKind.IsProcessService;
                                    m.Info( $"Type '{t}' is a Endpoint service because it inherits from a Endpoint service." );
                                }
                                Debug.Assert( !inheritedEndpointInfo.IsLocked );
                                // If we are on a IAutoService, we lock the inherited information: IAutoService if used for Endpoint service
                                // must be homogeneous because they ultimately resolve to the same type.
                                if( (k & CKTypeKind.IsAutoService) != 0 )
                                {
                                    inheritedEndpointInfo.Lock( $"This type is an IAutoService. IAutoService Endpoint services cannot be altered." );
                                }
                                // Now that the base is locked if it must be, try to combine with the attributes if any.
                                if( endpointTypes != null )
                                {
                                    hasEndpointServiceError |= !inheritedEndpointInfo.CombineWith( m, endpointSingletonOwner, exclusiveEndpointSingletonOwner, endpointTypes );
                                }
                                if( hasEndpointServiceError )
                                {
                                    m.Error( $"Inherited Endpoint service information conflicts with the one defined by attribute on type '{t}'." );
                                }
                                else
                                {
                                    // No error. Try to combine it with the external configuration if any.
                                    if( _endpointServices.TryGetValue( t, out var exists ) )
                                    {
                                        hasEndpointServiceError |= !inheritedEndpointInfo.CombineWith( m, exists );
                                    }
                                    if( hasEndpointServiceError )
                                    {
                                        m.Error( $"Inherited Endpoint service information conflicts with the one defined externally for type '{t}'." );
                                    }
                                    else
                                    {
                                        // No error. This is fine.
                                        final = inheritedEndpointInfo;
                                    }
                                }
                            }
                            else
                            {
                                // We have no inherited info...
                                if( !_endpointServices.TryGetValue( t, out var exists ) )
                                {
                                    // ...and no external info: final is provided by the attributes if any.
                                    if( endpointTypes != null )
                                    {
                                        final = new EndpointServiceInfo( t, endpointSingletonOwner, exclusiveEndpointSingletonOwner, endpointTypes );
                                    }
                                }
                                else
                                {
                                    // We have an external info.
                                    // If we have no attribute, the external defines the kind.
                                    // Else, if it can be combined with the attributes, its the final.
                                    if( endpointTypes == null )
                                    {
                                        Debug.Assert( k == CKTypeKind.None && !hasEndpointServiceError );
                                        k |= CKTypeKind.IsEndpointService | CKTypeKind.IsProcessService;
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
                                        hasEndpointServiceError |= !exists.CombineWith( m, endpointSingletonOwner, exclusiveEndpointSingletonOwner, endpointTypes );
                                    }
                                    if( hasEndpointServiceError )
                                    {
                                        final = exists;
                                    }
                                }
                            }
                            if( final != null )
                            {
                                // We have a final. The kind has been set.
                                Debug.Assert( (k & CKTypeKind.FrontTypeMask) == CKTypeKind.FrontTypeMask );
                                // We won't allow now a change of the Singleton aspect.
                                // If the final has a owner then we already are a singleton: this has been ensured above.
                                Debug.Assert( final.Owner == null || (k & CKTypeKind.IsSingleton) != 0 );
                                // But the reverse is not true, when no "EndpointSingleton" has been applied,
                                // we can be anything. Why do we care?
                                // If we are a pure IAutoService (no lifetime), it will be computed based on the most
                                // specialized service constructor. And if we are not a auto service we will default to
                                // scoped (this is the work of ComputeFinalTypeKind methods).
                                // But, this is a Endpoint service: singletons are "special" (scoped services are simply not
                                // available from non specified endpoints) because if it happens to be a singleton, we must
                                // have a owner (the EndpointType that will be in charge of creating it and from
                                // which the other ones will pick it up). We cannot randomly assigns it a owner... except the
                                // DefaultEndpointType (with a non exclusive ownership). We really have no other choice and this
                                // seems coherent.
                                // If the lifetime is not yet specified, then we choose Scoped.
                                // If the lifetime is already on error (Scoped & Singleton), we skip this and let the error be signaled below.
                                if( (k & CKTypeKind.LifetimeMask) != CKTypeKind.LifetimeMask )
                                {
                                    if( (k & CKTypeKind.LifetimeMask) == 0 )
                                    {
                                        k |= CKTypeKind.IsScoped;
                                        m.Info( $"Type '{t}' has no associated lifetime. Since it is a Endpoint service, its lifetime will be Scoped." );
                                    }
                                    else if( (k & CKTypeKind.IsSingleton) != 0 && final.Owner == null )
                                    {
                                        // Note that we cannot be here if we have inherited: this job has already been done above and we benefit from it,
                                        // so the final EndpointServiceInfo is necessarily not locked yet.
                                        Debug.Assert( !final.IsLocked );
                                        m.Warn( $"Type '{t}' is a Singleton and an Endpoint service but no owner has been declared. The DefaultEndpointType will be the non exclusive owner." );
                                        final.SetDefaultSingletonOwner();
                                    }
                                }
                                // This locks the owner/exclusive information but EndpointTypes may still be added if
                                // the EndpointServiceInfo is not locked.
                                final.SetTypeProcessed();
                                _endpointServices[ t ] = final;
                            }
                        }
                        // Check for errors and handle IMarshaller<> only if the type is not excluded.
                        if( k != CKTypeKind.None && !isExcludedType && !hasEndpointServiceError )
                        {
                            // Checking errors here that cannot be checked by the central GetCombinationError method.

                            // A type MUST be public only if it is an IAutoService.
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
                                    k |= CKTypeKind.HasCombinationError;
                                }
                                else if( (k & CKTypeKind.IsAutoService) != 0 )
                                {
                                    foreach( var marshaller in allInterfaces.Where( i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof( CK.StObj.Model.IMarshaller<> ) ) )
                                    {
                                        Type marshallable = marshaller.GetGenericArguments()[0];
                                        m.Info( $"Type '{marshallable}' considered as a Marshallable service because a IMarshaller implementation has been found on '{t}' that is a IAutoService." );
                                        SetLifetimeOrFrontType( m, marshallable, CKTypeKind.IsMarshallable | IsMarshallableReasonMarshaller );

                                        // The marshaller interface (the closed generic) is promoted to be an IAutoService since it must be
                                        // mapped (without ambiguities) on the currently registering class (that is itself an IAutoService).
                                        var exists = RawGet( m, marshaller );
                                        if( (exists & CKTypeKind.IsAutoService) == 0 )
                                        {
                                            exists |= CKTypeKind.IsAutoService;
                                            error = exists.GetCombinationError( false );
                                            if( error != null ) m.Error( $"Unable to promote the IMarshaller interface {marshaller.Name} as a IAutoService: {error}" );
                                            else
                                            {
                                                m.Trace( $"Interface {marshaller.Name} is now an IAutoService." );
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
                                    k |= CKTypeKind.HasCombinationError;
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
                                               ref List<Type>? endpointTypes,
                                               ref Type? endpointSingletonOwner,
                                               ref bool exclusiveEndpointSingletonOwner )
            {
                var args = a.ConstructorArguments;
                if( args.Count != 2 )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type endpointType, bool exclusiveEndpoint )] on '{t}': expected 2 arguments (got {args.Count})." );
                    return false;
                }
                if( args[0].Value is not Type tEndpointType
                    || !typeof( EndpointType ).IsAssignableFrom( tEndpointType ) )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type endpointType, bool exclusiveEndpoint )] on '{t}': the endpointType must be a EndpointType (got '{args[0].Value}')." );
                    return false;
                }
                if( args[1].Value is not bool exclusiveEndpoint )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type endpointType, bool exclusiveEndpoint )] on '{t}': the exclusiveEndpoint must be a boolean (got '{args[0].ArgumentType}')." );
                    return false;
                }
                if( endpointSingletonOwner != null && endpointSingletonOwner != tEndpointType )
                {
                    monitor.Error( $"Invalid [EndpointSingletonServiceOwner( Type, bool )] on '{t}': already bound to owner '{endpointSingletonOwner}'." );
                    return false;
                }
                endpointSingletonOwner = tEndpointType;
                exclusiveEndpointSingletonOwner = exclusiveEndpoint;
                endpointTypes = AddEndpoint( endpointTypes, tEndpointType );
                return true;

            }

            static bool ReadEndpointAvailability( IActivityMonitor monitor,
                                                  Type t,
                                                  System.Reflection.CustomAttributeData a,
                                                  ref List<Type>? endpointTypes )
            {
                var args = a.ConstructorArguments;
                if( args.Count != 1 )
                {
                    monitor.Error( $"Invalid [EndpointServiceAvailability( Type endpointType )] on '{t}': expected a single argument (got {args.Count})." );
                    return false;
                }
                if( args[0].Value is not Type tEndpointType
                    || !typeof( EndpointType ).IsAssignableFrom( tEndpointType ) )
                {
                    monitor.Error( $"Invalid [EndpointServiceAvailability( Type endpointType )] on '{t}': the endpointType must be a EndpointType (got '{args[0].Value}')." );
                    return false;
                }
                endpointTypes = AddEndpoint( endpointTypes, tEndpointType );
                return true;
            }

            static List<Type> AddEndpoint( List<Type>? endpointTypes, Type tEndpointType )
            {
                if( endpointTypes == null )
                {
                    endpointTypes = new List<Type>() { tEndpointType };
                }
                else
                {
                    // Just in case both EndpointServiceAvailabilityAttribute and EndpointSingletonServiceOwner are used
                    // or duplicated types with EndpointServiceAvailabilityAttribute.
                    if( !endpointTypes.Contains( tEndpointType ) ) endpointTypes.Add( tEndpointType );
                }
                return endpointTypes;
            }
        }

        bool BuildInheritedEndpointInfo( IActivityMonitor monitor,
                                         ref EndpointServiceInfo? inheritedEndpointInfo,
                                         Type serviceType,
                                         EndpointServiceInfo baseInfo )
        {
            Debug.Assert( _cache.ContainsKey( baseInfo.ServiceType ) && ( _cache[baseInfo.ServiceType] & CKTypeKind.IsEndpointService) != 0,
                         "baseInfo.ServiceType must be registered as an IsEndpointService." );
            // Lock the base info: it can no more be extended.
            baseInfo.Lock( $"This has been used to initialize specialized '{serviceType.ToCSharpName()}' type." );
            if( inheritedEndpointInfo == null )
            {
                inheritedEndpointInfo = new EndpointServiceInfo( serviceType, baseInfo );
                return true;
            }
            return inheritedEndpointInfo.CombineWith( monitor, baseInfo );
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
            if( (t & IsMarshallableReasonMarshaller) != 0 ) c += " [FrontType:MarshallableSinceMarshallerExists]";
            if( (t & IsFrontTypeReasonExternal) != 0 ) c += " [FrontType:External]";
            if( (t & IsMultipleReasonExternal) != 0 ) c += " [Multiple:External]";
            return c;
        }

    }

}
