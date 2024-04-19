using System;

namespace CK.Core
{
    /// <summary>
    /// Detailed flags that categorizes service types used by the Automatic DI.
    /// This is a subset of a more complex enumeration defined and used by the engine but
    /// due to the hybrid nature of the DI configuration, these flags need to be known by
    /// the generated code that configures the DI containers: this is why they are exposed
    /// at the Model level.
    /// </summary>
    [Flags]
    public enum AutoServiceKind
    {
        /// <summary>
        /// Not a service we handle or external service for which
        /// no lifetime nor any information is known.
        /// </summary>
        None = 0,

        /// <summary>
        /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
        /// </summary>
        IsAutoService = 1 << 6,

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
        /// A <see cref="IRealObject"/> is a true singleton.
        /// <list type="bullet">
        ///     <item><term>Implies</term><description><see cref="IsSingleton"/></description></item>
        ///     <item><term>Rejects</term><description><see cref="IsMultipleService"/></description></item>
        /// </list>
        /// </summary>
        IsRealObject = 1 << 10,

        /// <summary>
        /// The type is a DI service available in some endpoint contexts but not necessarily in all of them.
        /// <para>
        /// It is up to each <see cref="DIContainerDefinition{TScopeData}.ConfigureEndpointServices"/> to register
        /// an implementation or not for this service.
        /// </para>
        /// </summary>
        IsEndpointService = 1 << 11,

        /// <summary>
        /// The type is a DI scoped service necessarily available in all contexts that automatically flows from endpoints
        /// to the background context.
        /// <list type="bullet">
        ///     <item><term>Implies</term><description><see cref="IsScoped"/> and <see cref="IsEndpointService"/></description></item>
        /// </list>
        /// </summary>
        IsAmbientService = 1 << 14,

        /// <summary>
        /// Multiple registration flag. Applies only to interfaces. See <see cref="IsMultipleAttribute"/>. 
        /// <list type="bullet">
        ///     <item><term>Implies</term><description></description></item>
        ///     <item><term>Rejects</term><description><see cref="IsRealObject"/></description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// Such "Multiple" services must be registered with TryAddEnumerable instead of TryAdd.
        /// </remarks>
        IsMultipleService = 1 << 15,
    }
}
