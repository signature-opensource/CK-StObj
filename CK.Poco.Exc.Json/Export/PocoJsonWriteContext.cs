using CK.Core;

namespace CK.Poco.Exc.Json;

/// <summary>
/// Context object that is provided to all the write methods.
/// </summary>
/// <remarks>
/// This holds the <see cref="Options"/> and the <see cref="RuntimeFilter"/>.
/// </remarks>
public sealed class PocoJsonWriteContext
{
    readonly PocoJsonExportOptions _options;
    readonly ExchangeableRuntimeFilter _typeFilter;

    /// <summary>
    /// Initialize a new writer context.
    /// </summary>
    /// <param name="pocoDirectory">The <see cref="PocoDirectory"/>.</param>
    /// <param name="options">Options to use. Defaults to <see cref="PocoJsonExportOptions.Default"/></param>
    public PocoJsonWriteContext( PocoDirectory pocoDirectory, PocoJsonExportOptions? options = null )
    {
        Throw.CheckNotNullArgument( pocoDirectory );
        options ??= PocoJsonExportOptions.Default;
        _typeFilter = ((IPocoDirectoryExchangeGenerated)pocoDirectory).GetRuntimeFilter( options.TypeFilterName );
        _options = options;
    }

    /// <summary>
    /// Gets the options.
    /// </summary>
    public PocoJsonExportOptions Options => _options;

    /// <summary>
    /// Gets the type filter from the <see cref="PocoJsonExportOptions.TypeFilterName"/>.
    /// </summary>
    public ExchangeableRuntimeFilter RuntimeFilter => _typeFilter;
}

