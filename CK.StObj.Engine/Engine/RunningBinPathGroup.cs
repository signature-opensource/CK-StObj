using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Loader;

namespace CK.Setup;

/// <summary>
/// Implements <see cref="IRunningBinPathGroup"/>.
/// </summary>
sealed class RunningBinPathGroup : IRunningBinPathGroup
{
    readonly BinPathTypeGroup _typeGroup;
    readonly string _generatedDllName;
    readonly string _names;
    readonly EngineConfiguration _engineConfiguration;
    readonly BinPathConfiguration _configuration;
    readonly IReadOnlyCollection<BinPathConfiguration> _similarConfigurations;
    readonly GeneratedFileArtifactWithTextSignature? _generatedAssembly;
    readonly GeneratedG0Artifact? _generatedSource;
    internal StObjCollectorResult? _collectorResult;

    SaveSourceLevel _saveSource;
    SHA1Value _runSignature;
    CompileOption _compileOption;

    internal enum SaveSourceLevel
    {
        None,
        RequiredForSHA1,
        SaveSource
    }

    internal IConfiguredTypeSet ConfiguredTypes => _typeGroup!.ConfiguredTypes;

    internal RunningBinPathGroup( EngineConfiguration engineConfiguration, BinPathTypeGroup typeGroup )
    {
        _typeGroup = typeGroup;
        _engineConfiguration = engineConfiguration;
        _names = typeGroup.GroupName;
        if( typeGroup.IsUnifiedPure )
        {
            // Legacy unified had a Configuration. Let's create a stupid empty one
            // because we already have the types.
            var unified = new BinPathConfiguration()
            {
                Path = AppContext.BaseDirectory,
                OutputPath = AppContext.BaseDirectory,
                Name = "_Unified_",
                // The root (the Working directory) doesn't want any output by itself.
                GenerateSourceFiles = false
            };
            Debug.Assert( unified.CompileOption == CompileOption.None );
            _configuration = unified;
            _generatedDllName = String.Empty;
            _similarConfigurations = new[] { unified };
        }
        else
        {
            _configuration = typeGroup.Configurations[0];
            _generatedDllName = $"{engineConfiguration.GeneratedAssemblyName}.{_configuration.Name}.dll";
            _similarConfigurations = typeGroup.Configurations;
            _runSignature = engineConfiguration.BaseSHA1;
            _generatedSource = CreateG0( _configuration );
            _generatedAssembly = CreateAssembly( _configuration );
        }
    }

    static GeneratedG0Artifact CreateG0( BinPathConfiguration c ) => new GeneratedG0Artifact( c.ProjectPath.AppendPart( "$StObjGen" ).AppendPart( "G0.cs" ) );

    GeneratedFileArtifactWithTextSignature CreateAssembly( BinPathConfiguration c ) => new GeneratedFileArtifactWithTextSignature( c.OutputPath.AppendPart( _generatedDllName ) );

    internal bool Initialize( IActivityMonitor monitor, bool forceRun, ref bool canSkipRun )
    {
        if( IsUnifiedPure )
        {
            // If we are on the unified pure BinPath, we have nothing to do.
            Throw.DebugAssert( _saveSource == SaveSourceLevel.None
                               && _compileOption == CompileOption.None
                               && _similarConfigurations.Single() == _configuration
                               && _runSignature.IsZero );
            return true;
        }
        CompileOption compile = CompileOption.None;
        bool source = false;
        foreach( var b in _similarConfigurations )
        {
            compile = (CompileOption)Math.Max( (int)compile, (int)b.CompileOption );
            source |= b.GenerateSourceFiles;
        }
        _compileOption = compile;
        _saveSource = source ? SaveSourceLevel.SaveSource : SaveSourceLevel.None;

        if( _runSignature.IsZero )
        {
            // No known code base SHA1.
            // Retrieving the SHA1 (if forceSetup is false) from the existing generated source and/or assembly
            // is easily doable but pointless: the Engine is runnning in-process without known SHA1, with 
            // a set of types that is specific and with no way to have any clue about
            // their "content" (even for two consecutive identical set of types, their code, attributes or the
            // code of the generators may have changed between 2 runs).
            // In this usage, the goal is to correctly manage the G0.cs and CK.GeneratedAssembly files.
            if( _saveSource == SaveSourceLevel.None )
            {
                monitor.Info( $"Source code for '{Names}' will be generated to compute the SHA1 but will not be saved." );
                // This level doesn't need to be exposed since the GenerateSourceCodeSecondPass
                // will generate the source code even if SaveSource is false: this level is here to avoid setting
                // SaveSource to true here so that CopyArtifactsFromHead will not update any files.
                _saveSource = SaveSourceLevel.RequiredForSHA1;
            }
            canSkipRun = false;
        }
        else if( !forceRun && (_saveSource != SaveSourceLevel.None || compile != CompileOption.None) )
        {
            // A code base SHA1 is provided.
            // If we can find this map in the already available StObjMap, we may skip the run.
            var mapInfo = StObjContextRoot.GetMapInfo( _runSignature, monitor );
            if( mapInfo != null )
            {
                monitor.Info( $"An existing StObjMap with the signature is already loaded: setting SaveSource to false and CompileOption to None for BinPaths {Names}." );
                _saveSource = SaveSourceLevel.None;
                _compileOption = CompileOption.None;
            }
            else
            {
                if( _saveSource != SaveSourceLevel.None && _generatedSource.GetSignature( monitor ) == _runSignature )
                {
                    monitor.Info( $"Source '{_generatedSource}' is up to date. Setting SaveSource to false for BinPaths {Names}." );
                    _saveSource = SaveSourceLevel.None;
                }
                if( compile != CompileOption.None && _generatedAssembly.GetSignature( monitor ) == _runSignature )
                {
                    monitor.Info( $"Assembly '{_generatedAssembly}' is up to date. Setting CompileOption to None for BinPaths {Names}." );
                    _compileOption = CompileOption.None;
                }
            }
            canSkipRun &= _saveSource == SaveSourceLevel.None && _compileOption == CompileOption.None;
        }
        return true;
    }

    /// <inheritdoc />
    public EngineConfiguration EngineConfiguration => _engineConfiguration;

    /// <inheritdoc />
    public BinPathConfiguration Configuration => _configuration;

    /// <inheritdoc />
    [MemberNotNullWhen( false, nameof( _generatedSource ), nameof( _generatedAssembly ), nameof( GeneratedSource ), nameof( GeneratedAssembly ) )]
    public bool IsUnifiedPure => _typeGroup.IsUnifiedPure;

    /// <inheritdoc />
    public IReadOnlyCollection<BinPathConfiguration> SimilarConfigurations => _similarConfigurations;

    /// <inheritdoc />
    public SHA1Value RunSignature { get => _runSignature; internal set => _runSignature = value; }

    /// <inheritdoc />
    [MemberNotNullWhen( true, nameof( GeneratedSource ), nameof( _generatedSource ) )]
    public bool SaveSource => _saveSource == SaveSourceLevel.SaveSource;

    /// <inheritdoc />
    public CompileOption CompileOption => _compileOption;

    /// <inheritdoc />
    public GeneratedFileArtifactWithTextSignature? GeneratedAssembly => _generatedAssembly;

    /// <inheritdoc />
    public GeneratedG0Artifact? GeneratedSource => _generatedSource;

    /// <inheritdoc />
    public string Names => _names;

    /// <inheritdoc />
    public IStObjEngineMap? EngineMap => _collectorResult?.EngineMap;

    /// <inheritdoc />
    public IPocoTypeSystemBuilder? PocoTypeSystemBuilder => _collectorResult?.PocoTypeSystemBuilder;

    /// <inheritdoc/>
    public IStObjMap? TryLoadStObjMap( IActivityMonitor monitor, bool embeddedIfPossible = true )
    {
        return TryLoadStObjMap( monitor, embeddedIfPossible, false );
    }

    /// <inheritdoc/>
    public IStObjMap LoadStObjMap( IActivityMonitor monitor, bool embeddedIfPossible = true )
    {
        return TryLoadStObjMap( monitor, embeddedIfPossible, true )!;
    }

    IStObjMap? TryLoadStObjMap( IActivityMonitor monitor, bool embeddedIfPossible, bool throwOnError )
    {
        Throw.CheckState( !IsUnifiedPure );
        if( embeddedIfPossible )
        {
            IStObjMap? map = StObjContextRoot.Load( RunSignature, monitor );
            if( map != null )
            {
                monitor.Info( $"Embedded generated source code is available for BinPath '{_names}'." );
                return map;
            }
            monitor.Info( $"No embedded generated source code found for RunSignature '{RunSignature}'." );
        }
        if( !GeneratedAssembly.Exists() )
        {
            var msg = $"Unable to find generated assembly '{GeneratedAssembly.Path}' for BinPath '{_names}'.";
            if( throwOnError ) Throw.InvalidOperationException( msg );
            monitor.Error( msg );
        }
        else
        {
            System.Reflection.Assembly a;
            try
            {
                a = AssemblyLoadContext.Default.LoadFromAssemblyPath( GeneratedAssembly.Path );
                return StObjContextRoot.Load( a, monitor );
            }
            catch( Exception ex )
            {
                var msg = $"While loading assembly '{GeneratedAssembly.Path}' for BinPath '{_names}'.";
                if( throwOnError ) Throw.InvalidOperationException( msg, ex );
                monitor.Error( msg, ex );
            }
        }
        return null;
    }


    internal bool UpdateSimilarArtifactsFromHead( IActivityMonitor monitor )
    {
        Debug.Assert( !IsUnifiedPure );
        bool source = _saveSource == SaveSourceLevel.SaveSource && _generatedSource.Exists();
        bool compile = _compileOption == CompileOption.Compile && _generatedAssembly.Exists();
        if( !source && !compile ) return true;

        foreach( var b in _similarConfigurations.Skip( 1 ) )
        {
            if( source && b.GenerateSourceFiles )
            {
                if( !Update( monitor, _generatedSource.Path, CreateG0( b ) ) ) return false;
            }
            if( compile && b.CompileOption == CompileOption.Compile )
            {
                if( !Update( monitor, _generatedSource.Path, CreateAssembly( b ) ) ) return false;
            }
        }
        return true;
    }

    bool Update( IActivityMonitor monitor, NormalizedPath source, IGeneratedArtifact t )
    {
        if( source != t.Path && t.GetSignature( monitor ) != _runSignature )
        {
            return t.UpdateFrom( monitor, source );
        }
        return true;
    }


}
