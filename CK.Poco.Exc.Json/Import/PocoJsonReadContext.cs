using CK.Core;
using System.Text.Json;

namespace CK.Poco.Exc.Json;

/// <summary>
/// Context object that is provided to all the read methods.
/// <para>
/// This context must be disposed once done with it.
/// </para>
/// </summary>
public sealed class PocoJsonReadContext : IUtf8JsonReaderContext, IMCDeserializationOptions
{
    readonly IUtf8JsonReaderContext? _inner;
    readonly ExchangeableRuntimeFilter _typeFilter;
    readonly PocoJsonImportOptions _options;

    /// <summary>
    /// Initialize a new reader context.
    /// </summary>
    /// <param name="pocoDirectory">The <see cref="PocoDirectory"/>.</param>
    /// <param name="options">Options to use. Defaults to <see cref="PocoJsonImportOptions.Default"/></param>
    /// <param name="inner">Optional wrapped context.</param>
    public PocoJsonReadContext( PocoDirectory pocoDirectory,
                                PocoJsonImportOptions? options = null,
                                IUtf8JsonReaderContext? inner = null )
    {
        Throw.CheckNotNullArgument( pocoDirectory );
        options ??= PocoJsonImportOptions.Default;
        _inner = inner;
        _typeFilter = ((IPocoDirectoryExchangeGenerated)pocoDirectory).GetRuntimeFilter( options.TypeFilterName );
        _options = options;
    }

    /// <summary>
    /// Gets the options.
    /// </summary>
    public PocoJsonImportOptions Options => _options;

    /// <summary>
    /// Gets the type filter from the <see cref="PocoJsonExportOptions.TypeFilterName"/>.
    /// </summary>
    public ExchangeableRuntimeFilter RuntimeFilter => _typeFilter;

    /// <inheritdoc />
    public bool CreateUnexistingCultures => _options.CreateUnexistingCultures;

    /// <inheritdoc />
    public NormalizedCultureInfo? DefaultCulture => _options.DefaultCulture;

    /// <inheritdoc />
    public void ReadMoreData( ref Utf8JsonReader reader ) => _inner?.ReadMoreData( ref reader );

    /// <inheritdoc />
    public void SkipMoreData( ref Utf8JsonReader reader ) => _inner?.SkipMoreData( ref reader );
}

