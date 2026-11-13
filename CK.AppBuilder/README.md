# CK.AppBuilder

Runs the CKomposable engine from a "XXX.Builder" project, whose `Program.cs` is one line:

```csharp
return await CK.Setup.CKomposableAppBuilder.RunAsync();
```

## `ckonf.json`: the configuration of the builder

When the "XXX.Builder" project has a `ckonf.json`, it is the configuration of what the builder builds in one engine run:
its Apps, keyed by their name, each one with the configuration of its host, and the aspects.

```jsonc
{
  // The Builder aspects: they apply to the whole engine run.
  "Aspects": { ... },
  "Apps": {
    "Back1": {
      "Types": [ "CK.Core.IActivityMonitor, CK.ActivityMonitor",
                 { "Name": "CK.Core.IParallelLogger, CK.ActivityMonitor", "Kind": "IsContainerConfiguredService|IsScoped" } ],
      // The aspects of this App.
      "Aspects": { "Globalization": { "ActiveCultures": "fr, en" } },
      "FullName": "Sample/MultiBinPath/$Back1/#Dev",
      "Local": { ... },
      "CK-Monitoring": { ... }
    },
    "Back2": { ... }
  }
}
```

- **An App is a process:** the App "Back1" is the "../Back1.App" project (its code; its bin folder is the one of the
  builder: `bin/{Configuration}/{TargetFramework}`) and the "../Back1.Host" project (its host, the `ProjectPath` of
  its engine BinPath). `"AppProject"` and `"HostProject"` (relative to the `ckonf.json` folder) depart from this
  convention. A path that leaves the solution's folder is an error.
- **The compile time keys of an App:** `Types` (an assembly qualified name, or `{ "Name", "Kind" }`),
  `ExcludedTypes`, `Assemblies`, `DiscoverAssembliesFromPath`, `CompileOption`, `GenerateSourceFiles`, `AppProject`,
  `HostProject` and `Aspects`. The other keys are the configuration of the host.
- **The aspects are compile time,** at the root (the Builder aspects) and in an App (its aspects), keyed by their
  name. Each one is read by the contributor of the package that handles it: an aspect that no contributor reads is an
  error.
- **The keys are case insensitive** (it is a configuration): a duplicate key (`Back1` and `back1`) is an error. The
  order of the Apps means nothing: the engine receives them sorted by name.
- **The host's `appsettings.json` is a projection of its App:** its content without the compile time keys (the ones
  above, and the ones the contributors declare, unless `"ProjectCompileKeys": true`), the keys that the contributors
  project in a section moved in it (the keys of the party in `"CK-AppIdentity"`). It is generated (its first line
  says so) and written only when it changes: edit the `ckonf.json`, not the projection.
- **A `CKSetup.xml` may complete it** with the aspects that are not deduced yet and the global options of the engine,
  but no BinPath.

Without `ckonf.json`, the `CKSetup.xml` of the builder configures the engine, as before.

## The contributors

The engines that the builder references deduce the engine configuration from the `ckonf.json`: they declare an
`IAppBuilderContributor` (CK.Engine, next to `ICKomposableAppBuilder`) with
`[assembly: AppBuilderContributor( typeof( MyContributor ) )]`. The builder finds them in its own folder (nothing to
write in the "XXX.Builder" project) and runs them before the engine:

- `Contribute( monitor, context )` reads the `context.Apps` (an `AppConfiguration`: its name, its projects, its
  `Configuration` section, its `Aspects`, its engine `BinPathConfiguration`) and the Builder `context.Aspects`, and
  completes the `context.EngineConfiguration` (the aspects it deduces or reads).
  - `AppAspects.TryGetAspect( name )` reads an aspect and marks it as handled.
  - `AppConfiguration.AddCompileKey( path )` keeps a key out of the projection.
  - `AppConfiguration.ProjectInSection( key, sectionName )` moves a key in a section of the projection.
- `OnContributed( monitor, context )` is called once all of them have contributed: a contributor can check what the
  others did.
- **A constructor parameter is a requirement:** a contributor has one public constructor whose parameters are other
  contributors; they are created and run before it. A cycle is an error.
