using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Extends <see cref="IExtMemberInfo"/>.
/// </summary>
public interface IExtParameterInfo : IExtMemberInfo
{
    /// <summary>
    /// Gets the parameter info.
    /// </summary>
    ParameterInfo ParameterInfo { get; }
}
