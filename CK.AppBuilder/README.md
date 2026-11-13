# CK.AppBuilder

Runs the CKomposable engine from a "XXX.Builder" project, whose `Program.cs` is one line:

```csharp
return await CK.Setup.CKomposableAppBuilder.RunAsync();
```

## `ckonf.json`: the configuration of the App

When the "XXX.Builder" project has a `ckonf.json`, it is the configuration of the App: its BinPaths (its parties),
keyed by their name, each one with the configuration of its host.

```jsonc
{
  "App": {
    "BinPaths": {
      "Back1": {
        "Types": [ "CK.Core.IActivityMonitor, CK.ActivityMonitor",
                   { "Name": "CK.Core.IParallelLogger, CK.ActivityMonitor", "Kind": "IsContainerConfiguredService|IsScoped" } ],
        "CK-AppIdentity": { "FullName": "Sample/MultiBinPath/$Back1/#Dev", "Local": { ... } },
        "CK-Monitoring": { ... }
      },
      "Back2": { ... }
    }
  }
}
```

- **The paths follow the BinPath's name:** the BinPath "Back1" is the "../Back1.App" project (its bin folder is the
  one of the builder: `bin/{Configuration}/{TargetFramework}`) and the "../Back1.Host" project (its `ProjectPath`).
  `"AppProject"` and `"HostProject"` (relative to the `ckonf.json` folder) depart from this convention. A path that
  leaves the solution's folder is an error.
- **The compile time keys of a BinPath:** `Types` (an assembly qualified name, or `{ "Name", "Kind" }`),
  `ExcludedTypes`, `Assemblies`, `DiscoverAssembliesFromPath`, `CompileOption`, `GenerateSourceFiles`, `AppProject`,
  `HostProject`. The other keys are the configuration of the host.
- **The keys are case insensitive** (it is a configuration): a duplicate key (`Back1` and `back1`) is an error. The
  order of the BinPaths means nothing: the engine receives them sorted by name.
- **The host's `appsettings.json` is a projection of its BinPath:** its content without the compile time keys (the
  ones above, and the ones the contributors declare, unless `"App": { "ProjectCompileKeys": true }`). It is generated
  (its first line says so) and written only when it changes: edit the `ckonf.json`, not the projection.
- **A `CKSetup.xml` may complete it** with the aspects that are not deduced yet and the global options of the engine,
  but no BinPath.

Without `ckonf.json`, the `CKSetup.xml` of the builder configures the engine, as before.

## The contributors

The engines that the builder references can deduce the engine configuration from the App's configuration: they declare
an `IAppBuilderContributor` (CK.Engine, next to `ICKomposableAppBuilder`) with
`[assembly: AppBuilderContributor( typeof( MyContributor ) )]`. The builder finds them
in its own folder (nothing to write in the "XXX.Builder" project) and runs them before the engine:

- `Contribute( monitor, context )` reads the `context.BinPaths` (an `AppBinPath`: its name, its projects, its
  `Configuration` section, its engine `BinPathConfiguration`) and completes the `context.EngineConfiguration` (the aspects
  it deduces); `AppBinPath.AddCompileKey( path )` keeps a key out of the projection.
- `OnContributed( monitor, context )` is called once all of them have contributed: a contributor can check what the
  others did.
- **A constructor parameter is a requirement:** a contributor has one public constructor whose parameters are other
  contributors; they are created and run before it. A cycle is an error.
