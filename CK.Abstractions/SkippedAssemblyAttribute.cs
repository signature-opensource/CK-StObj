using System;

namespace CK.Setup;

/// <summary>
/// Marks this assembly to be totally ignored. By default "Microsoft.*" and "System.*" assemblies are skipped.
/// <para>
/// This is an "existential exclusion": the potential effects of a skipped assembly are ignored. For instance, skipping a <see cref="IsPFeatureDefinerAttribute"/>
/// assembly makes the assemblies that depend on it no more PFeatures.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
public sealed class SkippedAssemblyAttribute : Attribute
{
}
