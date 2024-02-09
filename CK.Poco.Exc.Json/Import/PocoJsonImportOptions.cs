using CK.Core;
using System;
using System.Text.Json;

namespace CK.Poco.Exc.Json
{
    /// <summary>
    /// Describes dynamic deserialization options.
    /// </summary>
    public sealed class PocoJsonImportOptions
    {
        /// <summary>
        /// Gets a singleton default option:
        /// <list type="bullet">
        ///     <item>Trailing commas are allowed.</item>
        ///     <item>Json comments are silently skipped.</item>
        ///     <item>The maximal Json depth is 64.</item>
        ///     <item>The default type filter is "AllExchangeable".</item>
        /// </list>
        /// </summary>
        public static readonly PocoJsonImportOptions Default = new PocoJsonImportOptions();

        /// <summary>
        /// Initializes a new option.
        /// </summary>
        public PocoJsonImportOptions()
        {
            TypeFilterName = "AllExchangeable";
            ReaderOptions = new JsonReaderOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        }

        /// <summary>
        /// Get the reader options. See <see cref="Default"/>.
        /// </summary>
        public JsonReaderOptions ReaderOptions { get; init; }

        /// <summary>
        /// Gets the name of the type filter to use.
        /// Defaults to "AllExchangeable".
        /// </summary>
        public string TypeFilterName { get; init; }
    }
}
