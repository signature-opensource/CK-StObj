using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace CK.Setup
{
    /// <summary>
    /// Detector for <see cref="CKTypeKind"/>.
    /// </summary>
    public class CKTypeKindDetector
    {
        const int PrivateStart = 512;

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
        const CKTypeKind IsMarshallableReasonMarshaller = (CKTypeKind)(PrivateStart << 8);

        // The lifetime reason is an external definition (applies to IsSingleton and IsScoped).
        const CKTypeKind IsLifetimeReasonExternal = (CKTypeKind)(PrivateStart << 9);

        // The front type reason is an external definition (applies to IsMarshallable and IsFrontOnly).
        const CKTypeKind IsFrontTypeReasonExternal = (CKTypeKind)(PrivateStart << 10);

        // The IsMultiple reason is an external definition.
        const CKTypeKind IsMultipleReasonExternal = (CKTypeKind)(PrivateStart << 11);


        readonly Dictionary<Type, CKTypeKind> _cache;

        /// <summary>
        /// Initializes a new detector.
        /// </summary>
        public CKTypeKindDetector()
        {
            _cache = new Dictionary<Type, CKTypeKind>( 1024 );
        }

        /// <summary>
        /// Gets whether a registered type is known to be a singleton.
        /// </summary>
        /// <param name="t">The already registered type.</param>
        /// <returns>True if this is a singleton.</returns>
        public bool IsSingleton( Type t ) => _cache.TryGetValue( t, out var i ) && (i&CKTypeKind.IsSingleton) != 0;

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>) for a type.
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a Front service).
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <param name="kind">The kind of service. Must not be <see cref="AutoServiceKind.None"/>.</param>
        /// <returns>The type kind on success, null on error (errors - included combination ones - are logged).</returns>
        public CKTypeKind? SetAutoServiceKind( IActivityMonitor m, Type t, AutoServiceKind kind )
        {
            if( kind == AutoServiceKind.None ) throw new ArgumentException( nameof( kind ) );

            bool hasFrontType = (kind & (AutoServiceKind.IsFrontProcessService|AutoServiceKind.IsFrontService|AutoServiceKind.IsMarshallable)) != 0;
            bool hasLifetime = (kind & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) != 0;
            bool hasMultiple = (kind & AutoServiceKind.IsMultipleService) != 0;

            CKTypeKind k = (CKTypeKind)kind;
            if( hasFrontType ) k |= CKTypeKind.IsFrontProcessService | IsFrontTypeReasonExternal;
            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsLifetimeReasonExternal;
            return SetLifetimeOrFrontType( m, t, k );
        }

        /// <summary>
        /// Defines a type as being a <see cref="CKTypeKind.IsSingleton"/> because it is used
        /// as a ctor parameter of a Singleton Service.
        /// Can be called multiple times as long as lifetime is Singleton.
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <returns>The type kind on success, null on error.</returns>
        internal CKTypeKind? DefineAsSingletonReference( IActivityMonitor m, Type t )
        {
            return SetLifetimeOrFrontType( m, t, CKTypeKind.IsSingleton | IsSingletonReasonReference );
        }

        /// <summary>
        /// Promotes a type to be a singleton: it is good to be a singleton (for performance reasons).
        /// This is acted at the end of the process of handling services once we know that nothing
        /// prevents a <see cref="IAutoService"/> to be a singleton.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type to promote.</param>
        /// <returns>The type kind on success, null on error.</returns>
        internal CKTypeKind? PromoteToSingleton( IActivityMonitor m, Type t )
        {
            return SetLifetimeOrFrontType( m, t, CKTypeKind.IsSingleton | IsSingletonReasonFinal );
        }

        CKTypeKind? SetLifetimeOrFrontType( IActivityMonitor m, Type t, CKTypeKind kind  )
        {
            bool hasLifetime = (kind & CKTypeKind.LifetimeMask) != 0;
            bool hasFrontType = (kind & CKTypeKind.FrontTypeMask) != 0;
            bool isMultiple = (kind & CKTypeKind.IsMultipleService) != 0;
            bool isMarshallable = (kind & CKTypeKind.IsMarshallable) != 0;

            Debug.Assert( (kind & (IsDefiner|IsSuperDefiner)) == 0
                          // At least, something must be set.
                          && (hasLifetime || hasFrontType || isMultiple || isMarshallable)
                          // If lifetime is set, it cannot be both Scoped and Singleton.
                          && (!hasLifetime || (kind & CKTypeKind.LifetimeMask) != CKTypeKind.LifetimeMask )
                          // If front type is set, it cannot be both Marshallable and FrontOnly.
                          && (!hasFrontType || (kind & CKTypeKind.FrontTypeMask) != CKTypeKind.FrontTypeMask) );

            // This registers the type (as long as the Type detection is concerned): there is no difference between Registering first
            // and then defining lifetime or the reverse. (This is not true for the full type registration: SetLifetimeOrFrontType must
            // not be called for an already registered type.)
            var exist = RawGet( m, t );
            if( (exist & (IsDefiner|IsSuperDefiner)) != 0 )
            {
                throw new Exception( $"Type '{t}' is a Definer or a SuperDefiner. It cannot be defined as {ToStringFull( kind )}." );
            }
            var updated = exist | kind;
            string error = (updated & MaskPublicInfo).GetCombinationError( t.IsClass );
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
        /// Checks whether the type supports a IAutoService, IScopedAutoService, ISingletonAutoService, IFrontAutoService, IMarshalledAutoService, 
        /// or IRealObject interface or has been explicitly registered as a <see cref="CKTypeKind.IsScoped"/> or <see cref="CKTypeKind.IsSingleton"/>.
        /// <para>
        /// Only the interface name matters (namespace is ignored) and the interface
        /// must be a pure marker, there must be no declared members.
        /// </para>
        /// <para>
        /// The result can be <see cref="CKTypeKindExtension.IsNoneOrInvalid(CKTypeKind)"/>.
        /// </para>
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type that can be an interface or a class.</param>
        /// <returns>The CK type kind (may be invalid).</returns>
        public CKTypeKind GetKind( IActivityMonitor m, Type t )
        {
            var k = RawGet( m, t );
            return (k & (IsDefiner|IsSuperDefiner)) == 0
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
                var baseType = t.BaseType;
                var allInterfaces = t.GetInterfaces();
                // First handles the pure interface that have no base interfaces and no members: this can be one of our marker interfaces.
                // We must also handle here interfaces that have one base because IScoped/SingletonAutoService/IFrontAutoService are extending IAutoService...
                // ...and unfortunaltely we must also consider the ones with 2 base interfaces because of IMarshallableAutoService that extends IFrontAutoService
                // ...and unfortunaltely we must also consider the ones with 2 base interfaces because of IFrontAutoService that extends IFrontProcessAutoService
                // that extends IFrontAutoService. 
                if( t.IsInterface
                    && allInterfaces.Length <= 3
                    && t.GetMembers().Length == 0 )
                {
                    if( t.Name == nameof( IRealObject ) ) k = CKTypeKind.RealObject | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IAutoService ) ) k = CKTypeKind.IsAutoService | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IScopedAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsScoped | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( ISingletonAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsSingleton | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IFrontProcessAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsFrontProcessService | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IFrontAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsFrontService | IsDefiner | IsReasonMarker;
                    else if( t == typeof( IPoco ) ) k = CKTypeKind.IsPoco | IsDefiner | IsReasonMarker;
                }
                if( k == CKTypeKind.None )
                {
                    Debug.Assert( typeof( CKTypeSuperDefinerAttribute ).Name == "CKTypeSuperDefinerAttribute" );
                    Debug.Assert( typeof( CKTypeDefinerAttribute ).Name == "CKTypeDefinerAttribute" );
                    Debug.Assert( typeof( IsMultipleAttribute ).Name == "IsMultipleAttribute" );
                    Debug.Assert( typeof( IsMarshallableAttribute ).Name == "IsMarshallableAttribute" );
                    bool hasSuperDefiner = t.GetCustomAttributesData().Any( a => a.AttributeType.Name == "CKTypeSuperDefinerAttribute" );
                    bool hasDefiner = t.GetCustomAttributesData().Any( a => a.AttributeType.Name == "CKTypeDefinerAttribute" );
                    if( hasSuperDefiner )
                    {
                        if( hasDefiner )
                        {
                            m.Warn( $"Attribute [CKTypeDefiner] defined on type '{t}' is useless since [CKTypeSuperDefiner] is also defined." );
                        }
                        hasDefiner = true;
                    }
                    if( hasDefiner )
                    {
                        // If this is a definer, we can skip any handling of potential Super Definer.
                        // We also clear any IsMultipleService since this flag is not transitive.
                        foreach( var i in allInterfaces )
                        {
                            k |= RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner | CKTypeKind.IsMultipleService);
                            Debug.Assert( (k & CKTypeKind.IsMarshallable) == 0, "IsMarshallable is for classes only." );
                        }
                        k |= IsDefiner;
                        if( hasSuperDefiner ) k |= IsSuperDefiner;
                    }
                    else
                    {
                        Debug.Assert( k == CKTypeKind.None );
                        // If the base type is a SuperDefiner, then this is a Definer.
                        if( baseType != null )
                        {
                            // IsMarshallable is not propagated.
                            var kBase = RawGet( m, baseType ) & ~CKTypeKind.IsMarshallable;
                            Debug.Assert( (kBase&CKTypeKind.IsMultipleService) == 0, "IsMultipleService is for interfaces only." );
                            if( (kBase & IsSuperDefiner) != 0 )
                            {
                                Debug.Assert( (kBase & IsDefiner) != 0 );
                                k = kBase & ~IsSuperDefiner;
                            }
                            else k = kBase & ~IsDefiner;
                        }
                        if( (k&IsDefiner) != 0 )
                        {
                            // If the base type was a SuperDefiner, this is a definer and we can skip any handling of Super Definer.
                            foreach( var i in allInterfaces )
                            {
                                k |= RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner | CKTypeKind.IsMultipleService );
                                Debug.Assert( (k & CKTypeKind.IsMarshallable) == 0, "IsMarshallable is for classes only." );
                            }
                        }
                        else
                        {
                            // We are not (yet?) a Definer.
                            foreach( var i in allInterfaces )
                            {
                                var kI = RawGet( m, i ) & ~(IsDefiner | CKTypeKind.IsMultipleService);
                                Debug.Assert( (kI & CKTypeKind.IsMarshallable) == 0, "IsMarshallable is for classes only." );
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
                    if( t.IsInterface )
                    {
                        if( t.GetCustomAttributesData().Any( a => a.AttributeType.Name == "IsMultipleAttribute" ) )
                        {
                            k |= CKTypeKind.IsMultipleService;
                        }
                    }
                    else
                    {
                        Debug.Assert( t.IsClass );
                        if( t.GetCustomAttributesData().Any( a => a.AttributeType.Name == "IsMarshallableAttribute" ) )
                        {
                            k |= CKTypeKind.IsMarshallable;
                        }
                    }
                    // Check for errors and handle 
                    if( k != CKTypeKind.None )
                    {
                        // Checking errors here that cannot be checked by the central GetCombinationError method.
                        //
                        if( !t.Assembly.IsDynamic && !(t.IsPublic || t.IsNestedPublic) )
                        {
                            m.Error( $"Type '{t}' being '{(k & MaskPublicInfo).ToStringFlags()}' must be public." );
                        }
                        if( t.IsClass )
                        {
                            Debug.Assert( (k & CKTypeKind.IsMultipleService) == 0, "IsMultipleServiceAttribute targets interface only and is not propagated." );
                            if( (k & CKTypeKind.IsAutoService) != 0 )
                            {
                                foreach( var marshaller in allInterfaces.Where( i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof( CK.StObj.Model.IMarshaller<> ) ) )
                                {
                                    Type marshallable = marshaller.GetGenericArguments()[0];
                                    m.Info( $"Type '{marshallable.FullName}' considered as a Marshallable service because a IMarshaller implementation has been found on '{t.FullName}' that is a IAutoService." );
                                    SetLifetimeOrFrontType( m, marshallable, CKTypeKind.IsMarshallable | IsMarshallableReasonMarshaller );

                                    // The marshaller interface (the closed generic) is promoted to be an IAutoService since it must be
                                    // mapped (without ambiguities) on the currently registering class (that is itself an IAutoService).
                                    var exists = RawGet( m, marshaller );
                                    if( (exists & CKTypeKind.IsAutoService) == 0 )
                                    {
                                        exists |= CKTypeKind.IsAutoService;
                                        var error = exists.GetCombinationError( false );
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
                            // We are registering classes (not interfaces): the combination errors for classes are (at least should be) checked by the
                            // root register code.
                            // Here we check and log an error for any interface combination error. Some errors don't propagate to their final classes
                            // like for instance the fact that a IRealObject interface cannot be marked IsMultiple (the attribute applies only to the
                            // interface it decorates).
                            // We may have test here only these kind of specific errors (currently only this one), however we consider that it is better
                            // and safer to rely on the central GetCombinationError() method: this method concentrates all the checks.
                            var error = (k&MaskPublicInfo).GetCombinationError( false );
                            if( error != null ) m.Error( $"Invalid interface '{t.FullName}' kind: {error}" );
                        }
                    }
                }
                _cache.Add( t, k );
            }
            return k;
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
