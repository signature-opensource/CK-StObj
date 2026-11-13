using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup;

/// <summary>
/// A BinPath of the App: a party, declared in "ckonf.json" by its name ("App:BinPaths:{Name}"). Its ".App" project
/// gives its <see cref="BinPathConfiguration.Path"/>, its ".Host" project its <see cref="BinPathConfiguration.ProjectPath"/>
/// and receives the projection of its <see cref="Configuration"/> (its "appsettings.json").
/// </summary>
public sealed class AppBinPath
{
    readonly HashSet<string> _compileKeys;

    internal AppBinPath( string name,
                         NormalizedPath appProjectPath,
                         NormalizedPath hostProjectPath,
                         ImmutableConfigurationSection configuration,
                         BinPathConfiguration binPathConfiguration )
    {
        Name = name;
        AppProjectPath = appProjectPath;
        HostProjectPath = hostProjectPath;
        Configuration = configuration;
        BinPathConfiguration = binPathConfiguration;
        _compileKeys = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
    }

    /// <summary>
    /// Gets the name of this BinPath (its key in "App:BinPaths").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the ".App" project folder.
    /// </summary>
    public NormalizedPath AppProjectPath { get; }

    /// <summary>
    /// Gets the ".Host" project folder.
    /// </summary>
    public NormalizedPath HostProjectPath { get; }

    /// <summary>
    /// Gets the configuration of this BinPath ("App:BinPaths:{Name}"): the configuration of its host, and its compile time
    /// information.
    /// </summary>
    public ImmutableConfigurationSection Configuration { get; }

    /// <summary>
    /// Gets the engine configuration of this BinPath.
    /// </summary>
    public BinPathConfiguration BinPathConfiguration { get; }

    /// <summary>
    /// Declares a compile time key: it is not written in the projection of the <see cref="Configuration"/> (the host's
    /// "appsettings.json"), unless the App keeps them ("App:ProjectCompileKeys": true).
    /// </summary>
    /// <param name="path">The path of the key, relative to <see cref="Configuration"/> (with ':' separators).</param>
    public void AddCompileKey( string path )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( path );
        _compileKeys.Add( path );
    }

    internal IReadOnlyCollection<string> CompileKeys => _compileKeys;

    /// <inheritdoc />
    public override string ToString() => $"BinPath '{Name}'";
}
