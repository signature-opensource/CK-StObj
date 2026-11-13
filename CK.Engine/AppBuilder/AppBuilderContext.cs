using System.Collections.Generic;

namespace CK.Setup;

/// <summary>
/// What the <see cref="IAppBuilderContributor"/> receive: the BinPaths of the App ("ckonf.json") and the engine
/// configuration they complete.
/// </summary>
public sealed class AppBuilderContext
{
    internal AppBuilderContext( EngineConfiguration engineConfiguration, IReadOnlyList<AppBinPath> binPaths )
    {
        EngineConfiguration = engineConfiguration;
        BinPaths = binPaths;
    }

    /// <summary>
    /// Gets the engine configuration: its BinPaths are the <see cref="BinPaths"/> (and the root of an optional "CKSetup.xml").
    /// </summary>
    public EngineConfiguration EngineConfiguration { get; }

    /// <summary>
    /// Gets the BinPaths of the App, sorted by name.
    /// </summary>
    public IReadOnlyList<AppBinPath> BinPaths { get; }
}
