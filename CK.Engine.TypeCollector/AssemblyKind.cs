using CK.Setup;
using System;

namespace CK.Engine.TypeCollector;

[Flags]
public enum AssemblyKind
{
    /// <summary>
    /// This assembly is unknown or non relevant: it references no <see cref="PFeatureDefiner"/> nor <see cref="PFeature"/>
    /// and is not a CKEngine.
    /// </summary>
    None,

    /// <summary>
    /// This assembly has been skipped. It is a system assembly that we totally ignore.
    /// </summary>
    SystemSkipped = 1,

    /// <summary>
    /// This assembly has been skipped. It is a system assembly that we totally ignore.
    /// </summary>
    AutoSkipped = 2,

    /// <summary>
    /// This assembly has been excluded by configuration.
    /// </summary>
    Excluded = 4,

    /// <summary>
    /// This assembly is on the engine side.
    /// </summary>
    Engine = 8,

    /// <summary>
    /// This is a definer assembly (it is marked with a <see cref="IsPFeatureAttribute"/>).
    /// Assemblies that reference it are <see cref="PFeature"/>.
    /// </summary>
    PFeatureDefiner = 16,

    /// <summary>
    /// The assembly's visible types will be registered: it is either marked with a <see cref="IsPFeatureAttribute"/>
    /// or depends (at any depth) on another PFeature or an assembly that is marked with a <see cref="IsPFeatureDefinerAttribute"/>.
    /// </summary>
    PFeature = 32
}

public static class AssemblyKindExtensions
{
    /// <summary>
    /// Gets whether this assembly is <see cref="AssemblyKind.AutoSkipped"/> or <see cref="AssemblyKind.SystemSkipped"/>.
    /// </summary>
    /// <param name="kind">This kind.</param>
    /// <returns>Whether this assembly is skipped.</returns>
    public static bool IsSkipped( this AssemblyKind kind ) => (kind & (AssemblyKind.SystemSkipped | AssemblyKind.AutoSkipped)) != 0;

    public static bool IsExcluded( this AssemblyKind kind ) => (kind & AssemblyKind.Excluded) != 0;
    public static bool IsNone( this AssemblyKind kind ) => (kind & ~AssemblyKind.Excluded) == 0;
    public static bool IsPFeature( this AssemblyKind kind ) => (kind & AssemblyKind.PFeature) != 0;
    public static bool IsPFeatureDefiner( this AssemblyKind kind ) => (kind & AssemblyKind.PFeatureDefiner) != 0;
    public static bool IsEngine( this AssemblyKind kind ) => (kind & AssemblyKind.Engine) != 0;

    public static bool IsPFeatureOrDefiner( this AssemblyKind kind ) => (kind & (AssemblyKind.PFeature | AssemblyKind.PFeatureDefiner)) != 0;

    // Thes two ones are warnings and Excluded is removed.
    public static bool IsExcludedEngine( this AssemblyKind kind ) => (kind & (AssemblyKind.Engine | AssemblyKind.Excluded)) == (AssemblyKind.Engine | AssemblyKind.Excluded);
    public static bool IsExcludedPFeatureDefiner( this AssemblyKind kind ) => (kind & (AssemblyKind.PFeatureDefiner | AssemblyKind.Excluded)) == (AssemblyKind.PFeatureDefiner | AssemblyKind.Excluded);

    public static AssemblyKind SetEngine( this AssemblyKind k )
    {
        k &= AssemblyKind.PFeature | AssemblyKind.PFeatureDefiner;
        return k | AssemblyKind.Engine;
    }

    public static AssemblyKind SetPFeature( this AssemblyKind k )
    {
        k &= AssemblyKind.Engine | AssemblyKind.PFeatureDefiner;
        return k | AssemblyKind.PFeature;
    }
}
