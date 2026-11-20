# CK.AppDeployment

Builds the deployments of a CKomposable solution: the folders of its `Deployment` folder (next to its central
`ckonf.json`) whose name starts with `#` (`#Production`, `Paris/Shop/#Staging`). A deployment is an overlay of the
central configuration (see "The deployments" in [the App builder model](https://github.com/signature-opensource/CK-ResourceSpace/blob/stable/CK.Engine.Configuration/AppBuilder/README.md)).

The solution's `Deployment/GenerateAssets.cs` is two lines:

```csharp
#:package CK.AppDeployment@x.y.z
return await CK.Setup.AppDeployment.RunAsync( args );
```

CKli runs it after the build of the repository (`dotnet run GenerateAssets.cs -- {version}` in the `Deployment`
folder). The arguments are the version, then the names of the deployments to build (all of them without names).

## What it does

For each deployment and each builder that it deploys (`"Deployment": { "Builders": [...] }`, all by default):

1. **It generates a triplet** in `Deployment/.build/{AssetName}/` (ignored by git; the asset name drops the `#` and maps
   `/` to `.`: `Production`, `Paris.Shop.Staging`), with the path of the original builder
   (`Deployment/.build/Production/Front/Auth3.Front.Builder`):
   - the builder: a copy of the files that git tracks in the original one (without its `ckonf.json`), and a
     `ckonf.deployment.json` that makes it build the deployment: it projects the merged configuration of the
     deployment in its `ckonf.json`;
   - the hosts of its Apps: a copy of the files that git tracks in the original ones, without `Properties`;
   - the `.App` projects are not copied: the project references of the copies are re-targeted on the originals (and on
     the copied builder for a host).

   The outputs of a previous run (`bin`, `obj`, `node_modules`, the installed packages of the client applications: their
   yarn cache and Plug'n'Play files) are kept: the builds are incremental. A failing
   deployment is a triplet that can be opened and built by hand.
2. **It publishes each host** (`dotnet publish -c Release` with the version) in `Deployment/Assets/{AssetName}-{App}`
   (`Production-Auth3.Front`): the build of the host runs its generated builder. CKli zips each folder of `Assets`: one
   release asset per App (`Production-Auth3.Front.zip`). With `"SingleZipAsset": true`, the hosts are in
   `Deployment/Assets/{AssetName}/{App}`: one asset per deployment (`Production.zip`). The MSBuild properties of the publication (the `"Publish"` of the deployment's settings and of its App:
   `RuntimeIdentifier`, `SelfContained`, `PublishSingleFile`... applied as-is) are written in a `<PropertyGroup>` of the
   copied host project, not on the command line: a global property would reach the generated builder, that runs on the
   build machine during the build.
3. **It adds the manifest** that the builder wrote (`$StObjGen/Deployment.manifest`), with the version, to the
   `CK.AppHost.Published.json` of the published host (its `"Deployment"`): the full name of the party, the deployment and
   its bases, the publication properties, the names of the secrets that the host needs (given at deploy time: the
   `Secrets` section of the host's configuration), the values per deployment (its addresses, its public URLs), the
   instances of a fleet.

The build of a deployment never writes in the `#Dev` folder, never reads a secret and never sets up a database: a
builder whose engine sets up a database at build time (the SqlServer aspect) fails, it must be left out of the
deployment. The client applications of a host are built by its publication (CK.TS.AspNet.Clients) and published with
it; a published host uses its own folder as its content root (CK.AppHost).
