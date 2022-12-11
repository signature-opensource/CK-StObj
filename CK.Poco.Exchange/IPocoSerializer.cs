using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Simple Poco serializer contract.
    /// </summary>
    public interface IPocoSerializer
    {
       /// <summary>
        /// Synchronous serialization (throws on error).
        /// </summary>
        /// <param name="monitor">The monitor that may be used.</param>
        /// <param name="output">The output stream.</param>
        /// <param name="data">The Poco instance to serialize. May be null.</param>
        void Write( IActivityMonitor monitor, Stream output, IPoco? data );

        /// <summary>
        /// Asynchronous serialization (throws on error).
        /// </summary>
        /// <param name="monitor">The monitor that may be used.</param>
        /// <param name="output">The output stream.</param>
        /// <param name="data">The Poco instance to serialize. May be null.</param>
        /// <param name="cancel">Optional cancellation token.</param>
        /// <returns>The awaitable.</returns>
        Task WriteAsync( IActivityMonitor monitor, Stream output, IPoco? data, CancellationToken cancel = default );
    }
}
