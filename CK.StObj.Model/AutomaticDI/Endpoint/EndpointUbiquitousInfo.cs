using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{
    /// <summary>
    /// Helper scoped service that captures all the ubiquitous information services from a current
    /// service provider so they can be overridden and marshalled to other <see cref="IEndpointType{TScopeData}"/>
    /// containers through the <see cref="EndpointDefinition.ScopedData"/>.
    /// </summary>
    [Setup.ContextBoundDelegation( "CK.Setup.EndpointUbiquitousInfoImpl, CK.StObj.Engine" )]
    public abstract class EndpointUbiquitousInfo : IScopedAutoService
    {
        protected readonly struct Entry
        {
            public readonly Type Type;
            public readonly int Index;

            public Entry( Type type, int index ) : this()
            {
                Type = type;
                Index = index;
            }
        }
        protected struct Mapper
        {
            public readonly object Initial;
            public object Current;
            public readonly bool IsDirty => Initial != Current;

            public Mapper( object initial )
            {
                Initial = initial;
                Current = initial;
            }
        }
        [AllowNull]
        protected static Entry[] _entries;
        protected readonly Mapper[] _mappers;
        bool _locked;

        /// <summary>
        /// Initializes a new <see cref="EndpointUbiquitousInfo"/> from a current context.
        /// </summary>
        /// <param name="services">The current service provider (must be a scoped container).</param>
        public EndpointUbiquitousInfo( IServiceProvider services )
        {
            _mappers = Initialize( services );
        }

        /// <summary>
        /// Called by generated code.
        /// </summary>
        /// <param name="mappers">Ready to use clean mappers.</param>
        protected EndpointUbiquitousInfo( Mapper[] mappers )
        {
            _mappers = mappers;
        }

        /// <summary>
        /// Code generated.
        /// </summary>
        /// <param name="services">The current service provider (must be a scoped container).</param>
        /// <returns>The initial mapped values.</returns>
        protected abstract Mapper[] Initialize( IServiceProvider services );

        /// <summary>
        /// Gets the value retrieved from the originating DI container for a type (that must be a ubiquitous service type).
        /// </summary>
        /// <param name="t">The ubiquitous service type.</param>
        /// <returns>The initial value.</returns>
        public object GetInitialValue( Type t ) => Get( t ).Initial;

        /// <summary>
        /// Gets the current value for a type (that must be a ubiquitous service type).
        /// </summary>
        /// <param name="t">The ubiquitous service type.</param>
        /// <returns>The initial value.</returns>
        public object GetCurrentValue( Type t ) => Get( t ).Current;

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
        /// Locks these informations: this is called by <see cref="IEndpointServiceProvider{TScopeData}.CreateScope(TScopeData)"/>
        /// so that instances cannot be changed anymore (Override throws).
        /// If this must be reused for another endpoint, use <see cref="CleanClone()"/>.
        /// </summary>
        public void Lock() => _locked = true;

        /// <summary>
        /// Gets whether <see cref="Lock"/> has been called.
        /// </summary>
        public bool IsLocked => _locked;

        /// <summary>
        /// Gets an unlocked clone with no overridden values.
        /// </summary>
        public abstract EndpointUbiquitousInfo CleanClone();

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

        ref Mapper Get( Type t )
        {
            return ref _mappers[_entries[GetTypeIndex( t )].Index];
        }

        static int GetTypeIndex( Type t )
        {
            for( int i = 0; i < _entries.Length; ++i )
            {
                if( _entries[i].Type == t ) return i;
            }
            return Throw.ArgumentException<int>( $"Type '{t.ToCSharpName()}' must be a Ubiquitous service." );
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
            _mappers[_entries[i].Index].Current = instance;

            // This concerns only IAutoService.
            // Regular (non IAutoService) have no mappings.
            static void CheckSpecialization( int i, Type tInstance )
            {
                int iIndex = _entries[i].Index;
                int iImpl = i;
                int iNextImpl = i + 1;
                while( iNextImpl < _entries.Length && _entries[iNextImpl].Index == iIndex )
                {
                    iImpl = iNextImpl;
                    iNextImpl++;
                }
                // Lyskov here: the runtime type is allowed to be more specialized
                // than the ones we know.
                if( iImpl != i )
                {
                    if( !_entries[iImpl].Type.IsAssignableFrom( tInstance ) )
                    {
                        Throw.ArgumentException( $"Instance must be a specialization of '{_entries[iImpl].Type.ToCSharpName()}' (its type is '{tInstance.ToCSharpName()}')." );
                    }
                }
            }
        }

    }

}
