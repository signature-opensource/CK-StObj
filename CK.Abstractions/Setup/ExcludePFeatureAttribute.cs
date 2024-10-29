using System;

namespace CK.Setup;

/// <summary>
/// Prevents the specified referenced assembly's types to be considered when registering this assembly.
/// <para>
/// This is a "weak" exclusion used to compute a set of initial types to register: types from this
/// assembly can perfectly be registered explicitly or through other types and the excluded assembly is
/// still a PFeature (that can be included back by any upper level assembly).
/// </para>
/// <para>
/// This allows to hide all the types of a referenced assembly by default and to opt-in exposing some of
/// their types by using <see cref="RegisterCKTypeAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
public sealed class ExcludePFeatureAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="ExcludePFeatureAttribute"/>.
    /// The assembly name is checked at setup and if it is not found in this assembly's references closure
    /// (it may already been excluded by referenced assemblies), only a warning is emitted. This enables
    /// to guaranty the this assembly will exclude <paramref name="assemblyName"/> regardless of any
    /// changes in its referenced assemblies.
    /// </summary>
    /// <param name="assemblyName">The simple assembly name or "this" to exclude this assembly.</param>
    public ExcludePFeatureAttribute( string assemblyName )
    {
        AssemblyName = assemblyName;
    }

    /// <summary>
    /// Gets the referenced assembly name for which types must not be automatically registered.
    /// </summary>
    public string AssemblyName { get; }
}
