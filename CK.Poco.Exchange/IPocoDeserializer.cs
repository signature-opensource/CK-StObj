using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// Simple Poco deserializer contract.
/// </summary>
public interface IPocoDeserializer
{
    /// <summary>
    /// Synchronous deserialization.
    /// </summary>
    /// <param name="monitor">The monitor that may be used.</param>
    /// <param name="input">The input bytes.</param>
    /// <param name="data">The read Poco. May be null.</param>
    /// <returns>True on success, false on error.</returns>
    bool TryRead( IActivityMonitor monitor, ReadOnlySequence<byte> input, out IPoco? data );

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

    /// <summary>
    /// Synchronous deserialization that throws on error.
    /// </summary>
    /// <param name="monitor">The monitor that may be used.</param>
    /// <param name="input">The input stream.</param>
    /// <returns>the deserialized IPoco (that can be null).</returns>
    IPoco? Read( IActivityMonitor monitor, Stream input );

    /// <summary>
    /// Asynchronous deserialization that throws on error.
    /// </summary>
    /// <param name="monitor">The monitor that may be used.</param>
    /// <param name="input">The input stream.</param>
    /// <param name="cancel">Optional cancellation token.</param>
    /// <returns>the deserialized IPoco (that can be null).</returns>
    Task<IPoco?> ReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel );
}
