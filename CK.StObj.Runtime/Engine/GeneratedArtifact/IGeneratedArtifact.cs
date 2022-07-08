using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Generalization of an artifact produced that can be a file or directory.
    /// </summary>
    public interface IGeneratedArtifact
    {
        /// <summary>
        /// Gets the path of the artifact.
        /// </summary>
        NormalizedPath Path { get; }

        /// <summary>
        /// Gets whether the artifact is a directory or a file.
        /// </summary>
        bool IsDirectory { get; }

        /// <summary>
        /// Gets whether the directory or file exists.
        /// </summary>
        /// <returns>True if this artifact exists, false otherwise.</returns>
        bool Exists();

        /// <summary>
        /// Tries to extract a SHA1 signature for this artifact.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns><see cref="SHA1Value.Zero"/> if not found.</returns>
        SHA1Value GetSignature( IActivityMonitor monitor );

        /// <summary>
        /// Tries to copy a source file or directory content to <see cref="Path"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="source">The source file or directory path to copy.</param>
        /// <returns>True on success, false on error.</returns>
        bool CopyFrom( IActivityMonitor monitor, NormalizedPath source );

    }
}
