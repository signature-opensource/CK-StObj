using CK.Core;
using CK.Monitoring;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CK.Setup;

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
///     return await CK.Setup.CKomposableAppBuilder.RunAsync();
///     </code>
///     </item>
///     <item>The application to setup is a project named "XXX.Host".</item>
/// </list>
/// If a <c>ckonf.json</c> exists in the "XXX.Builder" project, it is the configuration of the builder: its Apps (keyed by
/// their name: "X" is the "X.App" and "X.Host" projects) with the configuration of their hosts (their "appsettings.json"
/// are projections of it), the aspects, and what the <see cref="IAppBuilderContributor"/> of the referenced engines deduce
/// from them. An optional <c>CKSetup.xml</c> brings the aspects that are not deduced yet.
/// <para>
/// Otherwise, if a <c>CKSetup.xml</c> exists in the "XXX.Builder" project, it automatically configures the <see cref="ICKomposableAppBuilder.EngineConfiguration"/>.
/// </para>
/// </para>
/// </summary>
public sealed class CKomposableAppBuilder : ICKomposableAppBuilder
{
    readonly NormalizedPath _parentFolderPath;
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
    /// The configuration is provided from the CKSetup.xml.
    /// </summary>
    /// <returns>The standard main result (0 on success, non zero on error).</returns>
    public static Task<int> RunAsync()
    {
        return DoRunAsync( null );
    }

    /// <summary>
    /// Shell method that handles the run of a setup from a ".App" to its ".Host" project.
    /// <para>
    /// The <paramref name="configure"/> can alter the CKSetup.xml (if it exists).
    /// </para>
    /// </summary>
    /// <param name="configure">Configure the <see cref="EngineConfiguration"/>.</param>
    /// <returns>The standard main result (0 on success, non zero on error).</returns>
    public static Task<int> RunAsync( Action<IActivityMonitor, ICKomposableAppBuilder> configure )
    {
        Throw.CheckNotNullArgument( configure );
        return DoRunAsync( configure );
    }

    /// <summary>
    /// Shell method that handles the run of a setup from a ".App" to its ".Host" project.
    /// <para>
    /// The <paramref name="configure"/> can alter the CKSetup.xml (if it exists).
    /// </para>
    /// </summary>
    /// <param name="configure">Configure the <see cref="EngineConfiguration"/>.</param>
    /// <returns>The standard main result (0 on success, non zero on error).</returns>
    public static Task<int> RunAsync( Func<IActivityMonitor, ICKomposableAppBuilder, Task> configure )
    {
        Throw.CheckNotNullArgument( configure );
        return DoRunAsync( configure );
    }

    static async Task<int> DoRunAsync( object? configure = null )
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

            EngineConfiguration? configuration;
            AppBuilderConfiguration? app = null;
            NormalizedPath ckSetupFile = builderFolder.AppendPart( "CKSetup.xml" );
            if( File.Exists( builderFolder.AppendPart( AppBuilderConfiguration.FileName ) ) )
            {
                // The builder's configuration is the source: the Apps, their hosts' "appsettings.json" and the aspects.
                // An optional "CKSetup.xml" brings the aspects that are not deduced yet.
                app = AppBuilderConfiguration.Load( monitor,
                                                    builderFolder,
                                                    msBuildOutputPath,
                                                    File.Exists( ckSetupFile ) ? System.Xml.Linq.XElement.Load( ckSetupFile ) : null,
                                                    out configuration );
                if( app == null ) return 1;
                Throw.DebugAssert( configuration != null );
                if( !AppBuilderContributors.Run( monitor, new AppBuilderContext( configuration, app.Apps, app.Aspects ), AppContext.BaseDirectory )
                    || !app.CheckAspectsHandled( monitor ) )
                {
                    return 1;
                }
            }
            else if( File.Exists( ckSetupFile ) )
            {
                monitor.Info( "Loading EngineConfiguration from CKSetup.xml file." );
                configuration = EngineConfiguration.Load( ckSetupFile );
            }
            else
            {
                configuration = new EngineConfiguration() { BasePath = builderFolder };
            }
            var b = new CKomposableAppBuilder( parentPath, monitor, msBuildOutputPath, applicationName, builderFolder, configuration );
            if( configure is Func<IActivityMonitor, ICKomposableAppBuilder, Task> a )
            {
                await a.Invoke( monitor, b ).ConfigureAwait( false );
            }
            else if( configure is Action<IActivityMonitor, ICKomposableAppBuilder> s )
            {
                s.Invoke( monitor, b );
            }
            // The hosts' "appsettings.json" are projections of the App's configuration.
            if( app != null && !app.WriteProjections( monitor ) ) return 1;
            var engineResult = await b.EngineConfiguration.RunAsync( monitor ).ConfigureAwait( false );
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
            var d = GrandOutput.Default;
            if( d != null ) await d.DisposeAsync().ConfigureAwait( false );
        }
    }

    CKomposableAppBuilder( NormalizedPath parentPath,
                           IActivityMonitor monitor,
                           NormalizedPath msBuildOutputPath,
                           string applicationName,
                           NormalizedPath builderFolderPath,
                           EngineConfiguration configuration )
    {
        _parentFolderPath = parentPath;
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
