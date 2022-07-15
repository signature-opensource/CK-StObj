using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements <see cref="IRunningBinPathGroup"/>.
    /// </summary>
    public sealed class RunningBinPathGroup : IRunningBinPathGroup
    {
        readonly string _generatedDllName;
        readonly string _names;
        SaveSourceLevel _saveSource;

        internal enum SaveSourceLevel
        {
            None,
            RequiredForSHA1,
            SaveSource
        }

        internal RunningBinPathGroup( string generatedAssemblyName, BinPathConfiguration head, BinPathConfiguration[] similars, SHA1Value sha )
        {
            Debug.Assert( generatedAssemblyName != null && generatedAssemblyName.StartsWith( StObjContextRoot.GeneratedAssemblyName ) );
            Debug.Assert( similars != null && similars.Length > 0 && similars[0] == head );
            _generatedDllName = $"{generatedAssemblyName}-{head.Name}.dll";
            Configuration = head;
            SimilarConfigurations = similars;
            RunSignature = sha;
            GeneratedSource = CreateG0( head );
            GeneratedAssembly = CreateAssembly( head );
            _names = similars.Select( c => c.Name ).Concatenate();
        }

        internal RunningBinPathGroup( string generatedAssemblyName, BinPathConfiguration head, SHA1Value sha )
            : this( generatedAssemblyName, head, new[] { head }, sha )
        {
        }

        internal RunningBinPathGroup( BinPathConfiguration unifiedPure )
        {
            IsUnifiedPure = true;
            _generatedDllName = String.Empty;
            Configuration = unifiedPure;
            SimilarConfigurations = new[] { unifiedPure };
            _names = "(Unified)";
        }

        GeneratedG0Artifact CreateG0( BinPathConfiguration c ) => new GeneratedG0Artifact( c.ProjectPath.AppendPart( "G0.cs" ) );

        GeneratedFileArtifactWithTextSignature CreateAssembly( BinPathConfiguration c ) => new GeneratedFileArtifactWithTextSignature( c.OutputPath.AppendPart( _generatedDllName ) );

        internal bool Initialize( IActivityMonitor monitor, bool forceRun, ref bool canSkipRun )
        {
            if( IsUnifiedPure )
            {
                // If we are on the unified pure BinPath.
                Debug.Assert( _saveSource == SaveSourceLevel.None
                              && CompileOption == CompileOption.None
                              && SimilarConfigurations.Single() == Configuration
                              && RunSignature.IsZero );
                return true;
            }
            CompileOption compile = CompileOption.None;
            bool source = false;
            foreach( var b in SimilarConfigurations )
            {
                compile = (CompileOption)Math.Max( (int)compile, (int)b.CompileOption );
                source |= b.GenerateSourceFiles;
            }
            CompileOption = compile;
            _saveSource = source ? SaveSourceLevel.SaveSource : SaveSourceLevel.None;

            if( RunSignature.IsZero )
            {
                // No known code base SHA1.
                // We are not called by CKSetup: the StObjEngine is run in-process, typically
                // by CK.Testing.StObjEngine.
                // Retrieving the SHA1 (if forceSetup is false) from the existing generated source and/or assembly
                // is easily doable but pointless: when the StObjEngine is ran in-process without known SHA1, it is 
                // with a StObjCollector (the set of types) that is specific and with no way to have any clue about
                // their "content" (even for two consecutive identical set of types, their code, attributes or the
                // code of the generators may have changed between 2 runs).
                // In this usage, the goal is to correctly manage the G0.cs and CK.StObj.AutoAssembly files.
                //
                // The behavior here is tailored for CK.Testing.StObjEngine and by its API.
                // If the source code is not required, we require it here so that the SHA1 can be computed based on
                // the generated code source.
                if( _saveSource == SaveSourceLevel.None )
                {
                    monitor.Info( $"Source code for '{Names}' will be generated to compute the SHA1 but will not be saved." );
                    // This level doesn't need to be exposed since the GenerateSourceCodeSecondPass
                    // will generate the source code even if SaveSource is false, CompileOption is None as soon as RunSignature
                    // is zero: this level is here to avoid setting SaveSource to true here so that CopyArtifactsFromHead
                    // will not update any files.
                    _saveSource = SaveSourceLevel.RequiredForSHA1;
                }
                canSkipRun = false;
            }
            else if( !forceRun && (_saveSource != SaveSourceLevel.None || compile != CompileOption.None ) )
            {
                // A code base SHA1 is provided.
                // If we can find this map in the already available StObjMap, we may skip the run.
                var mapInfo = StObjContextRoot.GetMapInfo( RunSignature, monitor );
                if( mapInfo != null )
                {
                    monitor.Info( $"An existing StObjMap with the signature is already loaded: setting SaveSource to false and CompileOption to None for BinPaths {Names}." );
                    _saveSource = SaveSourceLevel.None;
                    CompileOption = CompileOption.None;
                }
                else
                {
                    if( _saveSource != SaveSourceLevel.None && GeneratedSource.GetSignature( monitor ) == RunSignature )
                    {
                        monitor.Info( $"Source '{GeneratedSource}' is up to date. Setting SaveSource to false for BinPaths {Names}." );
                        _saveSource = SaveSourceLevel.None;
                    }
                    if( compile != CompileOption.None && GeneratedAssembly.GetSignature( monitor ) == RunSignature )
                    {
                        monitor.Info( $"Assembly '{GeneratedAssembly}' is up to date. Setting CompileOption to None for BinPaths {Names}." );
                        CompileOption = CompileOption.None;
                    }
                }
                canSkipRun &= _saveSource == SaveSourceLevel.None && CompileOption == CompileOption.None;
            }
            return true;
        }

        /// <inheritdoc />
        public BinPathConfiguration Configuration { get; }

        /// <inheritdoc />
        [MemberNotNullWhen( false, nameof( GeneratedSource ), nameof( GeneratedAssembly ) )]
        public bool IsUnifiedPure { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<BinPathConfiguration> SimilarConfigurations { get; }

        /// <inheritdoc />
        public SHA1Value RunSignature { get; internal set; }

        /// <inheritdoc />
        [MemberNotNullWhen( true, nameof( GeneratedSource ) )]
        public bool SaveSource => _saveSource == SaveSourceLevel.SaveSource;

        /// <inheritdoc />
        public CompileOption CompileOption { get; private set; }

        /// <inheritdoc />
        public GeneratedFileArtifactWithTextSignature? GeneratedAssembly { get; }

        /// <inheritdoc />
        public GeneratedG0Artifact? GeneratedSource { get; }


        /// <inheritdoc />
        public string Names => _names;

        internal bool UpdateSimilarArtifactsFromHead( IActivityMonitor monitor )
        {
            Debug.Assert( !IsUnifiedPure );
            bool source = _saveSource == SaveSourceLevel.SaveSource && GeneratedSource.Exists();
            bool compile = CompileOption == CompileOption.Compile && GeneratedAssembly.Exists();
            if( !source && !compile ) return true;

            foreach( var b in SimilarConfigurations.Skip( 1 ) )
            {
                if( source && b.GenerateSourceFiles )
                {
                    if( !Update( monitor, GeneratedSource.Path, CreateG0( b ) ) ) return false;
                }
                if( compile && b.CompileOption == CompileOption.Compile )
                {
                    if( !Update( monitor, GeneratedSource.Path, CreateAssembly( b ) ) ) return false;
                }
            }
            return true;
        }

        bool Update( IActivityMonitor monitor, NormalizedPath source, IGeneratedArtifact t )
        {
            if( source != t.Path && t.GetSignature( monitor ) != RunSignature )
            {
                return t.UpdateFrom( monitor, source );
            }
            return true;
        }


    }

}
