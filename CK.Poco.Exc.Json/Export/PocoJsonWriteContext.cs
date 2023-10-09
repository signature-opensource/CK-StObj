using CK.Core;
using CK.Poco.Exc.Json;
using System;
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

        /// <summary>
        /// Initialize a new writer context.
        /// </summary>
        /// <param name="options">Options to use. Defaults to <see cref="PocoJsonExportOptions.Default"/></param>
        public PocoJsonWriteContext( PocoJsonExportOptions? options = null )
        {
            _options = options ?? PocoJsonExportOptions.Default;
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

