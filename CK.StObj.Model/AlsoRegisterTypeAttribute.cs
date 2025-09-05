using System;

namespace CK.Setup;

/// <summary>
/// Enables any registered type to register another type.
/// </summary>
[Obsolete( "Use CK.Core.AlsoRegisterTypeAttribute<T1,...> instead." )]
[AttributeUsage( AttributeTargets.All, AllowMultiple = true, Inherited = false )]
public class AlsoRegisterTypeAttribute : ContextBoundDelegationAttribute
{
    /// <summary>
    /// Initializes a <see cref="AlsoRegisterTypeAttribute"/> with a type
    /// that must be registered.
    /// </summary>
    /// <param name="type">A type (typically a nested type) that must be registered.</param>
    public AlsoRegisterTypeAttribute( Type type )
        : base( "CK.Setup.AlsoRegisterTypeAttributeImpl, CK.StObj.Engine" )
    {
        Type = type;
    }

    /// <summary>
    /// Gets the type that must be registered.
    /// </summary>
    public Type Type { get; }
}
