using System.Collections.Generic;
using System.Text;
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
        /// </summary>
        public readonly PocoJsonExportOptions Default = new PocoJsonExportOptions();

        /// <summary>
        /// Initializes new options.
        /// </summary>
        public PocoJsonExportOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }

        /// <summary>
        /// Gets or initializes the policy used to convert a property's name on an object to another format,
        /// such as camel-casing provided by <see cref="JsonNamingPolicy.CamelCase"/> that is the default.
        /// <para>
        /// Initializing to null, suppresses any conversion.
        /// </para>
        /// </summary>
        public JsonNamingPolicy? PropertyNamingPolicy { get; init; }

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
        public bool MapNumericsToNumberAndBigInt { get; init; }

    }
}
