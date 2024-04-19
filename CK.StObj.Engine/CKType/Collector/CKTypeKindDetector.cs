using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        const int PrivateStart = 1 << 17;

        /// <summary>
        /// Mask for public information defined in the <see cref="CKTypeKind"/> enumeration.
        /// Internally other flags are used.
        /// </summary>
        public const CKTypeKind MaskPublicInfo = (CKTypeKind)(PrivateStart-1);

        // The type is a service that is scoped because its ctor references a scoped service.
        const CKTypeKind IsScopedReasonReference = (CKTypeKind)(PrivateStart << 3);

        // The service is Marshallable because a IAutoService Marshaller class has been found.
        const CKTypeKind IsMarshallableReasonMarshaller = (CKTypeKind)(PrivateStart << 4);

        // The lifetime reason is an external definition (applies to IsSingleton and IsScoped).
        const CKTypeKind IsLifetimeReasonExternal = (CKTypeKind)(PrivateStart << 5);

        // The IsProcessService reason is an external definition.
        const CKTypeKind IsProcessServiceReasonExternal = (CKTypeKind)(PrivateStart << 6);

        // The IsEndpoint reason is an external definition.
        const CKTypeKind IsEndpointServiceReasonExternal = (CKTypeKind)(PrivateStart << 7);

        // The IsMultiple reason is an external definition.
        const CKTypeKind IsMultipleReasonExternal = (CKTypeKind)(PrivateStart << 8);

        readonly Dictionary<Type, CKTypeKind> _cache;
        readonly Func<IActivityMonitor, Type, bool>? _typeFilter;
        readonly Dictionary<Type, AutoServiceKind> _endpointServices;
        readonly List<Type> _ambientServices;

        /// <summary>
        /// Initializes a new detector.
        /// </summary>
        /// <param name="typeFilter">Optional type filter.</param>
        public CKTypeKindDetector( Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            _cache = new Dictionary<Type, CKTypeKind>( 1024 );
            _endpointServices = new Dictionary<Type, AutoServiceKind>();
            _typeFilter = typeFilter;
            _ambientServices = new List<Type>();
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
        public IReadOnlyList<Type> AmbientServices => _ambientServices;

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>).
        /// <para>
        /// If the <see cref="AutoServiceKind.IsEndpointService"/> bit set, one of the lifetime bits mus be set
        /// (<see cref="AutoServiceKind.IsScoped"/> xor <see cref="AutoServiceKind.IsSingleton"/>).
        /// </para>
        /// <para>
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a service cannot be both scoped and singleton).
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
            bool hasLifetime = (kind & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) != 0;
            bool hasMultiple = (kind & AutoServiceKind.IsMultipleService) != 0;
            bool hasEndpoint = (kind & AutoServiceKind.IsEndpointService) != 0;

            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsMultipleReasonExternal;
            if( hasEndpoint ) k |= IsEndpointServiceReasonExternal;

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

        CKTypeKind? SetLifetimeOrProcessType( IActivityMonitor m, Type t, CKTypeKind kind  )
        {
            Throw.DebugAssert( "kind MUST not be a SuperDefiner or a Definer.", ( kind & (CKTypeKind.IsDefiner | CKTypeKind.IsSuperDefiner)) == 0 );
            Throw.DebugAssert( (kind & MaskPublicInfo).GetCombinationError( t.IsClass )!, ( kind & MaskPublicInfo).GetCombinationError( t.IsClass ) == null );
            Throw.DebugAssert( "At least, something must be set.", ( kind & CKTypeKindExtension.LifetimeMask | CKTypeKind.IsMultipleService | CKTypeKindExtension.AmbientServiceFlags) != 0 );

            // This registers the type (as long as the Type detection is concerned): there is no difference between Registering first
            // and then defining lifetime or the reverse. (This is not true for the full type registration: SetLifetimeOrFrontType must
            // not be called for an already registered type.)
            var exist = RawGet( m, t );
            if( (exist & (CKTypeKind.IsDefiner|CKTypeKind.IsSuperDefiner)) != 0 )
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
            if( (updated & CKTypeKind.IsEndpointService) != 0 )
            {
                _endpointServices[t] = updated.ToAutoServiceKind();
                if( (updated & CKTypeKind.IsAmbientService) != 0
                    && !_ambientServices.Contains( t ) )
                {
                    _ambientServices.Add( t );
                }
            }

            Debug.Assert( (updated & (CKTypeKind.IsDefiner | CKTypeKind.IsSuperDefiner)) == 0 );
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
            return (k & (CKTypeKind.IsDefiner | CKTypeKind.IsSuperDefiner)) == 0
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
            return (k & (CKTypeKind.IsDefiner | CKTypeKind.IsSuperDefiner | CKTypeKind.IsExcludedType | CKTypeKind.HasError)) == 0
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
                if( t.IsGenericType && !t.IsGenericTypeDefinition )
                {
                    // A Generic Type definition can be a (Super)Definer or be a multiple service definition: this
                    // applies directly to the specialized type.
                    // Even the IsMarshallable is kept: we consider that a generic marshaller is possible!
                    // We also keep excluded type flags here: it seems appropriate that by excluding the open generics (IProcessor<T>), the intent
                    // is to exclude the closed ones (IProcessor<Document>) since excluding specifically the open one has no real meaning.
                    var tGen = t.GetGenericTypeDefinition();
                    k = RawGet( m, tGen );
                }
                else
                {
                    var baseType = t.BaseType;
                    if( baseType == typeof( object ) ) baseType = null;
                    // Internal interfaces are "transparent". They can bring some CKomposable interface (IAutoService, etc.)
                    // but are ignored.
                    // An "internal interface" is simply ignored because no public interfaces can extend it (Error CS0061: Inconsistent accessibility).
                    // Implementations are free to define and use them.
                    // There's one gotcha with this: when duck typing is used (with locally defined internal interfaces), this doesn't work.
                    // The planned solution is to stop using duck typing and to introduce a CK.Abstraction base assembly that will define
                    // once for all the CKomposable interfaces and attributes.
                    var allInterfaces = t.GetInterfaces().Where( i => i.IsPublic || i.IsNestedPublic ).ToArray();

                    // First handles the pure interface that have no base interfaces and no members: this can be one of our marker interfaces.
                    // We must also handle here interfaces that have one base because IScoped/SingletonAutoService/IProcessAutoService
                    // are extending IAutoService.
                    if( t.IsInterface
                        && allInterfaces.Length <= 1
                        && t.GetMembers().Length == 0 )
                    {
                        if( t.Name == nameof( IRealObject ) ) k = CKTypeKindExtension.RealObjectFlags | CKTypeKind.IsDefiner;
                        else if( t.Name == nameof( IAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsDefiner;
                        else if( t.Name == nameof( IScopedAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsScoped | CKTypeKind.IsDefiner;
                        else if( t.Name == nameof( ISingletonAutoService ) ) k = /*CKTypeKindExtension.SingletonFlags*/
                                                                                 CKTypeKind.IsSingleton   | CKTypeKind.IsAutoService | CKTypeKind.IsDefiner;
                        else if( t == typeof( IPoco ) ) k = CKTypeKind.IsPoco | CKTypeKind.IsDefiner;
                    }
                    // If it's not one of the interface marker and it's not an internal interface, we analyze it.
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
                        Debug.Assert( typeof( SingletonServiceAttribute ).Name == "SingletonServiceAttribute" );
                        bool hasSuperDefiner = false;
                        bool hasDefiner = false;
                        bool isMultipleInterface = false;
                        bool hasSingletonService = false;
                        bool isExcludedType = false;
                        bool isEndpointScoped = false;
                        bool isAmbientService = false;
                        bool isEndpointSingleton = false;

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
                                m.Trace( $"Type '{t:N}' is [StObjGen]. It is ignored." );
                                break;
                            }
                            switch( n )
                            {
                                case "ExcludeCKTypeAttribute":
                                    isExcludedType = true;
                                    break;
                                case "EndpointScopedServiceAttribute":
                                    isEndpointScoped = true;
                                    isAmbientService = a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is bool b && b;
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
                                case "SingletonServiceAttribute":
                                    hasSingletonService = true;
                                    break;
                                case "IsMultipleAttribute" when t.IsInterface:
                                    isMultipleInterface = true;
                                    break;
                            }
                        }

                        Debug.Assert( k == CKTypeKind.None || k == CKTypeKind.IsExcludedType );
                        if( k == CKTypeKind.None )
                        {
                            isExcludedType |= _typeFilter != null && !_typeFilter( m, t );

                            // Normalizes SuperDefiner => Definer (and emits a warning).
                            if( hasSuperDefiner )
                            {
                                if( hasDefiner )
                                {
                                    m.Warn( $"Attribute [CKTypeDefiner] defined on type '{t:N}' is useless since [CKTypeSuperDefiner] is also defined." );
                                }
                                hasDefiner = true;
                            }
                            // Type's attributes have been analyzed, IsDefiner is normalized.
                            // It's time to apply the bases.
                            var allBases = allInterfaces.Where( i => !allInterfaces.Any( baseInterface => i != baseInterface && i.IsAssignableFrom( baseInterface ) ) );
                            if( baseType != null )
                            {
                                allBases = allBases.Where( i => !i.IsAssignableFrom( baseType ) ).Append( baseType );
                            }
                            foreach( var i in allBases  )
                            {
                                var kI = RawGet( m, i ) & ~(CKTypeKind.IsDefiner | CKTypeKind.IsMultipleService | CKTypeKind.IsExcludedType);
                                if( (k & CKTypeKind.IsDefiner) == 0 // We are not yet a Definer...
                                    && (kI & CKTypeKind.IsSuperDefiner) != 0 ) // ...but this base is a SuperDefiner.
                                {
                                    kI |= CKTypeKind.IsDefiner;
                                }
                                k |= kI & ~CKTypeKind.IsSuperDefiner;
                            }
                            // Applying the direct flags. Any inherited combination error is cleared.
                            k &= ~CKTypeKind.HasError;
                            if( isMultipleInterface ) k |= CKTypeKind.IsMultipleService;
                            if( isExcludedType ) k |= CKTypeKind.IsExcludedType;
                            if( hasSingletonService ) k |= CKTypeKind.IsSingleton;
                            if( isEndpointSingleton ) k |= CKTypeKind.IsEndpointService | CKTypeKind.IsPerContextSingleton;
                            if( hasSuperDefiner ) k |= CKTypeKind.IsSuperDefiner;
                            if( hasDefiner ) k |= CKTypeKind.IsDefiner;
                            if( isAmbientService ) k |= CKTypeKind.IsAmbientService | CKTypeKind.IsEndpointService | CKTypeKind.IsScoped;
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
                                        m.Error( $"Type '{t:N}' being '{(k & MaskPublicInfo).ToStringFlags()}' must be public." );
                                        k |= CKTypeKind.HasError;
                                    }
                                }
                                else if( (k & CKTypeKind.IsEndpointService) != 0 )
                                {
                                    if( !isPublic )
                                    {
                                        k &= ~CKTypeKind.IsEndpointService;
                                        m.Info( $"Type '{t:N}' is an internal EndpointService. Its kind will only be '{(k & MaskPublicInfo).ToStringFlags()}'." );
                                    }
                                }
                                if( t.IsClass )
                                {
                                    Debug.Assert( (k & CKTypeKind.IsMultipleService) == 0, "IsMultipleAttribute targets interface only and is not propagated." );
                                    // Always use the central GetCombinationError() method when possible: this method concentrates all the checks.
                                    var error = (k & MaskPublicInfo).GetCombinationError( true );
                                    if( error != null )
                                    {
                                        m.Error( $"Invalid class '{t:N}' kind: {error}" );
                                        k |= CKTypeKind.HasError;
                                    }
                                }
                                else
                                {
                                    Debug.Assert( t.IsInterface );
                                    var error = (k & MaskPublicInfo).GetCombinationError( false );
                                    if( error != null )
                                    {
                                        m.Error( $"Invalid interface '{t:N}' kind: {error}" );
                                        k |= CKTypeKind.HasError;
                                    }
                                }
                                // Registers special services: endpoints and ambient: ignores the processwide singleton services.
                                if( (k & CKTypeKind.IsEndpointService) != 0 )
                                {
                                    _endpointServices.Add( t, k.ToAutoServiceKind() );
                                    if( (k & CKTypeKind.IsAmbientService) != 0 )
                                    {
                                        _ambientServices.Add( t );
                                    }
                                }
                            }
                        }
                    }
                }
                // Always registers the kind whatever it is.
                // The type is registered.
                // Ite missa est.
                _cache.Add( t, k );
            }
            return k;
        }

        static string ToStringFull( CKTypeKind t )
        {
            var c = (t & MaskPublicInfo).ToStringFlags();
            if( (t & CKTypeKind.IsDefiner) != 0 ) c += " [IsDefiner]";
            if( (t & CKTypeKind.IsSuperDefiner) != 0 ) c += " [IsSuperDefiner]";
            if( (t & IsLifetimeReasonExternal) != 0 ) c += " [Lifetime:External]";
            if( (t & IsScopedReasonReference) != 0 ) c += " [Lifetime:UsesScoped]";
            if( (t & IsMarshallableReasonMarshaller) != 0 ) c += " [Marshallable:MarshallerExists]";
            if( (t & IsProcessServiceReasonExternal) != 0 ) c += " [ProcessService:External]";
            if( (t & IsMultipleReasonExternal) != 0 ) c += " [Multiple:External]";
            return c;
        }

    }

}
