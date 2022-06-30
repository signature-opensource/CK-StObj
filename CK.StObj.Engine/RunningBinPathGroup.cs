using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements <see cref="IRunningBinPathGroup"/>.
    /// </summary>
    public sealed class RunningBinPathGroup : IRunningBinPathGroup
    {
        readonly string _generatedAssemblyName;
        readonly string _names;

        internal RunningBinPathGroup( string generatedAssemblyName, BinPathConfiguration head, BinPathConfiguration[] similars, SHA1Value sha )
        {
            Debug.Assert( generatedAssemblyName != null && generatedAssemblyName.StartsWith( StObjContextRoot.GeneratedAssemblyName ) );
            Debug.Assert( similars != null && similars.Length > 0 && similars[0] == head );
            Debug.Assert( !sha.IsZero && sha != SHA1Value.Zero );
            _generatedAssemblyName = generatedAssemblyName;
            Configuration = head;
            SimilarConfigurations = similars;
            SignatureCode = sha;
            GeneratedSource = CreateG0( head );
            GeneratedAssembly = CreateAssembly( head );
            _names = similars.Select( c => c.Name ).Concatenate();
        }

        GeneratedG0Artifact CreateG0( BinPathConfiguration c ) => new GeneratedG0Artifact( c.ProjectPath.AppendPart( "G0.cs" ) );

        GeneratedFileArtifactWithTextSignature CreateAssembly( BinPathConfiguration c ) => new GeneratedFileArtifactWithTextSignature( c.OutputPath.AppendPart( _generatedAssemblyName ) );

        internal RunningBinPathGroup( string generatedAssemblyName, BinPathConfiguration head, SHA1Value sha, bool isUnifiedPure )
            : this( generatedAssemblyName, head, new[] { head }, sha )
        {
            IsUnifiedPure = isUnifiedPure;
        }

        internal bool Initialize( IActivityMonitor monitor, bool forceRun, bool findLoadedStObjMap )
        {
            CompileOption compile = CompileOption.None;
            bool source = false;
            foreach( var b in SimilarConfigurations )
            {
                compile = (CompileOption)Math.Max( (int)compile, (int)b.CompileOption );
                source |= b.GenerateSourceFiles;
            }
            CompileOption = compile;
            SaveSource = source;
            if( !forceRun && (source || compile != CompileOption.None ) )
            {
                var mapInfo = findLoadedStObjMap ? StObjContextRoot.GetMapInfo( SignatureCode, monitor ) : null;
                if( mapInfo != null )
                {
                    monitor.Info( $"An existing StObjMap with the signature is already loaded: setting SaveSource to false and CompileOption to None for BinPaths {Names}." );
                }
                else
                {
                    if( source && GeneratedSource.GetSignature( monitor ) == SignatureCode )
                    {
                        monitor.Info( $"Source '{GeneratedSource}' is up to date. Setting SaveSource to false for BinPaths {Names}." );
                        SaveSource = false;
                    }
                    if( compile != CompileOption.None && GeneratedAssembly.GetSignature( monitor ) == SignatureCode )
                    {
                        monitor.Info( $"Assembly '{GeneratedAssembly}' is up to date. Setting CompileOption to None for BinPaths {Names}." );
                        CompileOption = CompileOption.None;
                    }
                }
            }
            return true;
        }

        /// <inheritdoc />
        public BinPathConfiguration Configuration { get; }

        /// <inheritdoc />
        public bool IsUnifiedPure { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<BinPathConfiguration> SimilarConfigurations { get; }

        /// <inheritdoc />
        public SHA1Value SignatureCode { get; internal set; }

        /// <inheritdoc />
        public bool SaveSource { get; private set; }

        /// <inheritdoc />
        public CompileOption CompileOption { get; private set; }

        /// <inheritdoc />
        public GeneratedFileArtifactWithTextSignature GeneratedAssembly { get; }

        /// <inheritdoc />
        public GeneratedG0Artifact GeneratedSource { get; }


        /// <inheritdoc />
        public string Names => _names;

        internal void CopyArtifactsFromHead( IActivityMonitor monitor )
        {
            bool source = SaveSource && GeneratedSource.Exists();
            bool compile = CompileOption == CompileOption.Compile && GeneratedAssembly.Exists();
            if( !source && !compile ) return;

            foreach( var b in SimilarConfigurations.Skip( 1 ) )
            {
                if( source && b.GenerateSourceFiles )
                {
                    Copy( monitor, GeneratedSource.Path, CreateG0( b ) );
                }
                if( compile && b.CompileOption == CompileOption.Compile )
                {
                    Copy( monitor, GeneratedSource.Path, CreateAssembly( b ) );
                }
            }
        }

        void Copy( IActivityMonitor monitor, NormalizedPath source, IGeneratedArtifact t )
        {
            if( source != t.Path && t.GetSignature( monitor ) != SignatureCode )
            {
                monitor.Info( $"Updating '{t.Path}'." );
                SafeCopy( monitor, source, t.Path );
            }

            static void SafeCopy( IActivityMonitor monitor, NormalizedPath f, NormalizedPath t )
            {
                int retryCount = 0;
                retry:
                try
                {
                    File.Copy( f, t, true );
                }
                catch( Exception ex )
                {
                    if( retryCount++ < 3 )
                    {
                        monitor.Warn( $"Failed to copy project file: '{f.LastPart}'. Retrying.", ex );
                        System.Threading.Thread.Sleep( retryCount * 50 );
                        goto retry;
                    }
                    monitor.Error( $"Failed to copy file: '{f}' to '{t}'. Rethrowing.", ex );
                    throw;
                }
            }

        }


    }

}
