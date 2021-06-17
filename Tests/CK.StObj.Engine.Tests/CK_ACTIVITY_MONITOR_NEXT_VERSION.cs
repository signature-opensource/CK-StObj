using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests
{
    static class CK_ACTIVITY_MONITOR_NEXT_VERSION
    {
        /// <summary>
        /// Enables simple "using" syntax to easily collect any <see cref="LogLevel"/> (or above) entries (defaults to <see cref="LogLevel.Error"/>) around operations.
        /// </summary>
        /// <param name="this">This <see cref="IActivityMonitor"/>.</param>
        /// <param name="entries">Collector for <see cref="ActivityMonitorSimpleCollector.Entry">entries</see>.</param>
        /// <param name="level">Defines the level of the collected entries (by default fatal or error entries).</param>
        /// <param name="capacity">Capacity of the collector defaults to 50.</param>
        /// <returns>A <see cref="IDisposable"/> object used to manage the scope of this handler.</returns>
        public static IDisposable CollectEntries( this IActivityMonitor @this, out IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries, LogLevelFilter level = LogLevelFilter.Error, int capacity = 50 )
        {
            ActivityMonitorSimpleCollector errorTracker = new ActivityMonitorSimpleCollector() { MinimalFilter = level, Capacity = capacity };
            @this.Output.RegisterClient( errorTracker );
            entries = errorTracker.Entries;
            return Util.CreateDisposableAction( () =>
            {
                @this.Output.UnregisterClient( errorTracker );
            } );
        }
    }
}
