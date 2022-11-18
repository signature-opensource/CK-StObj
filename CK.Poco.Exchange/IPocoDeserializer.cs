using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// Simple Poco deserializer contract.
    /// </summary>
    public interface IPocoDeserializer
    {
        /// <summary>
        /// Synchronous deserialization.
        /// </summary>
        /// <param name="monitor">The monitor that may be used.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="data">The read Poco. May be null.</param>
        /// <returns>True on success, false on error.</returns>
        bool TryRead( IActivityMonitor monitor, Stream input, out IPoco? data );

        /// <summary>
        /// Asynchronous deserialization.
        /// </summary>
        /// <param name="monitor">The monitor that may be used.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="cancel">Optional cancellation token.</param>
        /// <returns>Success and deserialized data.</returns>
        Task<(bool Success, IPoco? Data)> TryReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel );
    }
}
