using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// This interface is automaticaly implemented on the <see cref="PocoDirectory"/>.
/// </summary>
public interface IPocoDirectoryExchangeGenerated
{
    /// <summary>
    /// Gets the available <see cref="ExchangeableRuntimeFilter"/>.
    /// </summary>
    IReadOnlyCollection<ExchangeableRuntimeFilter> RuntimeFilters { get; }

    /// <summary>
    /// Gets the named runtime filter ot throws an <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="name">The <see cref="ExchangeableRuntimeFilter.Name"/> to find.</param>
    /// <returns>The filter.</returns>
    ExchangeableRuntimeFilter GetRuntimeFilter( string name );
}
