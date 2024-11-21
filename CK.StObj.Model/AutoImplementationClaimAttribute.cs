using System;

namespace CK.Core;

/// <summary>
/// When applied on an abstract class, method or property, this attribute claims that there exists a way to
/// generate the eventual implementation even if it doesn't provide the mean.
/// <para>
/// This attribute, just like <see cref="IRealObject"/>, <see cref="IAutoService"/>, <see cref="IScopedAutoService"/>, 
/// <see cref="ISingletonAutoService"/>, <see cref="PreventAutoImplementationAttribute"/> or <see cref="ReplaceAutoServiceAttribute"/>
/// can be created anywhere: as long as the name is "AutoImplementationClaimAttribute" (regardless of the namespace), it will be honored.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event, AllowMultiple = false, Inherited = false )]
public sealed class AutoImplementationClaimAttribute : Attribute
{
}


