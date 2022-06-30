using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    public class GeneratedFileArtifact : IGeneratedArtifact
    {
        public GeneratedFileArtifact( NormalizedPath path )
        {
            Path = path;
        }

        /// <inheritdoc />
        public NormalizedPath Path { get; }

        bool IGeneratedArtifact.IsDirectory => false;

        /// <summary>
        /// Gets whether <see cref="File.Exists(string?)"/>.
        /// </summary>
        /// <returns>True if this file exists, false otherwise.</returns>
        public virtual bool Exists() => !Path.IsEmptyPath && File.Exists( Path );

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
        /// Tries to extract a SHA1 signature from a file: the first valid SHA value that appears in the <see cref="NormalizedPath.Parts"/> (split by dot '.')
        /// form the end to start of the <see cref="Path"/>.
        /// <para>
        /// This can be overridden to detect the signature differently.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns><see cref="SHA1Value.Zero"/> if not found.</returns>
        protected virtual SHA1Value DoGetSignature( IActivityMonitor monitor )
        {
            foreach( var part in Path.Parts.Reverse() )
            {
                foreach( var p in part.Split( '.' ).Reverse() )
                {
                    if( SHA1Value.TryParse( p, out var signature ) )
                        return signature;
                }
            }
            return SHA1Value.Zero;
        }

        public static string? SafeReadFirstLine( IActivityMonitor monitor, NormalizedPath path )
        {
            try
            {
                using( var s = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
                using( var r = new StreamReader( s ) )
                {
                    return r.ReadLine();
                }
            }
            catch( Exception ex )
            {
                monitor.Warn( $"Unable to read the first line from '{path}'.", ex );
            }
            return null;
        }

        /// <summary>
        /// Overridden to return the <see cref="Path"/>.
        /// </summary>
        /// <returns>This Path.</returns>
        public override string ToString() => Path;

    }
}
