using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace CK.Core
{
    /// <summary>
    /// Describes dynamic serialization options.
    /// </summary>
    public class PocoJsonSerializerOptions
    {
        /// <summary>
        /// Gets the default options. Converters must be registered into <see cref="ForJsonSerializer"/>
        /// before the first use (see https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to).
        /// </summary>
        public static readonly PocoJsonSerializerOptions Default = new PocoJsonSerializerOptions();

        /// <summary>
        /// Initializes a new options.
        /// </summary>
        public PocoJsonSerializerOptions()
        {
            ForJsonSerializer = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
        /// <summary>
        /// Gets or sets the serialization mode.
        /// Defaults to <see cref="PocoJsonSerializerMode.ECMAScriptSafe"/>.
        /// </summary>
        public PocoJsonSerializerMode Mode { get; set; }

        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions"/> used to call <see cref="JsonSerializer.Serialize{TValue}(Utf8JsonWriter, TValue, JsonSerializerOptions)"/>
        /// and <see cref="JsonSerializer.Deserialize{TValue}(ref Utf8JsonReader, JsonSerializerOptions)"/> for unknown types.
        /// </summary>
        public JsonSerializerOptions ForJsonSerializer { get; set; }

    }
}
