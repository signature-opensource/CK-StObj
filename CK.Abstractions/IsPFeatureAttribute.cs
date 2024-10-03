using System;

namespace CK.Setup;

/// <summary>
/// Marks an assembly as being a PFeature. The types it contains must be processed by CKomposable.
/// <para>
/// This attribute doesn't need to be defined if the assembly eventually depends on an assembly that is
/// marked with <see cref="IsPFeatureDefinerAttribute"/> because being a PFeature is transitive: assemblies
/// that evenually depends on a PFeature are also PFeatures.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
public sealed class IsPFeatureAttribute : Attribute
{
}
