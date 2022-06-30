using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Generic directory artifact that uses a <see cref="SignatureFileName"/>
    /// file in the directory to associate the signature.
    /// </summary>
    public class GeneratedDirectoryArtifact : IGeneratedArtifact
    {
        public const string SignatureFileName = "signature.directory.txt";

        public GeneratedDirectoryArtifact( NormalizedPath path )
        {
            Path = path;
        }

        /// <summary>
        /// Gets the path of the artifact.
        /// </summary>
        public NormalizedPath Path { get; }

        bool IGeneratedArtifact.IsDirectory => true;

        /// <summary>
        /// Gets whether <see cref="Directory.Exists(string?)"/>.
        /// </summary>
        /// <returns>True if this directory exists, false otherwise.</returns>
        public virtual bool Exists() => !Path.IsEmptyPath && Directory.Exists( Path );

        /// <summary>
        /// Writes a SHA1 signature for this directory.
        /// Nothing is done if <see cref="Path"/>.<see cref="NormalizedPath.IsEmptyPath">IsEmptyPath</see>
        /// </para>
        /// </summary>
        /// <param name="signature">The signature to write.</param>
        public void CreateOrUpdateSignatureFile( SHA1Value signature )
        {
            if( !Path.IsEmptyPath ) File.WriteAllText( Path.AppendPart( SignatureFileName ), signature.ToString() );
        }

        /// <summary>
        /// Tries to extract a SHA1 signature for this artifact.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns><see cref="SHA1Value.Zero"/> if not found.</returns>
        public SHA1Value GetSignature( IActivityMonitor monitor )
        {
            return Exists() ? DoGetSignature( monitor ) : SHA1Value.Zero;
        }

        /// <summary>
        /// Tries to extract a SHA1 signature from an existing directory: the <see cref="SignatureFileName"/> is read if it exists.
        /// <para>
        /// This can be overridden to detect the signature differently.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns><see cref="SHA1Value.Zero"/> if not found.</returns>
        protected virtual SHA1Value DoGetSignature( IActivityMonitor monitor )
        {
            var f = Path.AppendPart( SignatureFileName );
            if( File.Exists( f ) && SHA1Value.TryParse( File.ReadAllText( f ), out var signature ) )
                return signature;
            return SHA1Value.Zero;
        }

        /// <summary>
        /// Overridden to return the <see cref="Path"/>.
        /// </summary>
        /// <returns>This Path.</returns>
        public override string ToString() => Path;

    }
}
