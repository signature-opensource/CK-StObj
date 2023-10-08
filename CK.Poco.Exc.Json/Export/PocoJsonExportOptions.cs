using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CK.Poco.Exc.Json
{
    /// <summary>
    /// Describes immutable dynamic serialization options.
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
        /// </list>
        /// </summary>
        public static readonly PocoJsonExportOptions Default = new PocoJsonExportOptions();

        /// <summary>
        /// Gets a singleton default option that is used by IPoco ToString implementation.
        /// It is the same as <see cref="Default"/> except that names are written as-is (<see cref="UseCamelCase"/> is false).
        /// </summary>
        public static readonly PocoJsonExportOptions ToStringDefault = new PocoJsonExportOptions() { UseCamelCase = false };

        /// <summary>
        /// Initializes new options.
        /// </summary>
        public PocoJsonExportOptions()
        {
            UseCamelCase = true;
#if DEBUG
            WriterOptions = new JsonWriterOptions();
#else
            WriterOptions = new JsonWriterOptions() { SkipValidation = true };
#endif
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
        /// Get the writer options. See <see cref="Default"/>.
        /// </summary>
        public JsonWriterOptions WriterOptions { get; init; }

    }
}
