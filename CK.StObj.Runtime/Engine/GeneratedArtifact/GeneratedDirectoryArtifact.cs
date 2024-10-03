using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup;

/// <summary>
/// Generic directory artifact that uses a <see cref="SignatureFileName"/>
/// companion file in the directory to associate the signature.
/// </summary>
public class GeneratedDirectoryArtifact : IGeneratedArtifact
{
    /// <summary>
    /// Name of the companion signature file.
    /// </summary>
    public const string SignatureFileName = "signature.directory.txt";

    /// <summary>
    /// Initializes a new generated directory. 
    /// </summary>
    /// <param name="path">Directory path. It MUST not be <see cref="NormalizedPath.IsEmptyPath"/> otherwise an <see cref="ArgumentException"/> is thrown.</param>
    public GeneratedDirectoryArtifact( NormalizedPath path )
    {
        Throw.CheckArgument( !path.IsEmptyPath );
        Path = path;
    }

    /// <summary>
    /// Gets the path of the artifact.
    /// </summary>
    public NormalizedPath Path { get; }

    /// <summary>
    /// Gets whether <see cref="Directory.Exists(string?)"/>.
    /// </summary>
    /// <returns>True if this directory exists, false otherwise.</returns>
    public virtual bool Exists() => Directory.Exists( Path );

    /// <summary>
    /// Writes a SHA1 signature for this directory.
    /// </summary>
    /// <param name="signature">The signature to write.</param>
    public void CreateOrUpdateSignatureFile( SHA1Value signature )
    {
        File.WriteAllText( Path.AppendPart( SignatureFileName ), signature.ToString() );
    }

    /// <summary>
    /// Tries to extract a SHA1 signature for this artifact.
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

    /// <summary>
    /// Copy the content of the source directory to <see cref="Path"/>.
    /// This calls the <see cref="SafeCopy(IActivityMonitor, NormalizedPath, NormalizedPath)"/>
    /// helper but can be overridden if needed.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="source">The source directory.</param>
    /// <returns>True on success, false on error.</returns>
    public virtual bool UpdateFrom( IActivityMonitor monitor, NormalizedPath source )
    {
        return SafeCopy( monitor, source, Path );
    }

    /// <summary>
    /// Helper that copy a directory content to another one with retries.
    /// This first deletes the target directory before copying the content.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="source">The source directory.</param>
    /// <param name="target">The target directory.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool SafeCopy( IActivityMonitor monitor, NormalizedPath source, NormalizedPath target )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckArgument( !target.StartsWith( source ) && !source.StartsWith( target ) );
        int tryCount = 0;
        retry:
        try
        {
            var dS = new DirectoryInfo( source );
            var dT = new DirectoryInfo( target );
            if( !dS.Exists )
            {
                monitor.Error( $"Source directory '{dS.FullName}' not found." );
                return false;
            }
            if( !dT.Exists )
            {
                Directory.CreateDirectory( dT.FullName );
            }
            else
            {
                dT.Delete( recursive: true );
            }
            FileUtil.CopyDirectory( dS, dT );
        }
        catch( Exception ex )
        {
            if( ++tryCount > 5 )
            {
                monitor.Error( $"Failed to copy directory content from {source} to '{target}' after 5 tries.", ex );
                return false;
            }
            monitor.Warn( $"Error while copying directory content. Retrying in {tryCount * 50} ms.", ex );
            System.Threading.Thread.Sleep( tryCount * 50 );
            goto retry;
        }
        monitor.Info( $"Directory content copied from '{source}' to '{target}'." );
        return true;

    }
}
