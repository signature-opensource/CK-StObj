using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CK.Poco.Exc.Json.Export
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
        ///     <item><see cref="UseSimplifiedTypes"/> is false.</item>
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
            WriterOptions = new JsonWriterOptions();
#if !DEBUG
            WriterOptions.SkipValidation = true;
#endif
        }

        /// <summary>
        /// Gets or initializes whether camelCasing must be used for property names.
        /// Defaults to true.
        /// </summary>
        public bool UseCamelCase { get; init; }

        /// <summary>
        /// Gets or initializes whether numeric type names should keep their "real" types or be simplified as "Number" and "BigInt".
        /// <para>
        /// When false (the default), float, single, small integers up to the Int32 are simply numbers but long (Int64), ulong (UInt64) and Decimal
        /// are expressed as strings. Their type names are based on their C# actual type: "byte", "sbyte", "short", "ulong", etc.
        /// </para>
        /// <para>
        /// When true, two purely client types that are "Number" and "BigInt". The float, single, small integers up to the Int32 are 
        /// exported as "Number" and big integers (long, ulong, decimal, BigIntegers) are exported as "BigInt".
        /// </para>
        /// </summary>
        public bool UseSimplifiedTypes { get; init; }

        /// <summary>
        /// Get the writer options. See <see cref="Default"/>.
        /// </summary>
        public JsonWriterOptions WriterOptions { get; init; }

    }
}
