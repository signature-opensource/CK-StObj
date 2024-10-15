namespace CK.Engine.TypeCollector;

/// <summary>
/// Captures <see cref="ICachedType.GenericArguments"/> information.
/// </summary>
public readonly struct CachedGenericArgument
{
    readonly CachedGenericParameter _parameter;
    readonly ICachedType? _type;

    internal CachedGenericArgument( CachedGenericParameter parameter, ICachedType? type )
    {
        _type = type;
        _parameter = parameter;
    }

    /// <summary>
    /// Gets the generic parameter.
    /// </summary>
    public CachedGenericParameter GenericParameter => _parameter;

    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public ICachedType? ParameterType => _type;

    /// <summary>
    /// Gets the <see cref="ParameterType"/> name if it is bound, or the <see cref="GenericParameter"/> name.
    /// </summary>
    /// <returns>This argument.</returns>
    public override string ToString() => _type?.ToString() ?? _parameter.Name;
}
