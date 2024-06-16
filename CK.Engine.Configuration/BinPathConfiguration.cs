using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;

namespace CK.Setup
{

    /// <summary>
    /// Describes a folder that the CKEngine must process.
    /// </summary>
    public sealed partial class BinPathConfiguration
    {
        readonly Dictionary<string,BinPathAspectConfiguration> _aspects;
        readonly HashSet<string> _assemblies;
        readonly HashSet<TypeConfiguration> _types;
        readonly HashSet<Type> _excludedTypes;
        EngineConfiguration? _owner;
        string? _name;
        NormalizedPath _path;
        NormalizedPath _outputPath;
        NormalizedPath _projectPath;
        CompileOption _compileOption;
        bool _generateSourceFiles;
        bool _discoverAssembliesFromPath;

        /// <summary>
        /// Initializes a new empty <see cref="BinPathConfiguration"/>.
        /// At least, the <see cref="Path"/> should be set for this BinPathConfiguration to be valid.
        /// </summary>
        public BinPathConfiguration()
        {
            GenerateSourceFiles = true;
            _assemblies = new HashSet<string>();
            _excludedTypes = new HashSet<Type>();
            _types = new HashSet<TypeConfiguration>();
            _aspects = new Dictionary<string, BinPathAspectConfiguration>();
        }

        /// <summary>
        /// Gets the configuration that contains this BinPath in its <see cref="EngineConfiguration.BinPaths"/>.
        /// </summary>
        public EngineConfiguration? Owner { get => _owner; internal set => _owner = value; }

        /// <summary>
        /// Gets or sets the name that uniquely identifies this configuration among the others.
        /// When null, an automatically numbered name is generated.
        /// </summary>
        public string? Name { get => _name; set => _name = value; }

        /// <summary>
        /// Gets or sets the path of the directory to setup.
        /// It can be relative: it will be combined to the <see cref="EngineConfiguration.BasePath"/>.
        /// <para>
        /// Nothing prevents multiple <see cref="BinPathConfiguration"/> to have the same Path. In such case, <see cref="OutputPath"/>
        /// and/or <see cref="ProjectPath"/> should be set to different directories (otherwise file generation will be in trouble).
        /// </para>
        /// </summary>
        public NormalizedPath Path { get => _path; set => _path = value; }

        /// <summary>
        /// Gets or sets an optional target (output) directory where generated files (assembly and/or sources)
        /// must be copied. When <see cref="NormalizedPath.IsEmptyPath"/>, this <see cref="Path"/> is used.
        /// </summary>
        public NormalizedPath OutputPath { get => _outputPath; set => _outputPath = value; }

        /// <summary>
        /// Gets or sets an optional target (output) directory for source files.
        /// When not <see cref="NormalizedPath.IsEmptyPath"/>, "$StObjGen/" folder is appended and
        /// the source files are generated into this folder instead of <see cref="OutputPath"/>.
        /// </summary>
        public NormalizedPath ProjectPath { get => _projectPath; set => _projectPath = value; }

        /// <summary>
        /// Gets or sets the Roslyn compilation behavior.
        /// Defaults to <see cref="CompileOption.None"/>.
        /// </summary>
        public CompileOption CompileOption { get => _compileOption; set => _compileOption = value; }

        /// <summary>
        /// Gets whether generated source files should be generated and copied to <see cref="OutputPath"/>.
        /// Defaults to true.
        /// </summary>
        public bool GenerateSourceFiles { get => _generateSourceFiles; set => _generateSourceFiles = value; }

        /// <summary>
        /// Gets a set of assembly names that must be processed for setup (only assemblies that appear in this list will be considered).
        /// This can be left empty if <see cref="DiscoverAssembliesFromPath"/> is true.
        /// assemblies.
        /// </summary>
        public HashSet<string> Assemblies => _assemblies;

        /// <summary>
        /// Gets or sets whether the dlls is the <see cref="Path"/> should be processed by the setup.
        /// <para>
        /// Defaults to false: only <see cref="Types"/> and existing <see cref="Assemblies"/> are considered.
        /// </para>
        /// </summary>
        public bool DiscoverAssembliesFromPath { get => _discoverAssembliesFromPath; set => _discoverAssembliesFromPath = value; }

        /// <summary>
        /// Gets a set of <see cref="TypeConfiguration"/> that must be registered explicitly regardless of the <see cref="Assemblies"/>.
        /// </summary>
        public HashSet<TypeConfiguration> Types => _types;

        /// <summary>
        /// Gets a set of types that must be excluded from registration.
        /// <para>
        /// Note that any type appearing in <see cref="EngineConfiguration.GlobalExcludedTypes"/> will also
        /// be excluded.
        /// </para>
        /// </summary>
        public HashSet<Type> ExcludedTypes => _excludedTypes;

        /// <summary>
        /// Gets the BinPath specific aspect configurations.
        /// </summary>
        public IReadOnlyCollection<BinPathAspectConfiguration> Aspects => _aspects.Values;

        /// <summary>
        /// Finds an existing aspect or returns null.
        /// </summary>
        /// <param name="name">The aspect name.</param>
        /// <returns>The aspect or null.</returns>
        public BinPathAspectConfiguration? FindAspect( string name ) => _aspects.GetValueOrDefault( name );

        /// <summary>
        /// Finds an existing aspect or returns null.
        /// <para>
        /// Whether <typeparamref name="T"/> is a <see cref="MultipleBinPathAspectConfiguration{TSelf}"/> or not
        /// doesn't matter: if at least one exists, it will be returned (with its <see cref="MultipleBinPathAspectConfiguration{TSelf}.OtherConfigurations"/>).
        /// </para>
        /// </summary>
        /// <returns>The aspect or null.</returns>
        public T? FindAspect<T>() where T : BinPathAspectConfiguration => _aspects.Values.OfType<T>().SingleOrDefault();

        /// <summary>
        /// Ensures that an aspect is registered in <see cref="Aspects"/>.
        /// <para>
        /// Whether <typeparamref name="T"/> is a <see cref="MultipleBinPathAspectConfiguration{TSelf}"/> or not
        /// doesn't matter: if at least one exists, it will be returned (with its <see cref="MultipleBinPathAspectConfiguration{TSelf}.OtherConfigurations"/>).
        /// </para>
        /// </summary>
        /// <typeparam name="T">The aspect type.</typeparam>
        /// <returns>The found or created aspect.</returns>
        public T EnsureAspect<T>() where T : BinPathAspectConfiguration, new()
        {
            T? a = FindAspect<T>();
            if( a == null )
            {
                AddAspect( a = new T() );
            }
            return a;
        }

        /// <summary>
        /// Adds an aspect to these <see cref="Aspects"/>.
        /// <para>
        /// No existing aspect with the same type must exist (except if it is a <see cref="MultipleBinPathAspectConfiguration{TSelf}"/>), the
        /// aspect must not belong to another configuration and the engine aspect must exist in the <see cref="EngineConfiguration.Aspects"/>
        /// otherwise an <see cref="ArgumentException"/> is thrown.
        /// </para>
        /// </summary>
        /// <param name="aspect">An aspect configuration to add.</param>
        public void AddAspect( BinPathAspectConfiguration aspect )
        {
            Throw.CheckNotNullArgument( aspect );
            if( aspect.Owner == this ) return;

            Throw.CheckArgument( aspect.Owner is null );

            EngineAspectConfiguration? engineAspect = Owner?.FindAspect( aspect.AspectName );
            if( Owner != null && engineAspect == null )
            {
                Throw.ArgumentException( nameof( aspect ), $"Unable to add the BinPath aspect configuration. The aspect '{aspect.AspectName}' must exist in the EngineConfiguration.Aspects." );
            }
            if( _aspects.TryGetValue( aspect.AspectName, out var existing ) )
            {
                if( existing is MultipleBinPathAspectConfiguration multi )
                {
                    if( aspect is not MultipleBinPathAspectConfiguration multiConf
                        || !multi.GetType().IsAssignableFrom( multiConf.GetType() ) )
                    {
                        Throw.ArgumentException( nameof( aspect ), $"Aspect configuration named '{aspect.AspectName}' (Type: {aspect.GetType().ToCSharpName()}) is not compatible withe existing type '{multi.GetType().ToCSharpName()}'." );
                    }
                    else
                    {
                        multi.DoAddOtherConfiguration( multiConf );
                    }
                }
                else
                {
                    Throw.ArgumentException( nameof( aspect ), $"An aspect configuration with the same type name '{aspect.AspectName}' already exists and is not a MultipleBinPathAspectConfiguration." );
                }
            }
            else
            {
                aspect.Bind( this, engineAspect );
                _aspects.Add( aspect.AspectName, aspect );
            }
        }

        /// <summary>
        /// Removes an aspect from <see cref="Aspects"/>. Does nothing if the <paramref name="aspect"/>
        /// does not belong to this configuration.
        /// </summary>
        /// <param name="aspect">An aspect configuration to remove.</param>
        public void RemoveAspect( BinPathAspectConfiguration aspect )
        {
            Throw.CheckNotNullArgument( aspect );
            if( aspect.Owner == this )
            {
                aspect.HandleOwnRemove( _aspects );
            }
        }

        /// <summary>
        /// Removes an aspect from <see cref="Aspects"/>. Does nothing if no <paramref name="name"/>
        /// aspect belong to this configuration.
        /// </summary>
        /// <param name="name">Name of the aspect configuration to remove.</param>
        public void RemoveAspect( string name )
        {
            var c = _aspects.GetValueOrDefault( name );
            if( c != null )
            {
                c.Bind( null, null );
                _aspects.Remove( name );
            }
        }
    }

}
