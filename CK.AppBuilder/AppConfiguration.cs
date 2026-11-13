using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace CK.Setup;

/// <summary>
/// The configuration of the App, read from the "ckonf.json" of its "XXX.Builder" project: its BinPaths (its parties)
/// keyed by their name. It is the future root of the engine: the engine still receives an <see cref="EngineConfiguration"/>,
/// that this projects (its BinPaths, the root of an optional "CKSetup.xml" for the aspects that are not deduced yet).
/// <para>
/// The paths are taken in control, top-down: everything is relative to the "ckonf.json" folder; a BinPath "X" is the
/// "../X.App" project (its bin folder is the one of the builder: "bin/{Configuration}/{TargetFramework}") and the
/// "../X.Host" project ("AppProject" and "HostProject" depart from this convention). A path that leaves the solution's
/// folder is an error.
/// </para>
/// <para>
/// The "appsettings.json" of each host is a projection of its BinPath: its content without its compile time keys.
/// </para>
/// </summary>
sealed class AppConfiguration
{
    /// <summary>
    /// The configuration file name, in the "XXX.Builder" project.
    /// </summary>
    public const string FileName = "ckonf.json";

    // The compile time keys of a BinPath (not projected).
    static readonly string[] _binPathKeys = ["AppProject", "HostProject", "Types", "ExcludedTypes", "Assemblies",
                                             "DiscoverAssembliesFromPath", "CompileOption", "GenerateSourceFiles"];

    static readonly JsonSerializerOptions _writeOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly NormalizedPath _builderFolder;
    readonly JsonObject _binPathsNode;
    readonly bool _projectCompileKeys;
    readonly IReadOnlyList<AppBinPath> _binPaths;

    AppConfiguration( NormalizedPath builderFolder, JsonObject binPathsNode, bool projectCompileKeys, IReadOnlyList<AppBinPath> binPaths )
    {
        _builderFolder = builderFolder;
        _binPathsNode = binPathsNode;
        _projectCompileKeys = projectCompileKeys;
        _binPaths = binPaths;
    }

    /// <summary>
    /// Gets the BinPaths, sorted by name.
    /// </summary>
    public IReadOnlyList<AppBinPath> BinPaths => _binPaths;

    /// <summary>
    /// Reads the "ckonf.json" of a builder and creates the engine configuration.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="builderFolder">The "XXX.Builder" project folder (that contains the "ckonf.json").</param>
    /// <param name="msBuildOutputPath">The output path of the builder ("bin/Debug/net10.0"): the one of the ".App" projects.</param>
    /// <param name="ckSetup">The optional "CKSetup.xml" root: its aspects and its global options. It must not define BinPaths.</param>
    /// <param name="engineConfiguration">The engine configuration on success.</param>
    /// <returns>The App configuration, or null on error (errors are logged).</returns>
    public static AppConfiguration? Load( IActivityMonitor monitor,
                                          NormalizedPath builderFolder,
                                          NormalizedPath msBuildOutputPath,
                                          XElement? ckSetup,
                                          out EngineConfiguration? engineConfiguration )
    {
        engineConfiguration = null;
        var file = builderFolder.AppendPart( FileName );
        using var _ = monitor.OpenInfo( $"Reading '{file}'." );
        string json;
        JsonObject root;
        ImmutableConfigurationSection section;
        try
        {
            json = File.ReadAllText( file );
            // The configuration is case insensitive: "Back1" and "back1" are the same key.
            using( var doc = JsonDocument.Parse( json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true } ) )
            {
                if( !CheckDuplicateKeys( monitor, file, doc.RootElement, "" ) ) return null;
            }
            section = ImmutableConfigurationSection.CreateFromJson( "ckonf", json );
            root = JsonNode.Parse( json,
                                   new JsonNodeOptions { PropertyNameCaseInsensitive = true },
                                   new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true } )
                   as JsonObject
                   ?? throw new InvalidDataException( "A JSON object is expected." );
        }
        catch( Exception ex )
        {
            monitor.Error( $"Invalid '{file}'.", ex );
            return null;
        }
        if( !FindSolutionFolder( monitor, builderFolder, out var solutionFolder ) ) return null;

        bool success = true;
        foreach( var c in section.GetChildren() )
        {
            if( c.Key is not ("App" or "$schema") )
            {
                monitor.Error( $"Unknown key '{c.Key}' in '{file}': the root has only \"App\"." );
                success = false;
            }
        }
        var app = section.TryGetSection( "App" );
        var binPathsSection = app?.TryGetSection( "BinPaths" );
        if( app == null || binPathsSection == null || binPathsSection.GetChildren().Count == 0 )
        {
            monitor.Error( $"'{file}' must define \"App\": {{ \"BinPaths\": {{ \"Name\": {{ ... }} }} }} (at least one BinPath, keyed by its name)." );
            return null;
        }
        foreach( var c in app.GetChildren() )
        {
            if( c.Key is not ("BinPaths" or "ProjectCompileKeys") )
            {
                monitor.Error( $"Unknown key 'App:{c.Key}' in '{file}': the keys of \"App\" are BinPaths and ProjectCompileKeys." );
                success = false;
            }
        }
        bool projectCompileKeys = false;
        var p = app["ProjectCompileKeys"];
        if( p != null && !bool.TryParse( p, out projectCompileKeys ) )
        {
            monitor.Error( $"Invalid \"App:ProjectCompileKeys\": '{p}' (true or false is expected)." );
            success = false;
        }
        var binPathsNode = root["App"]?["BinPaths"] as JsonObject;
        if( binPathsNode == null )
        {
            monitor.Error( $"\"App:BinPaths\" in '{file}' must be an object: the BinPaths are keyed by their name." );
            return null;
        }
        if( ckSetup != null && ckSetup.Elements( EngineConfiguration.xBinPaths ).Elements().Any() )
        {
            monitor.Error( $"The \"CKSetup.xml\" of '{builderFolder}' defines BinPaths: with a '{FileName}', the BinPaths are its \"App:BinPaths\" (the \"CKSetup.xml\" may only define the aspects that are not deduced yet)." );
            success = false;
        }
        if( !success ) return null;

        // The BinPaths, sorted by name (their order means nothing: the engine's FirstBinPath is the first name).
        var binPaths = new List<(string Name, NormalizedPath App, NormalizedPath Host, ImmutableConfigurationSection Section)>();
        var xBinPaths = new XElement( EngineConfiguration.xBinPaths );
        foreach( var b in binPathsSection.GetChildren().OrderBy( b => b.Key, StringComparer.OrdinalIgnoreCase ) )
        {
            if( b.Value != null )
            {
                monitor.Error( $"\"{b.Path}\" must be an object." );
                success = false;
                continue;
            }
            var appProject = ResolveProject( monitor, builderFolder, solutionFolder, b, "AppProject", $"{b.Key}.App" );
            var hostProject = ResolveProject( monitor, builderFolder, solutionFolder, b, "HostProject", $"{b.Key}.Host" );
            if( appProject.IsEmptyPath || hostProject.IsEmptyPath )
            {
                success = false;
                continue;
            }
            var x = CreateBinPathXml( monitor, b, appProject.Combine( msBuildOutputPath ), hostProject );
            if( x == null )
            {
                success = false;
                continue;
            }
            xBinPaths.Add( x );
            binPaths.Add( (b.Key, appProject, hostProject, b) );
        }
        if( !success ) return null;

        var xRoot = ckSetup != null ? new XElement( ckSetup ) : new XElement( "Setup" );
        xRoot.Elements( EngineConfiguration.xBinPaths ).Remove();
        xRoot.Add( xBinPaths );
        try
        {
            engineConfiguration = new EngineConfiguration( xRoot );
            if( engineConfiguration.BasePath.IsEmptyPath ) engineConfiguration.BasePath = builderFolder;
        }
        catch( Exception ex )
        {
            monitor.Error( $"While creating the engine configuration from '{file}'.", ex );
            return null;
        }
        var e = engineConfiguration;
        var appBinPaths = binPaths.Select( b => new AppBinPath( b.Name, b.App, b.Host, b.Section, e.FindRequiredBinPath( b.Name ) ) )
                                  .ToArray();
        return new AppConfiguration( builderFolder, binPathsNode, projectCompileKeys, appBinPaths );
    }

    static bool CheckDuplicateKeys( IActivityMonitor monitor, NormalizedPath file, JsonElement e, string path )
    {
        bool success = true;
        if( e.ValueKind == JsonValueKind.Object )
        {
            var names = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
            foreach( var p in e.EnumerateObject() )
            {
                var pPath = path.Length == 0 ? p.Name : $"{path}:{p.Name}";
                if( !names.Add( p.Name ) )
                {
                    monitor.Error( $"Duplicate key '{pPath}' in '{file}' (the keys are case insensitive)." );
                    success = false;
                }
                success &= CheckDuplicateKeys( monitor, file, p.Value, pPath );
            }
        }
        else if( e.ValueKind == JsonValueKind.Array )
        {
            int i = 0;
            foreach( var item in e.EnumerateArray() ) success &= CheckDuplicateKeys( monitor, file, item, $"{path}:{i++}" );
        }
        return success;
    }

    static bool FindSolutionFolder( IActivityMonitor monitor, NormalizedPath builderFolder, out NormalizedPath solutionFolder )
    {
        for( var f = builderFolder.RemoveLastPart(); !f.IsEmptyPath; f = f.RemoveLastPart() )
        {
            if( Directory.Exists( f ) && Directory.EnumerateFiles( f, "*.sln*" ).Any( s => s.EndsWith( ".sln", StringComparison.OrdinalIgnoreCase )
                                                                                       || s.EndsWith( ".slnx", StringComparison.OrdinalIgnoreCase ) ) )
            {
                solutionFolder = f;
                return true;
            }
        }
        monitor.Error( $"No solution file (.sln or .slnx) found above '{builderFolder}': the paths of '{FileName}' must stay in the solution's folder." );
        solutionFolder = default;
        return false;
    }

    static NormalizedPath ResolveProject( IActivityMonitor monitor,
                                          NormalizedPath builderFolder,
                                          NormalizedPath solutionFolder,
                                          ImmutableConfigurationSection binPath,
                                          string key,
                                          string conventionName )
    {
        var relative = binPath[key] ?? $"../{conventionName}";
        NormalizedPath full = Path.GetFullPath( Path.Combine( builderFolder, relative ) );
        if( !full.StartsWith( solutionFolder, strict: true ) )
        {
            monitor.Error( $"The path '{relative}' of \"{binPath.Path}:{key}\" ('{full}') leaves the solution's folder '{solutionFolder}'." );
            return default;
        }
        if( !Directory.Exists( full ) )
        {
            monitor.Error( $"The project folder '{full}' of {binPath.Key} doesn't exist{(binPath[key] == null ? $" (by convention: '{conventionName}'; \"{key}\" departs from it)" : "")}." );
            return default;
        }
        return full;
    }

    // The engine's BinPath: the same XML as a "CKSetup.xml" BinPath, so that the engine receives what it always received.
    static XElement? CreateBinPathXml( IActivityMonitor monitor, ImmutableConfigurationSection b, NormalizedPath binFolder, NormalizedPath hostProject )
    {
        var x = new XElement( EngineConfiguration.xBinPath,
                              new XAttribute( EngineConfiguration.xName, b.Key ),
                              new XAttribute( EngineConfiguration.xPath, binFolder ),
                              new XElement( EngineConfiguration.xProjectPath, hostProject.Path ) );
        if( b["DiscoverAssembliesFromPath"] is string discover ) x.Add( new XAttribute( EngineConfiguration.xDiscoverAssembliesFromPath, discover ) );
        if( b["CompileOption"] is string compile ) x.Add( new XElement( EngineConfiguration.xCompileOption, compile ) );
        if( b["GenerateSourceFiles"] is string generate ) x.Add( new XElement( EngineConfiguration.xGenerateSourceFiles, generate ) );
        var assemblies = ReadList( b, "Assemblies" );
        if( assemblies.Count > 0 ) x.Add( new XElement( EngineConfiguration.xAssemblies, assemblies.Select( a => new XElement( EngineConfiguration.xAssembly, a ) ) ) );
        var excluded = ReadList( b, "ExcludedTypes" );
        if( excluded.Count > 0 ) x.Add( new XElement( EngineConfiguration.xExcludedTypes, excluded.Select( t => new XElement( EngineConfiguration.xType, t ) ) ) );
        var types = b.TryGetSection( "Types" );
        if( types != null )
        {
            var xTypes = new XElement( EngineConfiguration.xTypes );
            foreach( var t in types.GetChildren() )
            {
                // A type is its assembly qualified name, or { "Name": "...", "Kind": "..." }.
                var name = t.Value ?? t["Name"];
                if( string.IsNullOrWhiteSpace( name ) )
                {
                    monitor.Error( $"Invalid \"{t.Path}\": a type is its assembly qualified name, or {{ \"Name\": \"...\", \"Kind\": \"...\" }}." );
                    return null;
                }
                var kind = t.Value == null ? t["Kind"] : null;
                xTypes.Add( new XElement( EngineConfiguration.xType, kind != null ? new XAttribute( EngineConfiguration.xKind, kind ) : null, name ) );
            }
            x.Add( xTypes );
        }
        return x;
    }

    static List<string> ReadList( ImmutableConfigurationSection c, string key )
    {
        var value = c[key];
        return value != null
                ? value.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).ToList()
                : c.TryGetSection( key )?.GetChildren()
                                         .Select( v => v.Value )
                                         .Where( v => !string.IsNullOrWhiteSpace( v ) )
                                         .Select( v => v!.Trim() )
                                         .ToList()
                  ?? [];
    }

    /// <summary>
    /// Writes the "appsettings.json" of each host: the content of its BinPath without its compile time keys (the ones of
    /// the BinPath, and the ones the contributors declared unless "App:ProjectCompileKeys" is true). A file is written only
    /// when it changes.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    public bool WriteProjections( IActivityMonitor monitor )
    {
        bool success = true;
        foreach( var b in _binPaths )
        {
            var node = (JsonObject)_binPathsNode.First( kv => StringComparer.OrdinalIgnoreCase.Equals( kv.Key, b.Name ) ).Value!.DeepClone();
            foreach( var k in _binPathKeys ) node.Remove( k );
            if( !_projectCompileKeys )
            {
                foreach( var path in b.CompileKeys ) Remove( node, path.Split( ':' ) );
            }
            var file = b.HostProjectPath.AppendPart( "appsettings.json" );
            var text = $"// Generated from {_builderFolder.LastPart}/{FileName}: do not edit.{Environment.NewLine}"
                       + node.ToJsonString( _writeOptions )
                       + Environment.NewLine;
            try
            {
                if( File.Exists( file ) && File.ReadAllText( file ) == text ) continue;
                File.WriteAllText( file, text );
                monitor.Info( $"'{file}' written from '{FileName}'." );
            }
            catch( Exception ex )
            {
                monitor.Error( $"While writing '{file}'.", ex );
                success = false;
            }
        }
        return success;

        static void Remove( JsonObject o, string[] path )
        {
            for( int i = 0; i < path.Length - 1; ++i )
            {
                if( o[path[i]] is not JsonObject child ) return;
                o = child;
            }
            o.Remove( path[^1] );
        }
    }
}
