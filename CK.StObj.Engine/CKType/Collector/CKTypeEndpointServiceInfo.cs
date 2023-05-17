using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        readonly List<(Type Definition, Type? Owner, bool Exclusive)>? _singletons;
        // Final processed kind (may have CKTypeKind.HasError).
        CKTypeKind _kind;

        /// <summary>
        /// Gets the type that is a endpoint service.
        /// </summary>
        public Type ServiceType => _serviceType;

        /// <summary>
        /// Gets whether this is a scoped service.
        /// </summary>
        public bool IsScoped => _scoped != null;

        /// <summary>
        /// Gets whether this is a scoped service.
        /// </summary>
        public bool IsSingleton => _scoped == null;

        /// <summary>
        /// Gets the endpoint types that expose this service as a scoped service (if <see cref="IsScoped"/> is true).
        /// </summary>
        public IReadOnlyList<Type>? Scoped => _scoped;

        /// <summary>
        /// Gets the endpoint types that expose this service as a singleton if this service is singleton.
        /// </summary>
        public IReadOnlyList<(Type Definition, Type? Owner, bool Exclusive)>? Singletons => _singletons;

        /// <summary>
        /// Gets the type kind of <see cref="ServiceType"/>.
        /// </summary>
        public CKTypeKind Kind => _kind;

        // New singleton.
        internal CKTypeEndpointServiceInfo( Type serviceType, Type definition, Type? owner, bool exclusive )
        {
            Debug.Assert( !exclusive || owner != null, "exclusive => owner != null" );
            _serviceType = serviceType;
            _singletons = new List<(Type Definition, Type? Owner, bool Exclusive)>() { (definition, owner, exclusive) };
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
            else _singletons = new List<(Type Definition, Type? Owner, bool Exclusive)>();
        }

        static ReadOnlySpan<char> DefinitionName( Type definition ) => definition.Name.AsSpan( 0, definition.Name.Length - 18 );


        internal void SetTypeProcessed( CKTypeKind processedKind )
        {
            Debug.Assert( _kind == CKTypeKind.None );
            Debug.Assert( (processedKind & CKTypeKind.IsEndpointService) != 0 );
            _kind = processedKind;
        }

        internal bool HasBeenProcessed => _kind != 0;

        internal bool CombineWith( IActivityMonitor monitor, Type definition )
        {
            if( _scoped == null )
            {
                monitor.Error( $"Endpoint service '{_serviceType}' in has been declared as a singleton, it cannot be scoped for '{DefinitionName(definition)}'." );
                return false;
            }
            if( !_scoped.Contains( definition ) ) _scoped.Add( definition );
            return true;
        }

        internal bool CombineWith( IActivityMonitor monitor, Type definition, Type? owner, bool exclusive )
        {
            Debug.Assert( !exclusive || owner == null, "Exclusive => definition owns the service." );
            if( _singletons == null )
            {
                monitor.Error( $"Endpoint service '{_serviceType}' has been declared as a scoped, it cannot be singleton for '{DefinitionName( definition )}'." );
                return false;
            }
            // "Convergent configuration" implementation here.
            // We want the final configuration to converge to the same result regardless of any difference in registration order.
            // It means that we cannot take any decision based on this configuration state until the final result is built.
            // A possible implementation is to capture all configurations and wait for the final resolution.
            // 
            // to be totally order independent 
            // The mapping to a definition must be unique.
            // If the definition is already registered, let's allow transitions that can be handled 
            // - A previously borrowed registration (from another endpoint) can be replaced by a null: the
            //   new registration states that the service is owned by the endpoint instead of being "stolen".
            //   This is acceptable.
            // - When owner is null, exclusive ownership can be strengthened: a previously exclusive: false (that is the default)
            //   can be set to true.
            int idx = _singletons.IndexOf( e => e.Definition == definition );
            if( idx >= 0 )
            {
                var asArray = CollectionsMarshal.AsSpan( _singletons );
                var exists = asArray[ idx ];
                if( exists.Owner != owner )
                {
                    // Only allow null new owner if it differs.
                    if( owner != null )
                    {
                        var ownedBy = exists.Owner != null ? $"'{DefinitionName( exists.Owner )}'" : "the definition itself";
                        monitor.Error( $"Endpoint service '{_serviceType}' for '{DefinitionName( exists.Definition )}' is owned by {ownedBy}."
                                       + $" It cannot also be owned by '{DefinitionName(owner)}'." );
                        return false;
                    }
                    // Unconditionally accepts the exclusive that applies to the new owner.
                    exists.Exclusive = exclusive;
                    exists.Owner = owner;
                }
                else
                {
                    // Same owner. We cannot accept exclusive from true to false since we may have rejected bound registrations before.
                    // We may accept the false to true, providing that we check all existing registrations.
                    // BUT since we can change the owner, this check breaks convergence (behavior will differ depending on the registration ordering)
                    // and we cannot accept that: we are stuck here: we cannot allow a change of ownership.
                    var ownedBy = exists.Owner != null ? $"'{DefinitionName( exists.Owner )}'" : "the definition itself";
                    monitor.Error( $"Endpoint service '{_serviceType}' for '{DefinitionName( exists.Definition )}' owned by {ownedBy} is exclusive,"
                                    + $" it cannot be set to non exclusive." );
                    return false;
                }

            }


                // If a owner is specified, and this owner has already been registered, we check that it allows sharing.
                if( owner == null )
            {
                if( _singletons.Any( e => e.Definition == owner && e.Owner == null && e.Exclusive ) )
                {

                }
            }
            _singletons.Add( (definition, owner, exclusive) );
        }

        internal bool CombineWith( IActivityMonitor monitor, Type? owner, bool exclusive, IReadOnlyList<Type> endpoints )
        {
            Debug.Assert( owner == null || endpoints.Contains( owner ) );
            Debug.Assert( !exclusive || owner != null, "exclusive => owner != null" );
            if( owner != null )
            {
                if( _owner != null )
                {
                    if( _owner != owner )
                    {
                        monitor.Error( $"Singleton Endpoint service '{_serviceType}' is already owned by '{_owner.Name}', it cannot also be owned by {owner.Name}." );
                        return false;
                    }
                    // Exclusive can only transition from false to true.
                    if( Exclusive != exclusive )
                    {
                        _exclusive = true;
                        if( exclusive )
                        {
                            if( HasBeenProcessed )
                            {
                                return ErrorTypeProcessed( monitor );
                            }
                            monitor.Warn( $"Singleton Endpoint service '{_serviceType}' owned by '{_owner.Name}' is now Exclusive." );
                        }
                        else
                        {
                            monitor.Warn( $"Singleton Endpoint service '{_serviceType}' is already exclusively owned by '{_owner.Name}'. The false exclusiveEndpoint is ignored." );
                        }
                    }
                }
                else
                {
                    if( HasBeenProcessed )
                    {
                        return ErrorTypeProcessed( monitor );
                    }
                    _owner = owner;
                    _exclusive = exclusive;
                }
            }
            foreach( Type type in endpoints )
            {
                if( !AddAvailableEndpointDefinition( monitor, type ) ) return false;
            }
            return true;
        }

        bool ErrorTypeProcessed( IActivityMonitor monitor )
        {
            monitor.Error( $"Type '{_serviceType}' has been processed. Singleton ownership cannot be altered." );
            return false;
        }

        internal bool AddAvailableEndpointDefinition( IActivityMonitor monitor, Type newEndpointDefinition )
        {
            if( !_endpoints.Contains( newEndpointDefinition ) )
            {
                if( _lockedReason != null )
                {
                    monitor.Error( $"Endpoint definition failed because: '{_lockedReason}': Extending Endpoint service '{_serviceType}' availability to '{newEndpointDefinition.Name}' endpoint is not possible." );
                    return false;
                }
                _endpoints.Add( newEndpointDefinition );
            }
            return true;
        }

        internal bool CombineWith( IActivityMonitor monitor, CKTypeEndpointServiceInfo baseInfo )
        {
            return CombineWith( monitor, baseInfo._owner, baseInfo._exclusive, baseInfo._endpoints );
        }

        internal void SetDefaultSingletonOwner()
        {
            Debug.Assert( _owner == null && !_exclusive && !HasBeenProcessed && _lockedReason == null );
            _owner = typeof( DefaultEndpointDefinition );
            if( !_endpoints.Contains( _owner ) ) _endpoints.Contains( _owner );
        }
    }

}
