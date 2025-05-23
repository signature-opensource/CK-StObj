using System;

namespace CK.Core;

/// <summary>
/// Default implementation of <see cref="Setup.IRealObjectAttribute"/>.
/// </summary>
[AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
public class RealObjectAttribute : Attribute, Setup.IRealObjectAttribute
{
    /// <inheritdoc />
    public Type? Container { get; set; }

    /// <inheritdoc />
    public DependentItemKindSpec ItemKind { get; set; }

    /// <inheritdoc />
    public TrackAmbientPropertiesMode TrackAmbientProperties { get; set; }

    /// <inheritdoc />
    public Type[]? Requires { get; set; }

    /// <inheritdoc />
    public Type[]? RequiredBy { get; set; }

    /// <inheritdoc />
    public Type[]? Children { get; set; }

    /// <inheritdoc />
    public Type[]? Groups { get; set; }

}
