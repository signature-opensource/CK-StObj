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
    public sealed partial class CKTypeEndpointServiceInfo
    {
        readonly Type _serviceType;
        readonly List<Type> _services;
        readonly bool _scoped;
        // Final processed kind (may have CKTypeKind.HasError).
        CKTypeKind _kind;

        /// <summary>
        /// Gets the type that is a endpoint service.
        /// </summary>
        public Type ServiceType => _serviceType;

        /// <summary>
        /// Gets whether this is a scoped service.
        /// </summary>
        public bool IsScoped => _scoped;

        /// <summary>
        /// Gets whether this is a singleton service.
        /// </summary>
        public bool IsSingleton => !_scoped;

        /// <summary>
        /// Gets the endpoint types that expose this service.
        /// </summary>
        public IReadOnlyList<Type> Services => _services;

        /// <summary>
        /// Gets the type kind of <see cref="ServiceType"/>.
        /// </summary>
        public CKTypeKind Kind => _kind;

        // New singleton or scoped.
        internal CKTypeEndpointServiceInfo( bool isScoped, Type serviceType, Type definition )
            : this( serviceType, isScoped )
        {
            _services.Add( definition );
        }

        // Orphan constructor.
        internal CKTypeEndpointServiceInfo( Type t, bool isScoped )
        {
            _serviceType = t;
            _services = new List<Type>();
            _scoped = isScoped;
        }

        internal void SetTypeProcessed( CKTypeKind processedKind )
        {
            Debug.Assert( _kind == CKTypeKind.None );
            Debug.Assert( (processedKind & CKTypeKind.IsEndpointService) != 0 );
            _kind = processedKind;
        }

        internal bool HasBeenProcessed => _kind != 0;

        internal bool CombineWith( IActivityMonitor monitor, bool isScoped, Type definition )
        {
            if( isScoped != _scoped )
            {
                monitor.Error( $"Endpoint service '{_serviceType}' has already been declared as a {(_scoped ? "scoped " : "singleton")}, the lifetime cannot be changed for endpoint '{DefinitionName( definition )}'." );
                return false;
            }
            if( !_services.Contains( definition ) ) _services.Add( definition );
            return true;
        }

        internal bool CombineScopedWith( IActivityMonitor monitor, Type definition ) => CombineWith( monitor, true, definition );

        internal bool CombineSingletonWith( IActivityMonitor monitor, Type definition ) => CombineWith( monitor, false, definition );
    }

}
