using System;

namespace CK.Setup;

/// <summary>
/// Excludes one or more types of automatic registration.
/// <para>
/// Excluding a type, just like excluding an assembly with <see cref="ExcludePFeatureAttribute"/>, is
/// "weak", it impacts the initial type set that will be considered. An excluded type referenced by a
/// registered one will eventually be considered or can be included back at a higher level by
/// <see cref="RegisterCKTypeAttribute"/>.
/// </para>
/// <para>
/// Exclusion applies "from the leaves": the most specialized types of a base type must be excluded for
/// the "base" type to also be excluded.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
public class ExcludeCKTypeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="ExcludeCKTypeAttribute"/>.
    /// </summary>
    /// <param name="type">The first type to exclude.</param>
    /// <param name="otherTypes">Other types to exclude.</param>
    public ExcludeCKTypeAttribute( Type type, params Type[] otherTypes )
    {
    }
}
