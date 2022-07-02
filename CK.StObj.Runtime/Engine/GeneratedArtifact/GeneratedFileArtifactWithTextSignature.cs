using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Implements any file that uses a companion file with a ".signature.txt" suffix to store the signature.
    /// </summary>
    public sealed class GeneratedFileArtifactWithTextSignature : GeneratedFileArtifact
    {
        /// <summary>
        /// Suffix of the companion signature file.
        /// </summary>
        public const string SuffixSignature = ".signature.txt";

        readonly NormalizedPath _signatureFile;

        /// <summary>
        /// Initializes a new <see cref="GeneratedFileArtifactWithTextSignature"/>.
        /// </summary>
        /// <param name="filePath">File path. It MUST not be <see cref="NormalizedPath.IsEmptyPath"/> otherwise an <see cref="ArgumentException"/> is thrown.</param>
        public GeneratedFileArtifactWithTextSignature( NormalizedPath filePath )
            : base( filePath )
        {
            Throw.CheckArgument( !filePath.IsEmptyPath );
            _signatureFile = filePath.RemoveLastPart().AppendPart( filePath.LastPart + SuffixSignature );
        }

        /// <summary>
        /// Overridden to check that both the file <see cref="Path"/> and the signature file exist.
        /// </summary>
        /// <returns>True if both file and signature exist.</returns>
        public override bool Exists()
        {
            return base.Exists() && File.Exists( _signatureFile );
        }

        /// <summary>
        /// Writes the SHA1 file signature for this file.
        /// </summary>
        /// <param name="signature">The signature to write.</param>
        public void CreateOrUpdateSignatureFile( SHA1Value signature )
        {
            File.WriteAllText( _signatureFile, signature.ToString() );
        }

        /// <summary>
        /// Overridden to read the signature file.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The signature or <see cref="SHA1Value.Zero"/> if it can't be read.</returns>
        protected override SHA1Value DoGetSignature( IActivityMonitor monitor )
        {
            var firstLine = SafeReadFirstLine( monitor, _signatureFile );
            if( firstLine != null && SHA1Value.TryParse( firstLine, out var signature ) )
            {
                return signature;
            }
            return SHA1Value.Zero;
        }

    }
}
