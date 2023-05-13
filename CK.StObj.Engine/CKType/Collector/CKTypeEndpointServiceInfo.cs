using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures <see cref="EndpointServiceAvailabilityAttribute"/>, <see cref="EndpointSingletonServiceOwnerAttribute"/>,
    /// <see cref="EndpointServiceTypeAvailabilityAttribute"/> and <see cref="EndpointSingletonServiceTypeOwnerAttribute"/>
    /// attributes declaration.
    /// </summary>
    sealed class CKTypeEndpointServiceInfo
    {
        readonly Type _serviceType;
        readonly List<Type> _endpoints;
        Type? _owner;
        bool _exclusive;
        string? _lockedReason;
        CKTypeKind _processedkind;

        internal enum ExtendStatus
        {
            Full,
            PromoteToExclusive,
        }

        /// <summary>
        /// Gets the type that is a endpoint service.
        /// </summary>
        public Type ServiceType => _serviceType;

        /// <summary>
        /// Gets the <see cref="EndpointType"/> owner if this service is a endpoint singleton.
        /// </summary>
        public Type? Owner => _owner;

        /// <summary>
        /// Gets whether this service is a endpoint singleton that is exclusively owned by <see cref="Owner"/>.
        /// </summary>
        public bool Exclusive => _exclusive;

        /// <summary>
        /// Gets the set of endpoint type that expose this service.
        /// </summary>
        public IReadOnlyList<Type> Endpoints => _endpoints;

        internal CKTypeEndpointServiceInfo( Type serviceType, Type? owner, bool exclusive, List<Type> endpoints )
        {
            Debug.Assert( owner == null || endpoints.Contains( owner ) );
            Debug.Assert( !exclusive || owner != null, "exclusive => owner != null" );
            _serviceType = serviceType;
            _owner = owner;
            _exclusive = exclusive;
            _endpoints = endpoints;
        }

        internal CKTypeEndpointServiceInfo( Type serviceType, CKTypeEndpointServiceInfo baseInfo )
        {
            _serviceType = serviceType;
            _owner = baseInfo._owner;
            _exclusive = baseInfo._exclusive;
            _endpoints = new List<Type>( baseInfo._endpoints );
        }

        internal void SetTypeProcessed( CKTypeKind processedKind )
        {
            Debug.Assert( _processedkind == CKTypeKind.None );
            Debug.Assert( (processedKind & CKTypeKind.EndpointProcessServiceMask) == CKTypeKind.EndpointProcessServiceMask );
            _processedkind = processedKind;
        }

        internal bool HasBeenProcessed => _processedkind != 0;

        internal bool IsLocked => _lockedReason != null;

        internal void Lock( string reason ) => _lockedReason ??= reason;

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
                if( !AddAvailableEndpointType( monitor, type ) )  return false;
            }
            return true;
        }

        bool ErrorTypeProcessed( IActivityMonitor monitor )
        {
            monitor.Error( $"Type '{_serviceType}' has been processed. Singleton ownership cannot be altered." );
            return false;
        }

        internal bool AddAvailableEndpointType( IActivityMonitor monitor, Type newEndpointType )
        {
            if( !_endpoints.Contains( newEndpointType ) )
            {
                if( _lockedReason != null )
                {
                    monitor.Error( $"Endpoint definition failed because: '{_lockedReason}': Extending Endpoint service '{_serviceType}' availability to '{newEndpointType.Name}' endpoint is not possible." );
                    return false;
                }
                _endpoints.Add( newEndpointType );
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
            _owner = typeof( DefaultEndpointType );
            if( !_endpoints.Contains( _owner ) ) _endpoints.Contains( _owner );
        }
    }

}
