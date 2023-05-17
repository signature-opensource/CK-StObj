using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace CK.Setup
{
    /// <summary>
    /// Captures <see cref="EndpointScopedServiceAttribute"/>, <see cref="EndpointSingletonServiceAttribute"/>,
    /// <see cref="EndpointScopedServiceTypeAttribute"/> and <see cref="EndpointSingletonServiceTypeAttribute"/>
    /// attributes declaration.
    /// </summary>
    public sealed class CKTypeEndpointServiceInfo
    {
        readonly Type _serviceType;
        // One of these is null.
        readonly List<Type>? _scoped;
        readonly List<(Type Definition, Type? Owner)>? _singletons;
        // Final processed kind (may have CKTypeKind.HasError).
        CKTypeKind _kind;

        /// <summary>
        /// Gets the type that is a endpoint service.
        /// </summary>
        public Type ServiceType => _serviceType;

        /// <summary>
        /// Gets whether this is a scoped service.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Scoped ) )]
        [MemberNotNullWhen( false, nameof( Singletons ) )]
        public bool IsScoped => _scoped != null;

        /// <summary>
        /// Gets whether this is a singleton service.
        /// </summary>
        [MemberNotNullWhen( true, nameof( Singletons ) )]
        [MemberNotNullWhen( false, nameof( Scoped ) )]
        public bool IsSingleton => _scoped == null;

        /// <summary>
        /// Gets the endpoint types that expose this service as a scoped service (if <see cref="IsScoped"/> is true).
        /// </summary>
        public IReadOnlyList<Type>? Scoped => _scoped;

        /// <summary>
        /// Gets the endpoint types that expose this service as a singleton (if this service is singleton).
        /// </summary>
        public IReadOnlyList<(Type Definition, Type? Owner)>? Singletons => _singletons;

        /// <summary>
        /// Gets the type kind of <see cref="ServiceType"/>.
        /// </summary>
        public CKTypeKind Kind => _kind;

        // New singleton.
        internal CKTypeEndpointServiceInfo( Type serviceType, Type definition, Type? owner )
        {
            Debug.Assert( owner == null || owner != definition, "null is the marker for owning definition." );
            _serviceType = serviceType;
            _singletons = new List<(Type Definition, Type? Owner)>() { (definition, owner) };
        }

        // New scoped.
        internal CKTypeEndpointServiceInfo( Type serviceType, Type definition )
        {
            _serviceType = serviceType;
            _scoped = new List<Type>() { definition };
        }

        // Orphan constructor.
        internal CKTypeEndpointServiceInfo( Type t, bool isScoped )
        {
            _serviceType = t;
            if( isScoped ) _scoped = new List<Type>();
            else _singletons = new List<(Type Definition, Type? Owner)>();
        }

        internal static ReadOnlySpan<char> DefinitionName( Type definition ) => definition.Name.AsSpan( 0, definition.Name.Length - 18 );

        internal void SetTypeProcessed( CKTypeKind processedKind )
        {
            Debug.Assert( _kind == CKTypeKind.None );
            Debug.Assert( (processedKind & CKTypeKind.IsEndpointService) != 0 );
            _kind = processedKind;
        }

        internal bool HasBeenProcessed => _kind != 0;

        internal bool CombineScopedWith( IActivityMonitor monitor, Type definition )
        {
            if( _scoped == null )
            {
                monitor.Error( $"Endpoint service '{_serviceType}' has already been declared as a singleton, it cannot be scoped for '{DefinitionName(definition)}'." );
                return false;
            }
            if( !_scoped.Contains( definition ) ) _scoped.Add( definition );
            return true;
        }

        internal bool CombineSingletonWith( IActivityMonitor monitor, Type definition, Type? owner )
        {
            Debug.Assert( owner == null || owner != definition, "null is the marker for owning definition." );
            if( _singletons == null )
            {
                monitor.Error( $"Endpoint service '{_serviceType}' has already been declared as a scoped, it cannot be singleton for '{DefinitionName( definition )}'." );
                return false;
            }
            // We can coalesce the registrations here.
            // First:
            //  - If the exact same definition already exists, obviously we have nothing to do.
            //  - If one exists with a null owner, whatever the specified owner is, it is weaker than the self owned declaration: we also
            //    have nothing to do.
            int existIdx = _singletons.IndexOf( e => e.Definition == definition && (e.Owner == owner || e.Owner == null) );
            if( existIdx < 0 )
            {
                // No registration exists for this service OR some exist with another (not null) owner.
                // - If the new owner is the null "absorbing" one, we clear all the previous bindings and add the definitive self owned registration.
                // - Else, we append this new registration. At the end, if more than one binding exist, this will be an error.
                if( owner == null ) _singletons.RemoveAll( e => e.Definition == definition );
                _singletons.Add( (definition,owner) );
            }
            return true;
        }
    }

}
