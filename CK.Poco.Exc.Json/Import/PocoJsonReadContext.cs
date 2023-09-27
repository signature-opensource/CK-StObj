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
    public sealed class PocoJsonReadContext : IUtf8JsonReaderContext, IDisposable
    {
        readonly IUtf8JsonReaderContext? _inner;
        readonly PocoJsonImportOptions _options;

        /// <summary>
        /// Initialize a new reader context.
        /// </summary>
        /// <param name="options">Options to use. Defaults to <see cref="PocoJsonImportOptions.Default"/></param>
        /// <param name="inner">Optional wrapped context.</param>
        public PocoJsonReadContext( PocoJsonImportOptions? options = null, IUtf8JsonReaderContext? inner = null )
        {
            _inner = inner;
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
            if( _inner is IDisposable d ) d.Dispose();
        }

        /// <inheritdoc />
        public void ReadMoreData( ref Utf8JsonReader reader ) => _inner?.ReadMoreData( ref reader );

        /// <inheritdoc />
        public void SkipMoreData( ref Utf8JsonReader reader ) => _inner?.SkipMoreData( ref reader );
    }
}

