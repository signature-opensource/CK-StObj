using CK.Core;
using CK.Poco.Exc.Json;
using System;
using System.Reflection;
using System.Text.Json;

namespace CK.Poco.Exc.Json
{
    /// <summary>
    /// Context object that is provided to all the write methods.
    /// <para>
    /// This context must be disposed once done with it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This currently only holds the <see cref="Options"/>. This is an extension point.
    /// </remarks>
    public sealed class PocoJsonWriteContext : IDisposable
    {
        readonly PocoJsonExportOptions _options;
        readonly ExchangeableRuntimeFilter _typeFilter;

        /// <summary>
        /// Initialize a new writer context.
        /// </summary>
        /// <param name="pocoDirectory">The <see cref="PocoDirectory"/>.</param>
        /// <param name="options">Options to use. Defaults to <see cref="PocoJsonExportOptions.Default"/></param>
        public PocoJsonWriteContext( PocoDirectory pocoDirectory, PocoJsonExportOptions? options = null )
        {
            Throw.CheckNotNullArgument( pocoDirectory );
            options ??= PocoJsonExportOptions.Default;
            _typeFilter = ((IPocoDirectoryExchangeGenerated)pocoDirectory).GetRuntimeFilter( options.TypeFilterName );
            _options = options;
        }

        /// <summary>
        /// Gets the options.
        /// </summary>
        public PocoJsonExportOptions Options => _options;

        /// <summary>
        /// Disposes this context.
        /// </summary>
        public void Dispose()
        {
        }
    }
}

