using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{
    /// <summary>
    /// Helper scoped services that captures all the ubiquitous information services from a current
    /// service provider so they can be overridden and marshalled to other <see cref="IEndpointType{TScopeData}"/>
    /// containers through the <see cref="EndpointDefinition.ScopedData"/>.
    /// </summary>
    public sealed class EndpointUbiquitousInfo : IScopedAutoService
    {
        readonly Dictionary<Type,object> _registrations;
        readonly bool _initial;

        /// <summary>
        /// Initializes a new <see cref="EndpointUbiquitousInfo"/> from a current context.
        /// </summary>
        /// <param name="services">The current service provider (must be a scoped container).</param>
        public EndpointUbiquitousInfo( IServiceProvider services )
        {
            _registrations = (Dictionary<Type, object>)services.GetRequiredService<EndpointTypeManager>().GetInitialEndpointUbiquitousInfo( services );
            _initial = true;
        }

        EndpointUbiquitousInfo( Dictionary<Type, object> r ) => _registrations = r;

        /// <summary>
        /// Gets whether this ubiquitous information has been overridden or contains the initial objects from the initial services.
        /// </summary>
        public bool IsCopy => !_initial;

        /// <summary>
        /// Overrides a ubiquitous resolution with an explicit instance.
        /// </summary>
        /// <typeparam name="T">The instance type. Must be a endpoint ubiquitous type.</typeparam>
        /// <param name="instance">The instance that must replace the default instance from the originating container.</param>
        /// <returns>This info if <see cref="IsCopy"/> is already true, or a copy of the initial configuration.</returns>
        public EndpointUbiquitousInfo Override<T>( T instance ) where T : notnull => DoOverride( typeof(T), instance );

        public EndpointUbiquitousInfo Override<TScopedData, T>( Func<TScopedData, T> factory ) where T : class => DoOverride( typeof( T ), factory );

        public EndpointUbiquitousInfo Override<T>( Func<IServiceProvider, T> factory ) where T : class => DoOverride( typeof( T ), Tuple.Create( factory ) );

        EndpointUbiquitousInfo DoOverride( Type t, object o )
        {
            Throw.CheckNotNullArgument( o, nameof( o ) );
            if( _initial )
            {
                var d = new Dictionary<Type, object>( _registrations );
                d[t] = o;
                return new EndpointUbiquitousInfo( d );
            }
            _registrations[t] = o;
            return this;
        }

        /// <summary>
        /// Infrastructure artifact not intended to be called directly.
        /// </summary>
        /// <param name="t">Must be the type of a endpoint ubiquitous information service.</param>
        /// <returns>An opaque object.</returns>
        public object GetMapping( Type t ) => _registrations[t];

    }

}
