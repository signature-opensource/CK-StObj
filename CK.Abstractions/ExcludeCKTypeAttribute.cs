using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Excludes one or more types of automatic registration.
/// <para>
/// Excluding a type, just like excluding an assembly with <see cref="ExcludePFeatureAttribute"/>, is
/// "weak", it impacts the initial type set that will be considered. An excluded type referenced by a
/// registered one will eventually be registered.
/// Exclusion applies "from the leaves": most specialized types must be excluded for a "base" type
/// to also be excluded.
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
