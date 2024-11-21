using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup;

/// <summary>
/// Simple file artifact where the SHA1 should appear in the file path.
/// <see cref="DoGetSignature(IActivityMonitor)"/> can be overridden.
/// </summary>
public class GeneratedFileArtifact : IGeneratedArtifact
{
    /// <summary>
    /// Initializes a new <see cref="GeneratedFileArtifact"/>.
    /// </summary>
    /// <param name="filePath">File path. It MUST not be <see cref="NormalizedPath.IsEmptyPath"/> otherwise an <see cref="ArgumentException"/> is thrown.</param>
    public GeneratedFileArtifact( NormalizedPath filePath )
    {
        Throw.CheckArgument( !filePath.IsEmptyPath );
        Path = filePath;
    }

    /// <inheritdoc />
    public NormalizedPath Path { get; }

    /// <summary>
    /// Gets whether <see cref="File.Exists(string?)"/>.
    /// </summary>
    /// <returns>True if this file exists, false otherwise.</returns>
    public virtual bool Exists() => !Path.IsEmptyPath && File.Exists( Path );

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

    /// <summary>
    /// Helper that reads the first line of a text file.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="path">The file path.</param>
    /// <returns>The first line or null on error.</returns>
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
    /// Tries to set the content of the file.
    /// This can be overridden if needed.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="content">The file content.</param>
    /// <returns>True on success, false on error.</returns>
    public virtual bool CreateOrUpdate( IActivityMonitor monitor, string content )
    {
        return PrepareWrite( monitor ) && SafeWrite( monitor, Path, content );
    }

    /// <summary>
    /// Tries to copy a source file to <see cref="Path"/>.
    /// This can be overridden if needed.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="source">The source file to copy.</param>
    /// <returns>True on success, false on error.</returns>
    public virtual bool UpdateFrom( IActivityMonitor monitor, NormalizedPath source )
    {
        return PrepareWrite( monitor ) && SafeCopy( monitor, source, Path );
    }

    /// <summary>
    /// Helper that writes a file with retries.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="path">The file path.</param>
    /// <param name="content">The file content.</param>
    /// <param name="name">Name of the file for logs.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool SafeWrite( IActivityMonitor monitor, NormalizedPath path, string content, string name = "source code" )
    {
        int tryCount = 0;
        retry:
        try
        {
            File.WriteAllText( path, content );
        }
        catch( Exception ex )
        {
            if( ++tryCount > 5 )
            {
                monitor.Error( $"Failed to write {name} to '{path}' after 5 tries.", ex );
                return false;
            }
            monitor.Warn( $"Error while writing {name}. Retrying in {tryCount * 50} ms.", ex );
            System.Threading.Thread.Sleep( tryCount * 50 );
            goto retry;
        }
        monitor.Info( $"Saved file: {path}." );
        return true;
    }

    /// <summary>
    /// Helper that copies a file with retries.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="source">The source file path.</param>
    /// <param name="target">The target file path.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool SafeCopy( IActivityMonitor monitor, NormalizedPath source, NormalizedPath target )
    {
        int tryCount = 0;
        retry:
        try
        {
            File.Copy( source, target, true );
        }
        catch( FileNotFoundException ex )
        {
            monitor.Error( $"Unable to copy file: source '{source}' not found.", ex );
            return false;
        }
        catch( Exception ex )
        {
            if( ++tryCount > 5 )
            {
                monitor.Error( $"Unable to copy file: '{source}' to '{target}' after 5 tries.", ex );
                return false;
            }
            monitor.Warn( $"Unable to copy file: '{source}' to '{target}'. Retrying in {tryCount * 50} ms.", ex );
            System.Threading.Thread.Sleep( tryCount * 50 );
            goto retry;
        }
        monitor.Info( $"Copied file '{source}' to '{target}'." );
        return true;
    }

    /// <summary>
    /// Called before copying, creating or updating the generated artifact.
    /// Enables artifacts to ensure that any requirements are met.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false on error.</returns>
    protected virtual bool PrepareWrite( IActivityMonitor monitor )
    {
        return true;
    }

    /// <summary>
    /// Overridden to return the <see cref="Path"/>.
    /// </summary>
    /// <returns>This Path.</returns>
    public override string ToString() => Path;

}
