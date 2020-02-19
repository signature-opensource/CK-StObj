using CK.Core;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Adds extension methods on <see cref="IServiceCollection"/>.
    /// </summary>
    public static class StObjServiceCollectionExtensions
    {
        /// <summary>
        /// Calls <see cref="AddStObjMap(IServiceCollection, IActivityMonitor, IStObjMap, SimpleServiceContainer)"/> after
        /// a <see cref="StObjContextRoot.Load(Assembly, IStObjRuntimeBuilder, IActivityMonitor)"/> of the map.
        /// <para>
        /// Assembly load conflicts may occur here. In such case, you should use the CK.WeakAssemblyNameResolver package
        /// and wrap the call this way:
        /// <code>
        /// using( CK.Core.WeakAssemblyNameResolver.TemporaryInstall() )
        /// {
        ///     services.AddStObjMap( stobjAssembly );
        /// }
        /// </code>
        /// Note that there SHOULD NOT be any conflicts. This workaround may be necessary but hides a conflict of version dependencies
        /// that may cause runtime errors.
        /// </para>
        /// <para>
        /// If the registration fails for any reason (file not found, type conflicts, etc.), an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="services">This services.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="stobjAssembly">The assembly.</param>
        /// <param name="startupServices">
        /// Optional simple container that may provide startup services. This is not used to build IRealObject
        /// (they must be independent of any "dynamic" services), however registered services become available to
        /// any <see cref="StObjContextRoot.ConfigureServicesMethodName"/> methods by parameter injection.
        /// </param>
        /// <returns>This services collection.</returns>
        public static IServiceCollection AddStObjMap( this IServiceCollection services, IActivityMonitor monitor, Assembly stobjAssembly, SimpleServiceContainer startupServices = null )
        {
            if( stobjAssembly == null ) throw new ArgumentNullException( nameof( stobjAssembly ) );
            var map = StObjContextRoot.Load( stobjAssembly );
            if( map == null )
                throw new ArgumentException( $"The assembly {stobjAssembly.FullName} was not found or is not a valid StObj map assembly." );
            return AddStObjMap( services, monitor, map, startupServices );
        }

        /// <summary>
        /// Calls <see cref="AddStObjMap(IServiceCollection, IActivityMonitor, IStObjMap, SimpleServiceContainer)"/> after
        /// having loaded the assembly and <see cref="StObjContextRoot.Load(Assembly, IStObjRuntimeBuilder, IActivityMonitor)"/> the
        /// map.
        /// <para>
        /// Assembly load conflicts may occur here. In such case, you should use the CK.WeakAssemblyNameResolver package
        /// and wrap the call this way:
        /// <code>
        /// using( CK.Core.WeakAssemblyNameResolver.TemporaryInstall() )
        /// {
        ///     services.AddStObjMap( "CK.StObj.AutoAssembly" );
        /// }
        /// </code>
        /// Note that there SHOULD NOT be any conflicts. This workaround may be necessary but hides a conflict of version dependencies
        /// that may cause runtime errors.
        /// </para>
        /// <para>
        /// If the registration fails for any reason (file not found, type conflicts, etc.), an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="services">This services.</param>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="assemblyName">The assembly name (without .ddl suffix).</param>
        /// <param name="startupServices">
        /// Optional simple container that may provide startup services. This is not used to build IRealObject
        /// (they must be independent of any "dynamic" services), however registered services become available to
        /// any <see cref="StObjContextRoot.ConfigureServicesMethodName"/> methods by parameter injection.
        /// </param>
        /// <returns>This services collection.</returns>
        /// <remarks>
        /// On NetCore runtime, Assembly.LoadFrom is used to resolves the assembly from its full path.
        /// </remarks>
        public static IServiceCollection AddStObjMap( this IServiceCollection services, IActivityMonitor monitor, string assemblyName, SimpleServiceContainer startupServices = null )
        {
#if NET461
            var a = Assembly.Load( new AssemblyName( assemblyName ) );
#else
            var a = Assembly.LoadFrom( System.IO.Path.Combine( AppContext.BaseDirectory, assemblyName + ".dll" ) );
#endif
            return AddStObjMap( services, monitor, a, startupServices );
        }

        /// <summary>
        /// Configures this <see cref="IServiceCollection"/> by registering the <see cref="IStObjMap.StObjs"/> and
        /// the <paramref name="map"/> itself as Singletons, by calling <see cref="StObjContextRoot.RegisterStartupServicesMethodName"/>
        /// and then <see cref="StObjContextRoot.ConfigureServicesMethodName"/> on all the <see cref="IStObjObjectMap.Implementations"/> that expose
        /// such methods and by registering the <see cref="IStObjServiceMap.SimpleMappings"/> and <see cref="IStObjServiceMap.ManualMappings"/> mappings.
        /// Any attempt to register an already registered service will be ignored and a warning will be emitted.
        /// <para>
        /// If the registration fails for any reason (file not found, type conflicts, etc.), an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="services">This service collection to configure.</param>
        /// <param name="monitor">The monitor to use. Must not be null.</param>
        /// <param name="map">StObj map to register. Can not be null.</param>
        /// <param name="startupServices">
        /// Optional simple container that may provide startup services. This is not used to build IRealObject
        /// (they must be independent of any "dynamic" services), however registered services become available to
        /// any <see cref="StObjContextRoot.ConfigureServicesMethodName"/> methods by parameter injection.
        /// </param>
        /// <returns>This services collection.</returns>
        public static IServiceCollection AddStObjMap( this IServiceCollection services, IActivityMonitor monitor, IStObjMap map, SimpleServiceContainer startupServices = null )
        {
            var reg = new StObjContextRoot.ServiceRegister( monitor, services, startupServices );
            if( !reg.AddStObjMap( map ) ) throw new Exception( "AddStObjMap failed. The logs contains detailed information." );
            return services;
        }

    }
}
