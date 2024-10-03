using CK.Core;
using System;
using System.Text.Json;

namespace CK.Poco.Exc.Json;

/// <summary>
/// Describes dynamic deserialization options.
/// </summary>
public sealed class PocoJsonImportOptions : IMCDeserializationOptions 
{
    /// <summary>
    /// Gets a singleton default option:
    /// <list type="bullet">
    ///     <item>Trailing commas are allowed.</item>
    ///     <item>Json comments are silently skipped.</item>
    ///     <item>The maximal Json depth is 64.</item>
    ///     <item>The type filter name is "AllExchangeable".</item>
    ///     <item>Cultures are not automatically created. Unknown cutures default to <see cref="NormalizedCultureInfo.CodeDefault"/>.</item>
    /// </list>
    /// </summary>
    public static readonly PocoJsonImportOptions Default = new PocoJsonImportOptions();

    /// <summary>
    /// Gets a singleton option that mirrors <see cref="PocoJsonExportOptions.ToStringDefault"/>:
    /// <list type="bullet">
    ///     <item>Trailing commas are allowed.</item>
    ///     <item>Json comments are silently skipped.</item>
    ///     <item>The maximal Json depth is 1000.</item>
    ///     <item>The type filter name is "AllSerializable".</item>
    ///     <item>Cultures are automatically created if needed.</item>
    /// </list>
    /// </summary>
    public static readonly PocoJsonImportOptions ToStringDefault = new PocoJsonImportOptions()
    {
        ReaderOptions = new JsonReaderOptions()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 1000,
        },
        TypeFilterName = "AllSerializable",
        CreateUnexistingCultures = true
    };

    /// <summary>
    /// Initializes a new option.
    /// </summary>
    public PocoJsonImportOptions()
    {
        TypeFilterName = "AllExchangeable";
        ReaderOptions = new JsonReaderOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    }

    /// <summary>
    /// Get the <see cref="Utf8JsonReader"/> options.
    /// </summary>
    public JsonReaderOptions ReaderOptions { get; init; }

    /// <summary>
    /// Gets the name of the type filter to use.
    /// Defaults to "AllExchangeable".
    /// </summary>
    public string TypeFilterName { get; init; }

    /// <inheritdoc />
    public bool CreateUnexistingCultures { get; init; }

    /// <inheritdoc />
    public NormalizedCultureInfo? DefaultCulture { get; init; }
}
