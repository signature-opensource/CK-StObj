using System;

namespace CK.Setup;

/// <summary>
/// Declares that an injected engine service is allowed to not be immediately available.
/// </summary>
[AttributeUsage( AttributeTargets.Parameter, AllowMultiple = false )]
public sealed class WaitForAttribute : Attribute
{
}
