using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{
    /// <summary>
    /// Helper scoped service that captures all the ubiquitous information services from a current
    /// service provider so they can be overridden and marshalled to other <see cref="IDIContainer{TScopeData}"/>
    /// containers through their <see cref="DIContainerDefinition.IScopedData"/>.
    /// </summary>
    [Setup.ContextBoundDelegation( "CK.Setup.AmbientServiceHubImpl, CK.StObj.Engine" )]
    public abstract class AmbientServiceHub : IScopedAutoService
    {
        /// <summary>
        /// Used by generated code.
        /// </summary>
        protected struct Mapper
        {
            /// <summary>
            /// Initial service instance.
            /// </summary>
            public readonly object Initial;

            /// <summary>
            /// Current service instance.
            /// </summary>
            public object Current;

            /// <summary>
            /// Gets whether current has changed.
            /// </summary>
            public readonly bool IsDirty => Initial != Current;

            /// <summary>
            /// Initializes a new mapper.
            /// </summary>
            /// <param name="initial">The initial and current service.</param>
            public Mapper( object initial )
            {
                Initial = initial;
                Current = initial;
            }
        }

        /// <summary>
        /// Used by generated code.
        /// </summary>
        protected readonly Mapper[] _mappers;
        readonly ImmutableArray<DIContainerHub.AmbientServiceMapping> _entries;
        bool _locked;

        /// <summary>
        /// Initializes a new <see cref="AmbientServiceHub"/> from a current context.
        /// <para>
        /// This is used in endpoint containers to capture the decisions of the endpoint services configuration
        /// and <see cref="IsLocked"/> is true.
        /// </para>
        /// <para>
        /// Background containers have an injected hub (also locked by <see cref="IDIContainerServiceProvider{TScopeData}.CreateScope(TScopeData)"/>)
        /// and ambient services are resolved from the hub.
        /// </para>
        /// </summary>
        /// <param name="services">The current service provider (must be a scoped container).</param>
        public AmbientServiceHub( IServiceProvider services )
        {
            _mappers = Initialize( services, out _entries );
            _locked = true;
        }

        /// <summary>
        /// Called by generated code.
        /// </summary>
        /// <param name="mappers">Ready to use clean mappers.</param>
        /// <param name="entries">The ubiquitous services mapping.</param>
        protected AmbientServiceHub( Mapper[] mappers, ImmutableArray<DIContainerHub.AmbientServiceMapping> entries )
        {
            _mappers = mappers;
            _entries = entries;
        }

        /// <summary>
        /// Gets the value retrieved from the originating DI container for a type (that must be an Ambient service type).
        /// </summary>
        /// <param name="t">The ambient service type.</param>
        /// <returns>The initial value.</returns>
        public object GetInitialValue( Type t ) => Get( t ).Initial;

        /// <summary>
        /// Gets the value retrieved from the originating DI container for a type (that must be an Ambient service type).
        /// </summary>
        /// <typeparam name="T">The ambient service type.</typeparam>
        /// <returns>The initial value.</returns>
        public T GetInitialValue<T>() => (T)Get( typeof(T) ).Initial;

        /// <summary>
        /// Gets the current value for a type (that must be an Ambient service type).
        /// </summary>
        /// <param name="t">The Ambient service type.</param>
        /// <returns>The current value.</returns>
        public object GetCurrentValue( Type t ) => Get( t ).Current;

        /// <summary>
        /// Gets the current value for a type (that must be an Ambient service type).
        /// </summary>
        /// <typeparam name="T">The ambient service type.</typeparam>
        /// <returns>The current value.</returns>
        public T GetCurrentValue<T>() => (T)Get( typeof(T) ).Current;

        /// <summary>
        /// Gets whether at least one value is overridden.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                for( int i = 0; i < _mappers.Length; ++i )
                {
                    if( _mappers[i].IsDirty ) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Locks these informations: this is called by <see cref="IDIContainerServiceProvider{TScopeData}.CreateScope(TScopeData)"/>
        /// so that instances cannot be changed anymore (Override throws).
        /// If this must be reused for another container, use <see cref="CleanClone()"/>.
        /// </summary>
        public void Lock() => _locked = true;

        /// <summary>
        /// Gets whether <see cref="Lock"/> has been called.
        /// </summary>
        public bool IsLocked => _locked;

        /// <summary>
        /// Gets an unlocked clone with no overridden values.
        /// </summary>
        public abstract AmbientServiceHub CleanClone();

        /// <summary>
        /// Overrides a ubiquitous resolution with an explicit instance.
        /// <para>
        /// This throws a <see cref="InvalidOperationException"/> if <see cref="IsLocked"/> is true.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The instance type. Must be a endpoint ubiquitous type.</typeparam>
        /// <param name="instance">The instance that must replace the default instance from the originating container.</param>
        public void Override<T>( T instance ) where T : class
        {
            Throw.CheckNotNullArgument( instance );
            DoOverride( typeof( T ), instance );
        }

        /// <summary>
        /// Overrides a ubiquitous resolution with an explicit instance.
        /// <para>
        /// This throws a <see cref="InvalidOperationException"/> if <see cref="IsLocked"/> is true.
        /// </para>
        /// </summary>
        /// <param name="type">The instance type that must be a endpoint ubiquitous type.</param>
        /// <param name="instance">The instance that must replace the default instance from the originating container.</param>
        public void Override( Type type, object instance )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckNotNullArgument( instance );
            Throw.CheckArgument( type.IsAssignableFrom( instance.GetType() ) );
            DoOverride( type, instance );
        }

        /// <summary>
        /// Code generated.
        /// </summary>
        /// <param name="services">The current service provider (must be a scoped container).</param>
        /// <param name="entries">The ambient services mapping.</param>
        /// <returns>The initial mapped values.</returns>
        protected abstract Mapper[] Initialize( IServiceProvider services, out ImmutableArray<DIContainerHub.AmbientServiceMapping> entries );

        ref Mapper Get( Type t )
        {
            return ref _mappers[_entries[GetTypeIndex( t )].MappingIndex];
        }

        int GetTypeIndex( Type t )
        {
            for( int i = 0; i < _entries.Length; ++i )
            {
                if( _entries[i].AmbientServiceType == t ) return i;
            }
            return Throw.ArgumentException<int>( $"Type '{t.ToCSharpName()}' must be a Ambient service." );
        }

        void DoOverride( Type type, object instance )
        {
            Throw.CheckState( !IsLocked );
            int i = GetTypeIndex( type );
            var tInstance = instance.GetType();
            if( tInstance != type )
            {
                CheckSpecialization( i, tInstance );
            }
            _mappers[_entries[i].MappingIndex].Current = instance;

            // This concerns only IAutoService.
            // Regular (non IAutoService) have no mappings.
            void CheckSpecialization( int i, Type tInstance )
            {
                int iIndex = _entries[i].MappingIndex;
                int iImpl = i;
                int iNextImpl = i + 1;
                while( iNextImpl < _entries.Length && _entries[iNextImpl].MappingIndex == iIndex )
                {
                    iImpl = iNextImpl;
                    iNextImpl++;
                }
                // Lyskov here: the runtime type is allowed to be more specialized
                // than the ones we know.
                if( iImpl != i )
                {
                    if( !_entries[iImpl].AmbientServiceType.IsAssignableFrom( tInstance ) )
                    {
                        Throw.ArgumentException( $"Instance must be a specialization of '{_entries[iImpl].AmbientServiceType.ToCSharpName()}' (its type is '{tInstance.ToCSharpName()}')." );
                    }
                }
            }
        }

    }

}
