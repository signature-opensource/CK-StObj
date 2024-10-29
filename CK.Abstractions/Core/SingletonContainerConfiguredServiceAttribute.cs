using System;

namespace CK.Abstractions;

/// <summary>
/// States that the decorated class or interface is a singleton service that is registered specifically
/// by Dependency Injection container.
/// </summary>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
public sealed class SingletonContainerConfiguredServiceAttribute : Attribute
{
}
