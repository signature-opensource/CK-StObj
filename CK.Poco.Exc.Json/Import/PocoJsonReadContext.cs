using CK.Core;
using CK.Poco.Exc.Json;
using System;
using System.Text.Json;

namespace CK.Poco.Exc.Json
{
    /// <summary>
    /// Context object that is provided to all the read methods.
    /// <para>
    /// This context must be disposed once done with it.
    /// </para>
    /// </summary>
    public sealed class PocoJsonReadContext : IUtf8JsonReaderDataProvider, IDisposable
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

        /// <summary>
        /// Disposes this context (disposing the <see cref="IUtf8JsonReaderDataProvider"/> if
        /// any and if it is disposable).
        /// </summary>
        public void Dispose()
        {
            if( _dataProvider is IDisposable d ) d.Dispose();
        }

        /// <inheritdoc />
        public void ReadMoreData( ref Utf8JsonReader reader ) => _dataProvider?.ReadMoreData( ref reader );

        /// <inheritdoc />
        public void SkipMoreData( ref Utf8JsonReader reader ) => _dataProvider?.SkipMoreData( ref reader );
    }
}

