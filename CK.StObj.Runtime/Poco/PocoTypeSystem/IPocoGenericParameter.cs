using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Captures <see cref="IPocoGenericTypeDefinition.Parameters"/> information.
/// </summary>
public interface IPocoGenericParameter
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the parameter attributes.
    /// </summary>
    GenericParameterAttributes Attributes { get; }
}
