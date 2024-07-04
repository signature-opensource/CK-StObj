using CK.Core;
using CK.Monitoring;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    /// <summary>
    /// Implements <see cref="ICKomposableAppBuilder"/> and provides a single static Run that encapsulates
    /// the whole process (including Logs).
    /// <para>
    /// This helper should be used with the following conventions:
    /// <list type="number">
    ///     <item>The application code is in a project named "XXX.App".</item>
    ///     <item>
    ///     This helper is used from an executable project named "XXX.Builder" whose Program.cs can be as simple as:
    ///     <code>
    ///     return CK.Setup.CKomposableAppBuilder.Run();
    ///     </code>
    ///     </item>
    ///     <item>The application to setup is a project named "XXX.Host".</item>
    /// </list>
    /// If a <c>CKSetup.xml</c> exists in the "XXX.Builder" project, it automatically configures the <see cref="ICKomposableAppBuilder.EngineConfiguration"/>.
    /// </para>
    /// </summary>
    public sealed class CKomposableAppBuilder : ICKomposableAppBuilder
    {
        readonly NormalizedPath _parentFolderPath;
        readonly NormalizedPath _rootLogPath;
        readonly IActivityMonitor _monitor;
        readonly NormalizedPath _msBuildOutputPath;
        readonly NormalizedPath _builderFolderPath;
        readonly EngineConfiguration _engineConfiguration;
        string _applicationName;

        /// <inheritdoc />
        public string ApplicationName
        {
            get => _applicationName;
            set => _applicationName = value ?? String.Empty;
        }

        /// <inheritdoc />
        public IActivityMonitor Monitor => _monitor;

        /// <inheritdoc />
        public NormalizedPath MsBuildOutputPath => _msBuildOutputPath;

        /// <inheritdoc />
        public NormalizedPath ParentFolderPath => _parentFolderPath;

        /// <inheritdoc />
        public NormalizedPath BuilderFolderPath => _builderFolderPath;

        /// <inheritdoc />
        public NormalizedPath GetAppFolderPath( string? appName = null ) => _parentFolderPath.AppendPart( appName ?? ApplicationName + ".App" );

        /// <inheritdoc />
        public NormalizedPath GetHostFolderPath( string? appName = null ) => _parentFolderPath.AppendPart( appName ?? ApplicationName + ".Host" );

        /// <inheritdoc />
        public NormalizedPath GetAppBinPath( string? appName = null ) => GetAppFolderPath( appName ).Combine( _msBuildOutputPath );

        /// <inheritdoc />
        public EngineConfiguration EngineConfiguration => _engineConfiguration;

        /// <summary>
        /// Shell method that handles the run of a setup from a ".App" to its ".Host" project.
        /// </summary>
        /// <param name="configure">Optional code that can configure the <see cref="EngineConfiguration"/>.</param>
        /// <returns>The standard main result (0 on success, non zero on error).</returns>
        public static int Run( Action<IActivityMonitor, ICKomposableAppBuilder>? configure = null )
        {
            NormalizedPath appContext = AppContext.BaseDirectory;
            var builderFolder = appContext.RemoveLastPart( 2 );
            if( builderFolder.LastPart != "bin" )
            {
                Console.WriteLine( $"Invalid OutputPath '{appContext}'. It must follow the 'bin/Configuration/TargetFramework' convention." );
                return -1;
            }
            builderFolder = builderFolder.RemoveLastPart();
            var rootLogPath = builderFolder.AppendPart( "Logs" );
            LogFile.RootLogPath = rootLogPath;
            GrandOutput.EnsureActiveDefault();
            var monitor = new ActivityMonitor();
            try
            {
                var parentPath = builderFolder.RemoveLastPart();
                var msBuildOutputPath = appContext.RemovePrefix( builderFolder );
                var applicationName = builderFolder.LastPart;
                if( applicationName.EndsWith( ".Builder" ) ) applicationName = applicationName.Substring( 0, applicationName.Length - 8 );

                EngineConfiguration configuration;
                NormalizedPath ckSetupFile = builderFolder.AppendPart( "CKSetup.xml" );
                if( File.Exists( ckSetupFile ) )
                {
                    monitor.Info( "Loading EngineConfiguration from CKSetup.xml file." );
                    configuration = EngineConfiguration.Load( ckSetupFile );
                }
                else
                {
                    configuration = new EngineConfiguration() { BasePath = builderFolder };
                }
                var b = new CKomposableAppBuilder( parentPath, rootLogPath, monitor, msBuildOutputPath, applicationName, builderFolder, configuration );
                configure?.Invoke( monitor, b );
                var engineResult = b.EngineConfiguration.Run( monitor );
                return engineResult != null && engineResult.Status != RunStatus.Failed ? 0 : 1;
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
                return -2;
            }
            finally
            {
                monitor.MonitorEnd();
                GrandOutput.Default?.Dispose();
                NormalizedPath lastLog = Directory.EnumerateFiles( rootLogPath.AppendPart( "Text" ) ).OrderBy( f => File.GetLastWriteTimeUtc( f ) ).LastOrDefault();
                if( !lastLog.IsEmptyPath )
                {
                    NormalizedPath lastRun = lastLog.Combine( "../../../LastRun.log" );
                    File.Copy( lastLog, lastRun );
                }
            }
        }

        CKomposableAppBuilder( NormalizedPath parentPath,
                               NormalizedPath rootLogPath,
                               IActivityMonitor monitor,
                               NormalizedPath msBuildOutputPath,
                               string applicationName,
                               NormalizedPath builderFolderPath,
                               EngineConfiguration configuration )
        {
            _parentFolderPath = parentPath;
            _rootLogPath = rootLogPath;
            _monitor = monitor;
            _msBuildOutputPath = msBuildOutputPath;
            _applicationName = applicationName;
            _builderFolderPath = builderFolderPath;
            if( configuration.FirstBinPath.Path.IsEmptyPath )
            {
                configuration.FirstBinPath.Path = GetAppBinPath();
            }
            if( configuration.FirstBinPath.ProjectPath.IsEmptyPath )
            {
                configuration.FirstBinPath.ProjectPath = GetHostFolderPath();
            }
            _engineConfiguration = configuration;
        }
    }
}
