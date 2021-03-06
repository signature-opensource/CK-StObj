using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Testing.StObjMap
{
    /// <summary>
    /// Gives access to one or more StObjMaps by loading them from existing generated assemblies.
    /// </summary>
    public interface IStObjMapTestHelperCore
    {
        /// <summary>
        /// Gets the generated assembly name from "StObjMap/GeneratedAssemblyName" configuration.
        /// Defaults to <see cref="Setup.StObjEngineConfiguration.DefaultGeneratedAssemblyName"/>.
        /// <para>
        /// This is updated each time <see cref="ResetStObjMap"/> is called with a ".Reset.#num" suffix
        /// (where #num is an incremented number starting at 1).
        /// </para>
        /// <para>
        /// The original name must not contain a ".Reset." part in its name otherwise an <see cref="ArgumentException"/> is thrown.
        /// </para>
        /// </summary>
        string GeneratedAssemblyName { get; }

        /// <summary>
        /// Highest entry point possible: this gives a ready to use shared service provider based on the current <see cref="StObjMap"/>.
        /// Note that this AutomaticServices will resolve (and keep) singletons AND scoped services. To obtain a "fresh" set of services,
        /// use <see cref="CreateAutomaticServices"/>.
        /// The returned services can be configured thanks to the <see cref="AutomaticServicesConfiguring"/>
        /// and <see cref="AutomaticServicesConfigured"/> events.
        /// This throws if any error prevents the services to be correctly configured.
        /// </summary>
        IServiceProvider AutomaticServices { get; }

        /// <summary>
        /// Creates and configures a pristine service provider based on the current <see cref="StObjMap"/>.
        /// The returned services can be automatically configured thanks to the <see cref="AutomaticServicesConfiguring"/>
        /// and <see cref="AutomaticServicesConfigured"/> events.
        /// This throws if any error prevents the services to be correctly configured.
        /// <para>
        /// Note that the <see cref="ServiceProvider"/> is a <see cref="IDisposable"/> object: it SHOULD be disposed once done with it.
        /// </para>
        /// </summary>
        /// <param name="startupServices">Optional startup services container.</param>
        /// <returns>A new service provider.</returns>
        ServiceProvider CreateAutomaticServices( SimpleServiceContainer? startupServices = null );

        /// <summary>
        /// Fires before the future <see cref="AutomaticServices"/> or a new one created by <see cref="CreateAutomaticServices(SimpleServiceContainer?)"/>
        /// is configured by the <see cref="StObjMap"/>: this enables external code to configure/alter the startup services and
        /// the <see cref="StObjContextRoot.ServiceRegister.StartupServices"/> before the <see cref="StObjContextRoot.ServiceRegister.AddStObjMap(IStObjMap)"/> call.
        /// </summary>
        event EventHandler<AutomaticServicesConfigurationEventArgs> AutomaticServicesConfiguring;

        /// <summary>
        /// Fires after the future <see cref="AutomaticServices"/> or a new one created by <see cref="CreateAutomaticServices(SimpleServiceContainer?)"/>
        /// have been configured by the <see cref="StObjMap"/> but before making it available to others.
        /// External code can configure/alter the configured services.
        /// </summary>
        event EventHandler<AutomaticServicesConfigurationEventArgs> AutomaticServicesConfigured;

        /// <summary>
        /// Gets the <see cref="IStObjMap"/> from the current <see cref="GeneratedAssemblyName"/>.
        /// The assembly, if it exists, is the one in the <see cref="IBasicTestHelper.BinFolder"/>.
        /// This throws if any error prevents the map to be correctly loaded.
        /// </summary>
        IStObjMap StObjMap { get; }

        /// <summary>
        /// Gets whether a failed attempt to obtain the <see cref="StObjMap"/> should be ignored: subsequent attempt to
        /// get it will trigger a full resolution.
        /// By default, this is "StObjMap/StObjMapRetryOnError" configuration that is false: if the first attempt to obtain the
        /// current <see cref="StObjMap"/> failed, subsequent obtentions immediatey throw.
        /// <para>
        /// Note that calls to <see cref="ResetStObjMap(bool)"/> resets any current load error.
        /// </para>
        /// </summary>
        bool StObjMapRetryOnError { get; set; }

        /// <summary>
        /// Fires whenever the <see cref="StObjMap"/> is accessed.
        /// This allows external code to handle map lifetime: see <see cref="StObjMapAccessedEventArgs"/>.
        /// </summary>
        event EventHandler<StObjMapAccessedEventArgs> StObjMapAccessed;

        /// <summary>
        /// Resets the <see cref="StObjMap"/>: internally sets it to null and appends a ".Reset.#num" suffix
        /// (where #num is an incremented number starting at 1) to <see cref="GeneratedAssemblyName"/> so
        /// that a new dll must be generated by <see cref="StObjMapLoading"/>.
        /// </summary>
        /// <param name="deleteGeneratedBinFolderAssembly">
        /// By default, <see cref="DeleteGeneratedAssemblies(string)"/> is called on the <see cref="IBasicTestHelper.BinFolder"/>.
        /// </param>
        void ResetStObjMap( bool deleteGeneratedBinFolderAssembly = true );

        /// <summary>
        /// Fires the first time the <see cref="StObjMap"/> on current <see cref="GeneratedAssemblyName"/>
        /// must be loaded.
        /// Enables external code to actually generate the assembly if needed.
        /// </summary>
        event EventHandler StObjMapLoading;

        /// <summary>
        /// Deletes all files like <see cref="GeneratedAssemblyName"/> or GeneratedAssemblyName.Reset.#num
        /// in the specified directory.
        /// </summary>
        /// <param name="directory">The directory from which generated assembly(ies) must be deleted.</param>
        /// <returns>The number of deleted files.</returns>
        int DeleteGeneratedAssemblies( string directory );

        /// <summary>
        /// Loads a <see cref="IStObjMap"/> from existing generated assembly in the <see cref="IBasicTestHelper.BinFolder"/>.
        /// Actual loading of the assembly is done only if the StObjMap is not already available.
        /// </summary>
        /// <returns>The map or null if an error occurred (the error is logged).</returns>
        IStObjMap? LoadStObjMap( string assemblyName, bool withWeakAssemblyResolver = true );

    }
}
