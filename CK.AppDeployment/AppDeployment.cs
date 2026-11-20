using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CK.Setup;

/// <summary>
/// Builds the deployments of a solution (see <see cref="CentralDeployment"/>): the "Deployment/GenerateAssets.cs" of the
/// solution is
/// <code>
/// #:package CK.AppDeployment@x.y.z
/// return await CK.Setup.AppDeployment.RunAsync( args );
/// </code>
/// CKli runs it after the build of the repository, in the "Deployment" folder, with the version as the first argument
/// (the other ones are deployment names: all the deployments without them). For each deployment and each deployed
/// builder (<see cref="CentralDeploymentSettings.Builders"/>), it generates in "Deployment/.build/{AssetName}" (ignored by
/// git) a copy of the builder and of the hosts of its Apps (the files git tracks, the project references re-targeted: the
/// ".App" projects are the original ones), with a <see cref="CentralDeployment.BuilderFileName"/> that makes the builder
/// project the deployment, then publishes each host in "Deployment/Assets/{AssetName}/{Host}" (CKli zips each folder of
/// "Assets": one asset per deployment) with its "Deployment.json" manifest (and the version).
/// </summary>
public static partial class AppDeployment
{
    /// <summary>
    /// The folder of the generated builders and hosts, in the "Deployment" folder (ignored by git).
    /// </summary>
    public const string WorkFolderName = ".build";

    /// <summary>
    /// The folder of the assets, in the "Deployment" folder (CKli's: emptied before the run, its folders are zipped).
    /// </summary>
    public const string AssetsFolderName = "Assets";

    // The folders of a generated project that are kept from one run to another (the incremental builds).
    static readonly string[] _keptFolders = ["bin", "obj", "node_modules", ".angular"];

    /// <summary>
    /// Builds the deployments: the arguments are the version, then the names of the deployments to build (all of them
    /// when there is none). The current directory is in the solution (CKli runs it in the "Deployment" folder).
    /// </summary>
    /// <param name="args">The version, then the deployment names.</param>
    /// <returns>0 on success, 1 on error.</returns>
    public static async Task<int> RunAsync( string[] args )
    {
        var monitor = new ActivityMonitor();
        monitor.Output.RegisterClient( new ActivityMonitorConsoleClient() );
        try
        {
            if( args.Length == 0 || string.IsNullOrWhiteSpace( args[0] ) )
            {
                monitor.Error( "The version is expected as the first argument (then the names of the deployments to build: all of them by default)." );
                return 1;
            }
            return await RunAsync( monitor, Environment.CurrentDirectory, args[0], args[1..] ).ConfigureAwait( false ) ? 0 : 1;
        }
        catch( Exception ex )
        {
            monitor.Error( ex );
            return 1;
        }
        finally
        {
            monitor.MonitorEnd();
        }
    }

    /// <summary>
    /// Builds deployments of the solution of a folder.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="folder">A folder of the solution (its "Deployment" folder).</param>
    /// <param name="version">The version of the build.</param>
    /// <param name="names">The names of the deployments to build: all of them when empty.</param>
    /// <returns>True on success, false on error (errors are logged).</returns>
    public static async Task<bool> RunAsync( IActivityMonitor monitor, NormalizedPath folder, string version, IReadOnlyList<string> names )
    {
        if( !CentralConfiguration.TryLoad( monitor, folder, out var central ) ) return false;
        if( central == null )
        {
            monitor.Error( $"No central configuration ('{CentralConfiguration.FileName}' at the root of the repository) above '{folder}'." );
            return false;
        }
        if( !CentralDeployment.TryLoad( monitor, central, out var all ) ) return false;
        var deployments = new List<CentralDeployment>();
        foreach( var n in names )
        {
            var d = all.FirstOrDefault( d => d.Name.Equals( n, StringComparison.OrdinalIgnoreCase ) || d.AssetName.Equals( n, StringComparison.OrdinalIgnoreCase ) );
            if( d == null )
            {
                monitor.Error( $"Unknown deployment '{n}': the deployments are '{all.Select( d => d.Name ).Concatenate( "', '" )}'." );
                return false;
            }
            if( !deployments.Contains( d ) ) deployments.Add( d );
        }
        if( names.Count == 0 ) deployments.AddRange( all );
        if( deployments.Count == 0 )
        {
            monitor.Warn( $"No deployment to build in '{central.RootFolder.AppendPart( CentralDeployment.FolderName )}'." );
            return true;
        }
        var deploymentFolder = central.RootFolder.AppendPart( CentralDeployment.FolderName );
        var work = deploymentFolder.AppendPart( WorkFolderName );
        Directory.CreateDirectory( work );
        File.WriteAllText( work.AppendPart( ".gitignore" ), "*" + Environment.NewLine );
        var tracked = await GetTrackedFilesAsync( monitor, central.RootFolder ).ConfigureAwait( false );
        if( tracked == null ) return false;
        foreach( var d in deployments )
        {
            using( monitor.OpenInfo( $"Building the deployment '{d.Name}' (asset '{d.AssetName}')." ) )
            {
                if( !await BuildAsync( monitor, central, d, version, work.AppendPart( d.AssetName ), deploymentFolder.AppendPart( AssetsFolderName ).AppendPart( d.AssetName ), tracked ).ConfigureAwait( false ) )
                {
                    return false;
                }
            }
        }
        return true;
    }

    static async Task<bool> BuildAsync( IActivityMonitor monitor,
                                        CentralConfiguration central,
                                        CentralDeployment deployment,
                                        string version,
                                        NormalizedPath work,
                                        NormalizedPath assets,
                                        IReadOnlyList<string> tracked )
    {
        var hosts = new List<(NormalizedPath Project, string Name)>();
        foreach( var b in deployment.DeployedBuilders )
        {
            var original = central.Builders.FirstOrDefault( o => o.Path.Equals( b.Path, StringComparison.OrdinalIgnoreCase ) );
            if( original == null )
            {
                monitor.Error( $"The builder '{b.Path}' of the deployment '{deployment.Name}' is not a builder of the solution: a deployment builds the projects of the solution." );
                return false;
            }
            // The generated builder keeps the path of the original one ("Front/Auth3.Front.Builder"), its hosts are next to it.
            var builderFolder = work.Combine( b.Path );
            var builderProject = FindProject( monitor, original.Folder );
            if( builderProject == null ) return false;
            var copies = new Dictionary<string, NormalizedPath>( StringComparer.OrdinalIgnoreCase )
            {
                { builderProject, builderFolder.AppendPart( Path.GetFileName( builderProject ) ) }
            };
            var appHosts = new List<(NormalizedPath Original, NormalizedPath Copy, NormalizedPath Project)>();
            foreach( var a in b.Apps )
            {
                var o = original.Apps.FirstOrDefault( x => x.Name.Equals( a.Name, StringComparison.OrdinalIgnoreCase ) );
                if( o == null )
                {
                    monitor.Error( $"The {a} of the deployment '{deployment.Name}' is not an App of the builder '{b.Path}' of the solution." );
                    return false;
                }
                var hostProject = FindProject( monitor, o.HostProjectFolder );
                if( hostProject == null ) return false;
                // The convention of the generated builder: "../{Name}.Host".
                var copy = builderFolder.RemoveLastPart().AppendPart( $"{a.Name}.Host" );
                var copiedProject = copy.AppendPart( Path.GetFileName( hostProject ) );
                copies.Add( hostProject, copiedProject );
                appHosts.Add( (o.HostProjectFolder, copy, copiedProject) );
            }
            if( !CopyProject( monitor, central.RootFolder, original.Folder, builderFolder, tracked, copies, isHost: false ) ) return false;
            File.Delete( builderFolder.AppendPart( AppBuilderConfiguration.FileName ) );
            deployment.WriteBuilderFile( builderFolder, b.Path );
            foreach( var (o, copy, project) in appHosts )
            {
                if( !CopyProject( monitor, central.RootFolder, o, copy, tracked, copies, isHost: true ) ) return false;
                hosts.Add( (project, copy.LastPart) );
            }
        }
        if( hosts.Count == 0 )
        {
            monitor.Warn( $"The deployment '{deployment.Name}' deploys no builder." );
            return true;
        }
        foreach( var (project, name) in hosts )
        {
            var output = assets.AppendPart( name );
            using( monitor.OpenInfo( $"Publishing '{name}' in '{output}'." ) )
            {
                var arguments = new List<string> { "publish", project, "-c", "Release", "-o", output, $"-p:Version={version}",
                                                   // The "#Dev" folder is the development's: a deployment never writes in it.
                                                   "-p:CKAppIdentityDevFolder=false" };
                if( deployment.Settings.RuntimeIdentifier != null ) arguments.AddRange( ["-r", deployment.Settings.RuntimeIdentifier] );
                arguments.Add( deployment.Settings.SelfContained ? "--self-contained" : "--no-self-contained" );
                var lines = new List<string>();
                if( await RunProcessAsync( monitor, "dotnet", arguments, project.RemoveLastPart(), lines ).ConfigureAwait( false ) != 0 )
                {
                    // The errors of MSBuild (a failing builder: see its Logs folder), or the end of the output.
                    var errors = lines.Where( l => l.Contains( ": error " ) ).Distinct().ToList();
                    foreach( var l in errors.Count > 0 ? errors : lines.TakeLast( 30 ) ) monitor.Error( l );
                    monitor.Error( $"The publication of '{name}' for the deployment '{deployment.Name}' failed: the generated projects are in '{project.RemoveLastPart( 2 )}' (the logs of a builder are in its Logs folder)." );
                    return false;
                }
                // The manifest written by the builder, with the version.
                var manifest = project.RemoveLastPart().AppendPart( AppBuilderConfiguration.DeploymentManifestFileName );
                if( !File.Exists( manifest ) )
                {
                    monitor.Error( $"The builder of '{name}' didn't write its manifest '{manifest}'." );
                    return false;
                }
                var json = (JsonObject)JsonNode.Parse( File.ReadAllText( manifest ) )!;
                json.Insert( 1, "Version", version );
                File.WriteAllText( output.AppendPart( AppBuilderConfiguration.DeploymentManifestFileName ),
                                   json.ToJsonString( new JsonSerializerOptions { WriteIndented = true } ) + Environment.NewLine );
            }
        }
        return true;
    }

    // The single ".csproj" of a project folder.
    static NormalizedPath? FindProject( IActivityMonitor monitor, NormalizedPath folder )
    {
        var projects = Directory.Exists( folder ) ? Directory.GetFiles( folder, "*.csproj" ) : [];
        if( projects.Length == 1 ) return projects[0];
        monitor.Error( $"A single '.csproj' is expected in '{folder}' ({projects.Length} found)." );
        return null;
    }

    // Copies the files that git tracks in a project folder (a host: without its "Properties"), the project references
    // re-targeted: on the copies when the referenced project is copied, on the original otherwise.
    static bool CopyProject( IActivityMonitor monitor,
                             NormalizedPath root,
                             NormalizedPath source,
                             NormalizedPath target,
                             IReadOnlyList<string> tracked,
                             IReadOnlyDictionary<string, NormalizedPath> copies,
                             bool isHost )
    {
        var prefix = source.RemovePrefix( root ).Path + '/';
        var files = tracked.Where( f => f.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
                           .Select( f => f.Substring( prefix.Length ) )
                           .Where( f => !isHost || !f.StartsWith( "Properties/", StringComparison.OrdinalIgnoreCase ) )
                           .ToList();
        if( files.Count == 0 )
        {
            monitor.Error( $"No file tracked by git in '{source}'." );
            return false;
        }
        Clean( target );
        foreach( var f in files )
        {
            var to = target.Combine( f );
            Directory.CreateDirectory( to.RemoveLastPart() );
            File.Copy( source.Combine( f ), to, overwrite: true );
            if( f.EndsWith( ".csproj", StringComparison.OrdinalIgnoreCase ) && !f.Contains( '/' ) )
            {
                var text = File.ReadAllText( to );
                var retargeted = ProjectReference().Replace( text, m =>
                {
                    var referenced = new NormalizedPath( Path.GetFullPath( Path.Combine( source, m.Groups[2].Value ) ) );
                    var actual = copies.TryGetValue( referenced, out var copy ) ? copy : referenced;
                    return m.Groups[1].Value + Path.GetRelativePath( target, actual ) + m.Groups[3].Value;
                } );
                File.WriteAllText( to, retargeted );
            }
        }
        monitor.Info( $"{files.Count} file(s) of '{source}' copied in '{target}'." );
        return true;

        // Deletes what a previous run copied, keeps the outputs and the caches of the incremental builds.
        static void Clean( NormalizedPath folder )
        {
            if( !Directory.Exists( folder ) ) return;
            foreach( var f in Directory.GetFiles( folder ) ) File.Delete( f );
            foreach( var d in Directory.GetDirectories( folder ) )
            {
                if( _keptFolders.Contains( Path.GetFileName( d ), StringComparer.OrdinalIgnoreCase ) ) continue;
                Clean( d );
            }
        }
    }

    [GeneratedRegex( """(<ProjectReference\s+Include=")([^"]+)(")""" )]
    private static partial Regex ProjectReference();

    // The files tracked by git, relative to the root ('/' separated).
    static async Task<IReadOnlyList<string>?> GetTrackedFilesAsync( IActivityMonitor monitor, NormalizedPath root )
    {
        var files = new List<string>();
        var code = await RunProcessAsync( monitor, "git", ["ls-files", "-z"], root, output: files ).ConfigureAwait( false );
        if( code != 0 )
        {
            monitor.Error( $"'git ls-files' failed in '{root}'." );
            return null;
        }
        return files.SelectMany( l => l.Split( '\0', StringSplitOptions.RemoveEmptyEntries ) ).ToList();
    }

    static async Task<int> RunProcessAsync( IActivityMonitor monitor, string fileName, IEnumerable<string> arguments, NormalizedPath workingDirectory, List<string>? output = null )
    {
        var info = new ProcessStartInfo( fileName )
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach( var a in arguments ) info.ArgumentList.Add( a );
        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += ( _, e ) =>
        {
            if( e.Data == null ) return;
            if( output != null ) lock( output ) output.Add( e.Data );
            else monitor.ParallelLogger.Trace( e.Data );
        };
        process.ErrorDataReceived += ( _, e ) => { if( e.Data != null ) monitor.ParallelLogger.Warn( e.Data ); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync().ConfigureAwait( false );
        return process.ExitCode;
    }
}
