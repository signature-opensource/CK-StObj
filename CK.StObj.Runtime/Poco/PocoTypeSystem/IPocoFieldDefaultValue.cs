namespace CK.Setup;

/// <summary>
/// Captures the <c>[DefaultValue(...)]</c> attribute or default parameter value
/// of record struct.
/// </summary>
public interface IPocoFieldDefaultValue
{
    /// <summary>
    /// Gets the default value when field's type is a simple, basic, type.
    /// <para>
    /// For complex type, this is null: the default value is typically obtained
    /// by creating a default instance of the type.
    /// </para>
    /// </summary>
    object? SimpleValue { get; }

    /// <summary>
    /// Gets the default value in C# source code.
    /// For complex type, this is typically a "new XXX()" expression.
    /// </summary>
    string ValueCSharpSource { get; }
}
