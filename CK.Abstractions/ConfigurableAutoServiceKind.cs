using System;

namespace CK.Setup;

/// <summary>
/// Subset of the Automatic DI types categorization that can be applied to external classes or interfaces.
/// <para>
/// This applies only to Automatic Services. Real Objects and Poco can only be defined by code.
/// </para>
/// </summary>
[Flags]
public enum ConfigurableAutoServiceKind
{
    /// <summary>
    /// No specific information is known about the type.
    /// </summary>
    None = 0,

    /// <summary>
    /// The service is known to be a scoped service. Each Unit of Work is provided a unique instance.
    /// <list type="bullet">
    ///     <item><term>Rejects</term><description><see cref="IsSingleton"/></description></item>
    /// </list>
    /// </summary>
    IsScoped = 1 << 7,

    /// <summary>
    /// The service is known to be a singleton service: all Unit of Works (including concurrent ones) will use the exact same instance.
    /// <list type="bullet">
    ///     <item><term>Rejects</term><description><see cref="IsScoped"/></description></item>
    /// </list>
    /// </summary>
    IsSingleton = 1 << 8,

    /// <summary>
    /// The type is a DI service available in some containers but not necessarily in all of them.
    /// <para>
    /// It is up to each DIContainerDefinition to register an implementation or not for this service
    /// in its ConfigureContainerServices method.
    /// </para>
    /// </summary>
    IsContainerConfiguredService = 1 << 11,

    /// <summary>
    /// The type is a DI scoped service necessarily available in all contexts that automatically flows from endpoints
    /// to the background context.
    /// <list type="bullet">
    ///     <item><term>Implies</term><description><see cref="IsScoped"/> and <see cref="IsContainerConfiguredService"/></description></item>
    /// </list>
    /// </summary>
    IsAmbientService = 1 << 14,

    /// <summary>
    /// Multiple registration flag. Applies only to interfaces. See <see cref="IsMultipleAttribute"/>. 
    /// </summary>
    IsMultipleService = 1 << 15,

}
