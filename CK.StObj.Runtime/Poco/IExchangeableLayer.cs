using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Captures a layer of additional type that must not be exchangeable.
    /// Layrs are created by <see cref="IPocoTypeSystem.CreateAndApplyExchangeableLayer(IActivityMonitor, Func{IPocoType, bool}, out IExchangeableLayer)"/>
    /// or <see cref="IPocoTypeSystem.CreateAndApplyExchangeableLayer(IActivityMonitor, IEnumerable{IPocoType}, out IExchangeableLayer)"/>.
    /// </summary>
    public interface IExchangeableLayer
    {
        /// <summary>
        /// Gets the source type system.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets whether this layer is currently applied.
        /// </summary>
        bool IsApplied { get; }
    }
}
