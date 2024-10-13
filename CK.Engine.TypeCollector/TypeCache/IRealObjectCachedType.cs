using CK.Core;
using System.Collections.Immutable;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Specialized <see cref="ICachedType"/> when <see cref="ICachedType.Type"/> is <see cref="IRealObject"/>.
/// </summary>
public interface IRealObjectCachedType : ICachedType
{
    /// <summary>
    /// Tries to get the <c>public static void Requires( ... )</c> method it it exists.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="configureMethods">The Requires method or null if it doesn't exist.</param>
    /// <returns>True on success, false on error.</returns>
    bool TryGetRequiresMethod( IActivityMonitor monitor, out CachedMethodInfo? requiresMethod );

    /// <summary>
    /// Tries to get all the <c>public static void Configure( IActivityMonitor monitor, ConfiguredType configuredType, RealObjectType instance, ... )</c>
    /// methods.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="configureMethods">The Configure methods.</param>
    /// <returns>True on success, false on error.</returns>
    bool TryGetConfigureMethods( IActivityMonitor monitor, out ImmutableArray<CachedMethodInfo> configureMethods );
}
