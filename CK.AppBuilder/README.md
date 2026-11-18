# CK.AppBuilder

Runs the CKomposable engine from a "XXX.Builder" project, whose `Program.cs` is one line:

```csharp
return await CK.Setup.CKomposableAppBuilder.RunAsync();
```

The builder reads its `ckonf.json` (its Apps and their aspects), computes the type sets of its Apps, applies the
aspects found in them, runs the contributors of the engines it references, then the engine (that reuses the analysis
of the assemblies), and writes the `appsettings.json` of the hosts: the `ckonf.json`, its projections and the
contributors' contract are described in [the App builder model](https://github.com/signature-opensource/CK-ResourceSpace/blob/stable/CK.Engine.Configuration/AppBuilder/README.md)
(CK.Engine.Configuration). Without `ckonf.json`, the `CKSetup.xml` of the builder configures the engine.

When the repository has a central configuration (a `ckonf.json` at its git root), the builder first writes its own
`ckonf.json` from its entry in it (with the contributors' `IAppBuilderContributor.ProjectFromCentral`): a builder that is
not in its `"Builders"` is an error.

## The contributors

The builder finds the contributors (`[assembly: AppBuilderContributor( typeof( MyContributor ) )]`) in its own folder:
nothing to write in the "XXX.Builder" project. It runs them before the engine.

- **A constructor parameter is a requirement:** a contributor has one public constructor whose parameters are other
  contributors; they are created and run before it. A cycle is an error.

## The engines

The engines that the models of the Apps require (their `[RequiredEngine]`, and the ones these engines require) must be
loadable from the builder's folder: the builder references them. Before the run, the engine checks it and one error
lists the missing ones with the models that require them (`CKEngine.CheckRequiredEngines`).
