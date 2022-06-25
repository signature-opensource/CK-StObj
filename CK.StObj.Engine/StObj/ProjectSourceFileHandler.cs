using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Handles source files that have been created in the output path by moving
    /// them to a "$StObjGen" folder in a project path and checking whether the signature (SHA1 at the start of the primary generated
    /// .cs file) match the already existing one: in such case, the available map should be used.
    /// <para>
    /// This class is public since it is directly used by CK.Testing.StObjEngine to update the "$StObjGen" folder of
    /// the TestHelper.ProjectTestFolder.
    /// </para>
    /// </summary>
    public class ProjectSourceFileHandler
    {
        readonly NormalizedPath _originPath;
        readonly NormalizedPath _target;
        readonly IActivityMonitor _monitor;

        /// <summary>
        /// Initializes a new handler.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="originPath">
        /// The output path of the code generation.
        /// </param>
        /// <param name="projectPath">
        /// The target project path into which a "$StObjGen" folder is updated.
        /// </param>
        public ProjectSourceFileHandler( IActivityMonitor monitor,
                                         NormalizedPath originPath,
                                         NormalizedPath projectPath )
        {
            _monitor = monitor;
            _originPath = originPath;
            _target = projectPath.AppendPart( "$StObjGen" );
            Directory.CreateDirectory( _target );
        }

        NormalizedPath GetCSFileTargetPath( int idx ) => _target.AppendPart( "G" + idx + ".cs" );

        /// <summary>
        /// Handles the move of the generated files from the output path to the "$StObjGen" folder.
        /// </summary>
        /// <param name="r">The generation result.</param>
        /// <returns>
        /// True if there is no generated source files or if the primary source file exists and has
        /// the same signature as the generated result. False otherwise.
        /// </returns>
        public bool MoveFilesAndCheckSignature( StObjCollectorResult.CodeGenerateResult r )
        {
            bool modified = false;
            int idxCSFile = 0;
            foreach( var f in r.GeneratedFileNames )
            {
                if( f.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) )
                {
                    bool isPrimaryFile = idxCSFile == 0;
                    var fPath = _originPath.AppendPart( f );
                    var t = GetCSFileTargetPath( idxCSFile++ );
                    if( File.Exists( t ) )
                    {
                        // The primary source file that has the signature is the first one.
                        if( isPrimaryFile )
                        {
                            if( SignatureFileEquals( fPath, t ) )
                            {
                                _monitor.Info( $"File signature matches: no change in primary project source file." );
                            }
                            else
                            {
                                _monitor.Info( $"Primary project source file changed." );
                                modified = true;
                            }
                            // The signature has matched.
                            // We skip the move in such case since we want the embedded source files that already exist to
                            // actually be editable by the developer (Ctrl+K,D for a simple reformatting
                            // and/or manual fixes to fix the generated code.
                            // Note that we are leaving the original sources in the working folder.
                            if( !modified ) continue;
                        }
                    }
                    else
                    {
                        _monitor.Trace( $"New project source file: '{t.LastPart}'." );
                    }
                    DoMoveOrCopy( _monitor, fPath, t, copy: false );
                }
                else if( f.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase )
                         || f.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase )
                         || f.EndsWith( StObjEngineConfiguration.ExistsSignatureFileExtension, StringComparison.OrdinalIgnoreCase ) ) continue;
                else
                {
                    // Other generated files?
                    // Like embedded resources or others... May be one day.
                    throw new NotImplementedException( $"Other generated files that .cs are not yet handled: {f}" );
                }
            }
            // Cleaning "G{X}.cs" files only if at least one C# file has been generated:
            // On error or if no files have been generated (typically because of a successful available
            // StObjMap match) we let ALL the existing files as-is.
            if( idxCSFile > 0 )
            {
                // Implemented cleaning here is currently useless since there are no G{X}.cs where X > 0.
                for(; ; )
                {
                    var previous = GetCSFileTargetPath( idxCSFile++ );
                    if( File.Exists( previous ) )
                    {
                        using( _monitor.OpenTrace( $"Deleting old project source file '{previous.LastPart}'." ) )
                        {
                            SafeDelete( _monitor, previous );
                        }
                    }
                    else break;
                }
            }
            return !modified;
        }

        internal static void DoMoveOrCopy( IActivityMonitor m, NormalizedPath f, NormalizedPath t, bool copy )
        {
            int retryCount = 0;
            retry:
            try
            {
                if( f != t.LastPart )
                {
                    m.Info( $"{(copy ? "Copy" : "Mov")}ing generated file: '{f.LastPart}' to '{t}'." );
                }
                else
                {
                    m.Info( $"{(copy ? "Copy" : "Mov")}ing generated file: '{t}'." );
                }
                if( copy ) File.Copy( f, t, true );
                else File.Move( f, t, true );
            }
            catch( Exception ex )
            {
                if( retryCount++ < 3 )
                {
                    m.Warn( $"Failed to {(copy ? "copy" : "move")} project source file: '{f.LastPart}'. Retrying.", ex );
                    Thread.Sleep( retryCount * 50 );
                    goto retry;
                }
                m.Error( $"Failed to {(copy ? "copy" : "move")} project source file: '{f.LastPart}'. Rethrowing.", ex );
                throw;
            }
        }


        internal static void SafeDelete( IActivityMonitor monitor, NormalizedPath filePath )
        {
            try
            {
                File.Delete( filePath );
            }
            catch( Exception ex )
            {
                monitor.Error( $"Error while deleting file '{filePath}'. Ignored.", ex );
            }
        }

        bool SignatureFileEquals( string f, string t )
        {
            // 80 bytes covers the [assembly: CK.StObj.Signature( "..." )] SHA1 attribute.
            // We want the file to be modified and keep its signature during debug/development sessions.
            // The test below ignores the white spaces.
            Span<byte> b1 = stackalloc byte[80];
            Span<byte> b2 = stackalloc byte[80];
            using( var f1 = new FileStream( f, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan ) )
            {
                f1.Read( b1 );
            }
            using( var f2 = new FileStream( t, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan ) )
            {
                f2.Read( b2 );
            }
            int i1 = 0, i2 = 0;
            while( i1 < b1.Length && i2 < b2.Length )
            {
                byte v = b1[i1];
                if( v == b2[i2] )
                {
                    if( v == ']' ) return true;
                    i1++;
                    i2++;
                }
                else
                {
                    if( v == ' ' ) i1++;
                    else if( b2[i2] == ' ' ) i2++;
                    else return false;
                }
            }
            return false;
        }
    }

}
