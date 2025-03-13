using CK.Core;
using System;

namespace CK.Setup;

/// <summary>
/// Extends <see cref="IServiceProvider"/> with simple DI methods (see <see cref="SimpleObjectActivator"/>).
/// </summary>
public static class ServiceProviderExtension
{
    /// <summary>
    /// Attempts to locate a <see cref="ISimpleObjectActivator"/> service or falls back to
    /// a default <see cref="SimpleObjectActivator"/> and calls <see cref="ISimpleObjectActivator.Create"/>
    /// on it.
    /// Returns null on error.
    /// </summary>
    /// <param name="this">This <see cref="IServiceProvider"/>.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="t">Type to instantiate.</param>
    /// <param name="requiredParameters">Optional required parameters.</param>
    /// <returns>A new instance on success, null on error.</returns>
    public static object? SimpleObjectCreate( this IServiceProvider @this, IActivityMonitor monitor, Type t, params object[] requiredParameters )
    {
        Throw.CheckNotNullArgument( monitor );
        ISimpleObjectActivator activator = @this.GetService<ISimpleObjectActivator>( false );
        if( activator == null )
        {
            activator = new SimpleObjectActivator();
        }
        return activator.Create( monitor, t, @this, requiredParameters );
    }

}
