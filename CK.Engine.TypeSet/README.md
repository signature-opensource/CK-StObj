# CK.Engine.TypeSet

The types that the engine considers for each BinPath: the BinPaths are grouped by the types they end up with, and
each group computes its named type sets.

[CK.Engine.TypeCollector](../CK.Engine.TypeCollector/README.md) gives the assemblies and the types they provide (the
`AssemblyCache` and the `GlobalTypeCache`). This project applies the type configuration of the BinPaths on top of it.

## BinPathTypeGroup

`BinPathTypeGroup.Run( IActivityMonitor, EngineConfiguration )` normalizes the configuration (null on a configuration
error), runs the `AssemblyCache`, then builds the groups. Its `BinPathTypeGroup.Result` exposes `Success`, the
`AssemblyCache`, the `TypeCache`, the `Groups` and a `Signature` of the whole result.

For each BinPath, the types of its assemblies are completed by its configuration, in this order:

1. its `BinPathConfiguration.ExcludedTypes` are removed;
2. its `BinPathConfiguration.Types` are added (with their configured kind);
3. the `Types` of its named type sets are added: the inclusion in a set is an inclusion in the BinPath;
4. the `[AlsoRegisterType]` closure of the selection is added: it is the last step and ignores any exclusion.

The BinPaths that share the same assemblies and the same type configuration form one group: `BinPathTypeGroup.Configurations`
lists them, `BinPathTypeGroup.ConfiguredTypes` holds their types and `BinPathTypeGroup.Signature` identifies them.

When there are several groups, the IRealObject and the IPoco of all of them must be handled by one group: the group that
covers them all comes first, or a "(Unified)" group (`BinPathTypeGroup.IsUnifiedPure`) is created for them.

## Named type sets

`BinPathTypeGroup.TypeSets` are the sets declared by `BinPathConfiguration.TypeSets`, by their name (case insensitive).
A set is a subset of the group: the types of the group minus the set's `ExcludedTypes`, plus the `[AlsoRegisterType]`
closure of what remains. The sets are part of the group's signature.
