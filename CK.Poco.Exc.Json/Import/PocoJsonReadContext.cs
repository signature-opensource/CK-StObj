using CK.Core;
using CK.Poco.Exc.Json;
using System.Text.Json;

namespace CK.Poco.Exc.Json
{
    /// <summary>
    /// Context object that is provided to all the read methods.
    /// </summary>
    public sealed class PocoJsonReadContext : IUtf8JsonReaderDataProvider
    {
        readonly IUtf8JsonReaderDataProvider? _dataProvider;
        readonly PocoJsonImportOptions _options;

        /// <summary>
        /// Initialize a new reader context.
        /// </summary>
        /// <param name="options">Options to use. Defaults to <see cref="PocoJsonImportOptions.Default"/></param>
        /// <param name="dataProvider">Optional data provider.</param>
        public PocoJsonReadContext( PocoJsonImportOptions? options = null, IUtf8JsonReaderDataProvider? dataProvider = null )
        {
            _dataProvider = dataProvider;
            _options = options ?? PocoJsonImportOptions.Default;
        }

        /// <summary>
        /// Gets the options.
        /// </summary>
        public PocoJsonImportOptions Options => _options;


        /// <inheritdoc />
        public void NeedMoreData( ref Utf8JsonReader reader, bool skip = false ) => _dataProvider?.NeedMoreData( ref reader, skip );
    }
}

