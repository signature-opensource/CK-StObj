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
        const CKTypeKind MaskPublicInfo = (CKTypeKind)31;
        const CKTypeKind IsDefiner = (CKTypeKind)32;
        const CKTypeKind IsSuperDefiner = (CKTypeKind)64;

        // The lifetime reason is the interface marker (applies to IsSingleton and IsScoped).
        const CKTypeKind IsReasonMarker = (CKTypeKind)128;
        // The lifetime reason is an external definition (applies to IsSingleton and IsScoped).
        const CKTypeKind IsReasonExternal = (CKTypeKind)256;
        // The type is singleton because it is used as a:
        // - ctor parameter of a Singleton Service.
        // - property or StObjConstruct/StObjFinalize parameter of a Real Object.
        const CKTypeKind IsSingletonReasonReference = (CKTypeKind)512;
        // The type is a singleton because nothing prevents it to be a singleton.
        const CKTypeKind IsSingletonReasonFinal = (CKTypeKind)1024;
        // The type is a service that is scoped because its ctor references a scoped service.
        const CKTypeKind IsScopedReasonReference = (CKTypeKind)2048;

        readonly struct Cache
        {
            public readonly Type Base;
            public readonly Type[] Interfaces;
            public readonly CKTypeKind Kind;

            public Cache( Type b, Type[] i, CKTypeKind k )
            {
                Base = b;
                Interfaces = i;
                Kind = k;
            }
        }

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
        /// Defines a type as being a <see cref="CKTypeKind.IsSingleton"/>.
        /// Can be called multiple times as long as no different registration already exists.
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <returns>The type kind on success, null on error.</returns>
        public CKTypeKind? DefineAsExternalSingleton( IActivityMonitor m, Type t )
        {
            return SetLifeTime( m, t, CKTypeKind.IsSingleton | IsReasonExternal );
        }

        /// <summary>
        /// Defines a type as being a <see cref="CKTypeKind.IsSingleton"/> because it is used
        /// as a ctor parameter of a Singleton Service.
        /// Can be called multiple times as long as lifetime is Singleton.
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <returns>The type kind on success, null on error.</returns>
        public CKTypeKind? DefineAsSingletonReference( IActivityMonitor m, Type t )
        {
            return SetLifeTime( m, t, CKTypeKind.IsSingleton | IsSingletonReasonReference );
        }

        /// <summary>
        /// Defines a type as being a pure <see cref="CKTypeKind.IsScoped"/>.
        /// Can be called multiple times as long as no different registration already exists.
        /// </summary>
        /// <param name="m">The monitor.</param>
        /// <param name="t">The type to register.</param>
        /// <returns>The type kind on success, null on error.</returns>
        public CKTypeKind? DefineAsExternalScoped( IActivityMonitor m, Type t )
        {
            return SetLifeTime( m, t, CKTypeKind.IsScoped | IsReasonExternal );
        }

        /// <summary>
        /// Promotes a type to be a singleton: it is good to be a singleton (for performance reasons).
        /// This is acted at the end of the process of handling services once we know that nothing
        /// prevents a <see cref="IAutoService"/> to be a singleton.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="t">The type to promote.</param>
        /// <returns>The type kind on success, null on error.</returns>
        public CKTypeKind? PromoteToSingleton( IActivityMonitor m, Type t )
        {
            return SetLifeTime( m, t, CKTypeKind.IsSingleton | IsSingletonReasonFinal );
        }

        CKTypeKind? SetLifeTime( IActivityMonitor m, Type t, CKTypeKind kind  )
        {
            Debug.Assert( (kind & IsDefiner) == 0
                          && (kind & (CKTypeKind.IsScoped | CKTypeKind.IsSingleton)) != 0
                          && (kind & (CKTypeKind.IsScoped | CKTypeKind.IsSingleton)) != (CKTypeKind.IsScoped | CKTypeKind.IsSingleton) );
            var k = RawGet( m, t );
            if( (k & (IsDefiner|IsSuperDefiner)) != 0 )
            {
                throw new Exception( $"Type '{t}' is a Definer or a SuperDefiner. It cannot be defined as {ToStringFull( kind )}." );
            }
            var kType = k & (CKTypeKind.IsScoped | CKTypeKind.IsSingleton);
            Debug.Assert( kType != (CKTypeKind.IsScoped | CKTypeKind.IsSingleton) );
            if( kType != CKTypeKind.None && kType != (kind & (CKTypeKind.IsScoped | CKTypeKind.IsSingleton)) )
            {
                m.Error( $"Type '{t}' is already registered as a '{ToStringFull( k )}'. It can not be defined as {ToStringFull( kind )}." );
                return null;
            }
            k |= kind;
            _cache[t] = k;
            Debug.Assert( (k & (IsDefiner | IsSuperDefiner)) == 0 );
            return k & MaskPublicInfo;
        }

        /// <summary>
        /// Checks whether the type supports a IAutoService, IScopedAutoService, ISingletonAutoService
        /// or IRealObject interface or has been explicitly registered as a <see cref="CKTypeKind.IsScoped"/>
        /// or <see cref="CKTypeKind.IsSingleton"/>.
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
            return (k & IsDefiner) == 0
                        ? k & MaskPublicInfo
                        : CKTypeKind.None;
        }

        CKTypeKind RawGet( IActivityMonitor m, Type t )
        {
            if( !_cache.TryGetValue( t, out var k ) )
            {
                var allInterfaces = t.GetInterfaces();
                var baseType = t.BaseType;
                // First handles the pure interface that have no base interfaces and no members: this can be one of our marker interfaces.
                // We must also handle here interfaces that have one base because IScoped/SingletonAutoService are extending IAutoService...
                if( t.IsInterface
                    && allInterfaces.Length <= 1
                    && t.GetMembers().Length == 0 )
                {
                    if( t.Name == nameof( IRealObject ) ) k = CKTypeKind.RealObject | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IAutoService ) ) k = CKTypeKind.IsAutoService | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( IScopedAutoService ) ) k = CKTypeKind.AutoScoped | IsDefiner | IsReasonMarker;
                    else if( t.Name == nameof( ISingletonAutoService ) ) k = CKTypeKind.AutoSingleton | IsDefiner | IsReasonMarker;
                    else if( t == typeof( IPoco ) ) k = CKTypeKind.IsPoco | IsDefiner | IsReasonMarker;
                }
                if( k == CKTypeKind.None )
                {
                    Debug.Assert( typeof( CKTypeSuperDefinerAttribute ).Name == "CKTypeSuperDefinerAttribute" );
                    Debug.Assert( typeof( CKTypeDefinerAttribute ).Name == "CKTypeDefinerAttribute" );
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
                        foreach( var i in allInterfaces )
                        {
                            k |= RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner);
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
                            var kBase = RawGet( m, baseType );
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
                                k |= RawGet( m, i ) & ~(IsDefiner | IsSuperDefiner);
                            }
                        }
                        else
                        {
                            // We are not (yet?) a Definer.
                            foreach( var i in allInterfaces )
                            {
                                var kI = RawGet( m, i ) & ~IsDefiner;
                                if( (k & IsDefiner) == 0 // We are not yet a Definer...
                                    && (kI & IsSuperDefiner) != 0 ) // ...but this base is a SuperDefiner.
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
                }
                if( k != CKTypeKind.None )
                {
                    if( !t.Assembly.IsDynamic && !(t.IsPublic || t.IsNestedPublic) )
                    {
                        m.Error( $"Type '{t}' being '{(k & MaskPublicInfo).ToStringClear( t.IsClass )}' must be public." );
                    }
                    if( (k & CKTypeKind.IsPoco) == CKTypeKind.IsPoco && t.IsClass )
                    {
                        m.Error( $"Class '{t}' cannot be a IPoco: only interfaces can be IPoco." );
                    }
                }
                _cache.Add( t, k );
            }
            return k;
        }
        static string ToStringFull( CKTypeKind t )
        {
            var c = (t & MaskPublicInfo).ToStringClear();
            if( (t & IsDefiner) != 0 ) c += " [IsDefiner]";
            if( (t & IsSuperDefiner) != 0 ) c += " [IsSuperDefiner]";
            if( (t & IsReasonMarker) != 0 ) c += " [Marker]";
            if( (t & IsReasonExternal) != 0 ) c += " [External]";
            if( (t & IsSingletonReasonReference) != 0 ) c += " [ReferencedBySingleton]";
            if( (t & IsSingletonReasonFinal) != 0 ) c += " [OpimizedAsSingleton]";
            if( (t & IsScopedReasonReference) != 0 ) c += " [UsesScoped]";
            return c;
        }

    }

}
