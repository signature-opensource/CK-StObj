
# AssemblyCollector
Collects assemblies, analyzing `IsPFeatureAttribute`, `IsPFeatureDefinerAttribute`, `ExcludePFeatureAttribute` and their referenced assemblies recursively.

Assemblies are considered as sets of types, they are used as a source for the type collector. The `AssemblyCollector` centralizes cached assemblies and
cached types.

The real collectors are the nested `BinPath` that groups similar assembly assembly configurations accross the multiple `BinPathConfiguration`: the work is
done only once if the configuration has the same `Path`, `DiscoverAssembliesFromPath` and `Assemblies` (this is an optimization for the multi-BinPath mode).

## The nested BinPath

Assemblies are always loaded from the `AppContext.BaseDirectory` in the `AssemblyLoadContext.Default`, there is no sandbox or any
assembly load context involved. This collector works in two exclusive ways:

- Explicit mode: When there are at least one configured `Assemblies`. 
  It is an error to:
  - Add an assembly name without its corresponding file `Path/name.dll`.
  - Add an assembly that doesn't exist in the `AppContext.BaseDirectory`.
  - Add an assembly whose last write time in `Path` is newer than the one in `AppContext.BaseDirectory`.
  - Add an engine assembly (any assembly that depends on a engine is an engine).
  - Assembly loading error are errors.

  - Each of these explicitly loaded assemblies are "heads": they will provide their types.

- Discover mode: All files in the `Path` are analyzed.
  - Only "*.dll" files are considered.
  - Only their file name matters: the same assembly file name must be available in the `AppContext.BaseDirectory`.
  - When `Path` is not the `AppContext.BaseDirectory`, the last write time of the `Path`'s file must not be newer than the BaseDirectory's one.
  - Assembly loading error are only warnings, not errors (to ignore any native .dlls that may exist).

PFeatures that are not referenced by any CKEngines are the "heads": only them will provide their types. An edge case is when a folder has
no "user PFeatures", when all the PFeatures are "basic" assemblies that are managed by a CKEngine.
In this case there is no "head". This is sound, there's nothing to process and can be trivially handled by using the Explicit mode.


