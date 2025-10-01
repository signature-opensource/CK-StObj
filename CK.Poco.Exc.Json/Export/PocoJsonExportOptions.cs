using CK.Core;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CK.Poco.Exc.Json;

/// <summary>
/// Describes immutable dynamic serialization options. Two default singletons are available:
/// <list type="bullet">
/// <item><term><see cref="Default"/></term><description>Use "AllExchangeable" <see cref="TypeFilterName"/> and standard Json options.</description></item>
/// <item><term><see cref="ToStringDefault"/></term><description>Use "AllSerialisable" TypeFilterName and relaxed (less safe) Json options.</description></item>
/// </list>
/// </summary>
public sealed class PocoJsonExportOptions
{
    /// <summary>
    /// Gets a singleton default option.
    /// <list type="bullet">
    ///     <item>Property name are written in camelCase (<see cref="UseCamelCase"/> is true).</item>
    ///     <item>Json is compact (<see cref="JsonWriterOptions.Indented"/> is false).</item>
    ///     <item>The maximal Json depth is 1000.</item>
    ///     <item>The <see cref="JsonWriterOptions.Encoder"/> is null (uses the <see cref="JavaScriptEncoder.Default"/>).</item>
    ///     <item><see cref="JsonWriterOptions.SkipValidation"/> is true.</item>
    ///     <item>The default type filter is "AllExchangeable".</item>
    ///     <item><see cref="UserMessageFormat"/> is <see cref="UserMessageSimplifiedFormat.None"/> (full message).</item>
    /// </list>
    /// </summary>
    public static readonly PocoJsonExportOptions Default = new PocoJsonExportOptions();

    /// <summary>
    /// Gets a singleton default option that is used by IPoco ToString implementation.
    /// Property names are written as-is (<see cref="UseCamelCase"/> is false), <see cref="UserMessage"/> are fully written,
    /// <see cref="TypeLess"/> is false, <see cref="TypeFilterName"/> is "AllSerializable" and <see cref="JsonWriterOptions.Encoder"/>
    /// is <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> and <see cref="JsonWriterOptions.SkipValidation"/> is true.
    /// </summary>
    public static readonly PocoJsonExportOptions ToStringDefault = new PocoJsonExportOptions()
    {
        UseCamelCase = false,
        TypeFilterName = "AllSerializable",
        WriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, SkipValidation = true },
    };

    /// <summary>
    /// Initializes new options.
    /// </summary>
    public PocoJsonExportOptions()
    {
        UseCamelCase = true;
        TypeFilterName = "AllExchangeable";
#if DEBUG
        WriterOptions = new JsonWriterOptions();
#else
        WriterOptions = new JsonWriterOptions() { SkipValidation = true };
#endif
    }

    /// <summary>
    /// Copy constructor. Properties can then be initialized.
    /// </summary>
    /// <param name="o">Options to copy.</param>
    public PocoJsonExportOptions( PocoJsonExportOptions o )
    {
        UseCamelCase = o.UseCamelCase;
        TypeLess = o.TypeLess;
        UserMessageFormat = o.UserMessageFormat;
        WriterOptions = o.WriterOptions;
        TypeFilterName = o.TypeFilterName;
    }

    /// <summary>
    /// Gets or initializes whether camelCasing must be used for property names.
    /// Defaults to true.
    /// </summary>
    public bool UseCamelCase { get; init; }

    /// <summary>
    /// Gets whether type names should never be written.
    /// Defaults to false: when ambiguous, the type is written via a 2-cells array <c>["type name", &lt;value...&gt;]</c>.
    /// </summary>
    public bool TypeLess { get; init; }

    /// <summary>
    /// Gets how <see cref="UserMessage"/> must be exported.
    /// </summary>
    public UserMessageSimplifiedFormat UserMessageFormat { get; init; }

    /// <summary>
    /// Get the writer options. See <see cref="Default"/>.
    /// </summary>
    public JsonWriterOptions WriterOptions { get; init; }

    /// <summary>
    /// Gets the name of the type filter to use.
    /// Defaults to "AllExchangeable".
    /// </summary>
    public string TypeFilterName { get; init; }


}
