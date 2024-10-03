using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Declares one or more types from a referenced assembly that could have been hidden by <see cref="ExcludePFeatureAttribute"/>
/// or by a <see cref="ExcludeCKTypeAttribute"/> in referenced assemblies or are in a regular (non PFeature) assembly.
/// <list type="bullet">
///     <item>The types to register must be public (<see cref="Type.IsVisible"/>) otherwise it is a setup error.</item>
///     <item>They can belong to any assembly (excluded or not).</item>
/// </list>
/// This attribute also enables an external service to be configured. See <see cref="ConfigurableAutoServiceKind"/>.
/// </summary>
[AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
public sealed class RegisterCKTypeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="RegisterCKTypeAttribute"/>.
    /// </summary>
    /// <param name="type">The first type to expose.</param>
    /// <param name="otherTypes">Other types to expose.</param>
    public RegisterCKTypeAttribute( Type type, params Type[] otherTypes )
    {
    }

    /// <summary>
    /// Initializes a new <see cref="RegisterCKTypeAttribute"/> that declares a <see cref="ConfigurableAutoServiceKind"/>
    /// for the type.
    /// </summary>
    /// <param name="type">The first type to expose.</param>
    /// <param name="kind">The <see cref="ConfigurableAutoServiceKind"/> of the type.</param>
    public RegisterCKTypeAttribute( Type type, ConfigurableAutoServiceKind kind )
    {
    }

}
