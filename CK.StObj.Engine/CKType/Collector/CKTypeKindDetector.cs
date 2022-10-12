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
        readonly Func<IActivityMonitor, Type, bool>? _typeFilter;

        /// <summary>
        /// Initializes a new detector.
        /// </summary>
        /// <param name="typeFilter">Optional type filter.</param>
        public CKTypeKindDetector( Func<IActivityMonitor, Type, bool>? typeFilter = null )
        {
            _cache = new Dictionary<Type, CKTypeKind>( 1024 );
            _typeFilter = typeFilter;
        }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>) for a type.
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a Front service).
        /// Note that <see cref="AutoServiceKind.IsFrontService"/> is automatically expanded with <see cref="AutoServiceKind.IsScoped"/>
        /// and <see cref="AutoServiceKind.IsFrontProcessService"/>.
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <param name="kind">The kind of service. Must not be <see cref="AutoServiceKind.None"/>.</param>
        /// <returns>The type kind on success, null on error (errors - included combination ones - are logged).</returns>
        public CKTypeKind? SetAutoServiceKind( IActivityMonitor m, Type t, AutoServiceKind kind )
        {
            Throw.CheckArgument( kind != AutoServiceKind.None );

            bool hasFrontType = (kind & (AutoServiceKind.IsFrontProcessService|AutoServiceKind.IsFrontService)) != 0;
            bool hasLifetime = (kind & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) != 0;
            bool hasMultiple = (kind & AutoServiceKind.IsMultipleService) != 0;

            CKTypeKind k = (CKTypeKind)kind;
            if( hasFrontType )
            {
                if( (kind & AutoServiceKind.IsFrontService) != 0 )
                {
                    k |= CKTypeKind.IsScoped;
                    hasLifetime = true;
                }
                k |= CKTypeKind.IsFrontProcessService;
            }
            string? error = k.GetCombinationError( t.IsClass );
            if( error != null )
            {
                m.Error( $"Invalid Auto Service kind registration '{k.ToStringFlags()}' for type '{t}'." );
                return null;
            }
            if( hasLifetime ) k |= IsLifetimeReasonExternal;
            if( hasMultiple ) k |= IsMultipleReasonExternal;
            if( hasFrontType ) k |= IsFrontTypeReasonExternal;
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
                // We must also handle here interfaces that have one base because IScoped/SingletonAutoService/IFrontAutoService are extending IAutoService...
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
                    else if( t.Name == nameof( IFrontProcessAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsFrontProcessService | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IFrontAutoService ) ) k = CKTypeKind.IsAutoService | CKTypeKind.IsFrontService | CKTypeKind.IsFrontProcessService | CKTypeKind.IsScoped | IsDefiner | IsReasonMarker;
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
                    Debug.Assert( typeof( CKTypeSuperDefinerAttribute ).Name == "CKTypeSuperDefinerAttribute" );
                    Debug.Assert( typeof( CKTypeDefinerAttribute ).Name == "CKTypeDefinerAttribute" );
                    Debug.Assert( typeof( IsMultipleAttribute ).Name == "IsMultipleAttribute" );
                    Debug.Assert( typeof( IsMarshallableAttribute ).Name == "IsMarshallableAttribute" );
                    bool hasSuperDefiner = false;
                    bool hasDefiner = false;
                    bool isMultipleInterface = false;
                    bool hasMarshallable = false;
                    bool isExcludedType = false;

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
                            case "CKTypeDefinerAttribute":
                                hasDefiner = true;
                                break;
                            case "CKTypeSuperDefinerAttribute":
                                hasSuperDefiner = true;
                                break;
                            case "IsMarshallableAttribute":
                                hasMarshallable = true;
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
                            // Since this is a definer, we can skip any handling of potential Super Definer.
                            // We ignore the base type, we only consider its interfaces.
                            // We also clear any IsMultipleService and IsMarshallable since these flags are not transitive.
                            //
                            // (Note that [IsMultiple] may be "transmitted" here but a CKTypeDefiner for "Multiple" interfaces would not be
                            // a good idea: an explicit [IsMultiple] attribute on the interface is much more maintainable.)
                            //
                            foreach( var i in allInterfaces )
                            {
                                k |= RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner | CKTypeKind.IsMultipleService | CKTypeKind.IsMarshallable | IsReasonMarker);
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
                                // "Weak Exclusion": an excluded (or filtered) base type doesn't exclude it specializations.
                                var kBase = RawGet( m, baseType ) & ~(CKTypeKind.IsMarshallable | CKTypeKind.IsExcludedType | IsReasonMarker) ;
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
                        k &= ~CKTypeKind.HasCombinationError;
                        if( isMultipleInterface ) k |= CKTypeKind.IsMultipleService;
                        if( hasMarshallable ) k |= CKTypeKind.IsMarshallable;
                        if( isExcludedType ) k |= CKTypeKind.IsExcludedType;

                        // Check for errors and handle IMarshaller<> only if the type is not excluded.
                        if( k != CKTypeKind.None && !isExcludedType )
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
