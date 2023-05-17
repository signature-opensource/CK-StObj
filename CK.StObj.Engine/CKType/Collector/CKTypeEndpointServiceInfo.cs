using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures <see cref="EndpointAvailableServiceAttribute"/>, <see cref="EndpointSingletonServiceOwnerAttribute"/>,
    /// <see cref="EndpointAvailableServiceTypeAttribute"/> and <see cref="EndpointSingletonServiceTypeOwnerAttribute"/>
    /// attributes declaration.
    /// </summary>
    public sealed class CKTypeEndpointServiceInfo
    {
        public enum EntryKind
        {
            Scoped,

        }

        /// <summary>
        /// Captures a configuration entry.
        /// </summary>
        /// <param name="EndPoint">The endpoint in which this <see cref="ServiceType"/> is available.</param>
        /// <param name="singletonExclusive">
        /// <list type="bullet">
        /// <item>
        /// Null if <see cref="ServiceType"/> is scoped. All entries have necessarily also a null <paramref name="singletonExclusive"/>.
        /// </item>
        /// <item>
        /// False if <see cref="ServiceType"/> is singleton and can be exposed by other <paramref name="EndPoint"/>.
        /// 
        /// </item>
        /// </list>
        /// Null if <see cref="ServiceType"/> is scoped in the <paramref name="EndPoint"/>,
        /// </param>
        public readonly record struct Entry( Type EndPoint, bool? singletonExclusive );

        readonly Type _serviceType;
        readonly List<Entry> _entries;

        readonly List<Type> _endpoints;
        Type? _owner;
        bool _exclusive;
        string? _lockedReason;
        CKTypeKind _kind;

        /// <summary>
        /// Gets the type that is a endpoint service.
        /// </summary>
        public Type ServiceType => _serviceType;

        /// <summary>
        /// Gets the <see cref="EndpointDefinition"/> owner if this service is a endpoint singleton.
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

        /// <summary>
        /// Gets the type kind of <see cref="ServiceType"/>.
        /// </summary>
        public CKTypeKind Kind => _kind; 

        internal CKTypeEndpointServiceInfo( Type serviceType, Type? owner, bool exclusive, List<Type> endpoints )
        {
            Debug.Assert( owner == null || endpoints.Contains( owner ) );
            Debug.Assert( !exclusive || owner != null, "exclusive => owner != null" );
            _serviceType = serviceType;
            _owner = owner;
            _exclusive = exclusive;
            _endpoints = endpoints;
        }

        /// <summary>
        /// Orphan constructor.
        /// </summary>
        /// <param name="t">The service type.</param>
        internal CKTypeEndpointServiceInfo( Type t )
        {
            _serviceType = t;
            _endpoints = new List<Type>();
        }

        internal void SetTypeProcessed( CKTypeKind processedKind )
        {
            Debug.Assert( _kind == CKTypeKind.None );
            Debug.Assert( (processedKind & CKTypeKind.IsEndpointService) != 0 );
            _kind = processedKind;
        }

        internal bool HasBeenProcessed => _kind != 0;

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
