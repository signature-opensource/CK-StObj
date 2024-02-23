using CK.Core;
using CK.Poco.Exc.Json;
using System;
using System.Reflection;
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
        readonly ExchangeableRuntimeFilter _typeFilter;
        readonly PocoJsonImportOptions _options;

        /// <summary>
        /// Initialize a new reader context.
        /// </summary>
        /// <param name="pocoDirectory">The <see cref="PocoDirectory"/>.</param>
        /// <param name="options">Options to use. Defaults to <see cref="PocoJsonImportOptions.Default"/></param>
        /// <param name="inner">Optional wrapped context.</param>
        public PocoJsonReadContext( PocoDirectory pocoDirectory, PocoJsonImportOptions? options = null, IUtf8JsonReaderContext? inner = null )
        {
            Throw.CheckNotNullArgument( pocoDirectory );
            options ??= PocoJsonImportOptions.Default;
            _inner = inner;
            _typeFilter = ((IPocoDirectoryExchangeGenerated)pocoDirectory).GetRuntimeFilter( options.TypeFilterName );
            _options = options;
        }

        /// <summary>
        /// Gets the options.
        /// </summary>
        public PocoJsonImportOptions Options => _options;

        /// <summary>
        /// Disposes this context (disposing the <see cref="IUtf8JsonReaderContext"/> if
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

        /// <summary>
        /// Gets whether a type can be imported or not depending on <see cref="PocoJsonImportOptions.TypeFilterName"/>.
        /// <para>
        /// This is not intended to be used directly: this is used by the deserialization generated code.
        /// </para>
        /// </summary>
        /// <param name="index">The type index.</param>
        /// <returns>True if the type can be imported, false otherwise.</returns>
        public bool CanImport( int index ) => (_typeFilter.Flags[index >> 5] & (1 << index)) != 0;

    }
}

