using System.Collections.Generic;

namespace CK.Demo;

/// <summary>
/// Group of similar <see cref="BinPathConfiguration"/> in terms of types to consider.
/// </summary>
public sealed class BinPathTypeGroup
{
    /// <summary>
    /// Gets the configurations for this group.
    /// This is empty when <see cref="IsUnifiedPure"/> is true.
    /// <para>
    /// All these configurations result in the same set of assemblies and types.
    /// </para>
    /// </summary>
    public List<BinPathConfiguration> Configurations { get; } = new List<BinPathConfiguration>();

    /// <summary>
    /// Gets whether this group is the purely unified one.
    /// When true, no similar configuration exist (<see cref="Configurations"/> is empty).
    /// <para>
    /// This unified BinPath has no IAutoService, only IPoco and IRealObject, but all the IPoco and all the IRealObject
    /// of all the other BinPaths.
    /// This BinPath is only used to enables Aspects that interact with the real world to handle every aspect (!) of the IRealObject
    /// during the code generation but this code will never be used.
    /// </para>
    /// </summary>
    public bool IsUnifiedPure => Configurations.Count == 0;

}
