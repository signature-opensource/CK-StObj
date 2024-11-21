using CK.Setup;
using System;

namespace CK.Core;

/// <summary>
/// Enables a Poco type to explicitly register a Poco compliant type that is not
/// necessarily reachable from any <see cref="IPoco"/>.
/// <para>
/// This attribute can be specialized to trigger other aspects for the type.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = true )]
public class RegisterPocoTypeAttribute : Attribute, IAttributeContextBound
{
    /// <summary>
    /// Initializes a new <see cref="RegisterPocoTypeAttribute"/>.
    /// </summary>
    /// <param name="t">The type to register.</param>
    public RegisterPocoTypeAttribute( Type t )
    {
        Type = t;
    }

    /// <summary>
    /// Gets the type that must be registered.
    /// </summary>
    public Type Type { get; }
}
