using System;

namespace CK.Setup;

/// <summary>
/// Assembly attribute that states that this assembly requires a CKomposable Engine to be setup.
/// This is typically used by PFeatures definers (see <see cref="IsPFeatureDefinerAttribute"/>) or PFeatures
/// (see <see cref="IsPFeatureAttribute"/>) assemblies to declare one or more associated engines.
/// </summary>
[AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
public sealed class RequiredEngineAttribute : Attribute
{
    /// <summary>
    /// Initializes a new required engine dependency attribute.
    /// </summary>
    /// <param name="assemblyName">Name of the CKomposable Engine assembly.</param>
    /// <param name="versionRange">
    /// A NuGet version range (see https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort#version-ranges)
    /// of the Engine to use.
    /// <list type="bullet">
    ///     <item>The default <c>null</c> allows any version.</item>
    ///     <item>To specify a minimal version, simply writes it: "1.2.0-a" is the same as "[1.2.0-a,)" and allows the 1.2.0-a and greater ones.</item>
    ///     <item>To fix a version, use "[1.2.0-a]" (the inclusive brackets).</item>
    /// </list>
    /// </param>
    public RequiredEngineAttribute( string assemblyName, string? versionRange = null )
    {
    }
}
