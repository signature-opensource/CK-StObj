using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Exposes engine status.
    /// </summary>
    public interface IStObjEngineStatus
    {
        /// <summary>
        /// Gets the current success status of the Engine.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Gets the current (mutable) path. You may use ToArray or ToList methods to take
        /// a snapshot of this list.
        /// Use the extension method <see cref="ActivityMonitorExtension.ToStringPath"/> to easily format this path.
        /// </summary>
        IReadOnlyList<ActivityMonitorPathCatcher.PathElement> DynamicPath { get; }

        /// <summary>
        /// Gets the last <see cref="DynamicPath"/> where an <see cref="Core.LogLevel.Error"/>
        /// or a CK.Core.LogLevel.Fatal occurred. Null if no error nor fatal occurred.
        /// Use the extension method <see cref="ActivityMonitorExtension.ToStringPath"/> to easily format this path.
        /// </summary>
        IReadOnlyList<ActivityMonitorPathCatcher.PathElement> LastErrorPath { get; }

        /// <summary>
        /// Gets the last <see cref="DynamicPath"/> with a <see cref="Core.LogLevel.Fatal"/>, <see cref="Core.LogLevel.Error"/>
        /// or a <see cref="Core.LogLevel.Warn"/>.
        /// Null if no error, fatal nor warn occurred. 
        /// Use the extension method <see cref="ActivityMonitorExtension.ToStringPath"/> to easily format this path.
        /// </summary>
        IReadOnlyList<ActivityMonitorPathCatcher.PathElement> LastWarnOrErrorPath { get; }
    }
}
