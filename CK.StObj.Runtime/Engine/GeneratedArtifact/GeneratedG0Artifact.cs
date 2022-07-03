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
    /// Implements the G0.cs source code file.
    /// The signature is read from [assembly: CK.StObj.Signature( "..." )] SHA1 attribute in the first line.
    /// </summary>
    public sealed class GeneratedG0Artifact : GeneratedFileArtifact
    {

        /// <summary>
        /// Initializes a new <see cref="GeneratedG0Artifact"/>.
        /// </summary>
        /// <param name="filePath">File path. It MUST not be <see cref="NormalizedPath.IsEmptyPath"/> otherwise an <see cref="ArgumentException"/> is thrown.</param>
        public GeneratedG0Artifact( NormalizedPath filePath )
            : base( filePath )
        {
        }

        /// <summary>
        /// Overridden to match [assembly: CK.StObj.Signature( "..." )] SHA1 attribute in the first line.
        /// </summary>
        /// <param name="monitor"></param>
        /// <returns></returns>
        protected override SHA1Value DoGetSignature( IActivityMonitor monitor )
        {
            var firstLine = SafeReadFirstLine( monitor, Path );
            if( firstLine != null )
            {
                var m = Regex.Match( firstLine, @"\s*\[\s*assembly\s*:\s*CK.StObj.Signature\s*\(\s*@?""(?<1>.*?)""" );
                if( m.Success && SHA1Value.TryParse( m.Groups[1].Value, out var signature ) )
                {
                    return signature;
                }
            }
            monitor.Warn( $"Unable to read [assembly: CK.StObj.Signature( \"...\" )] attribute from '{Path}'." );
            return SHA1Value.Zero;
        }

    }
}