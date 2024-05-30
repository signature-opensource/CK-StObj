using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates configuration of the StObjEngine.
    /// This configuration is compatible with CKSetup SetupConfiguration object.
    /// </summary>
    public sealed partial class StObjEngineConfiguration
    {
        readonly List<BinPathConfiguration> _binPaths;
        Dictionary<string, StObjEngineAspectConfiguration> _namedAspects;
        List<StObjEngineAspectConfiguration> _aspects;
        string? _generatedAssemblyName;

        /// <summary>
        /// Gets the list of all configuration aspects that must participate to setup.
        /// </summary>
        public IReadOnlyList<StObjEngineAspectConfiguration> Aspects => _aspects;

        /// <summary>
        /// Finds an existing aspect or returns null.
        /// </summary>
        /// <param name="name">The aspect name.</param>
        /// <returns>The aspect or null.</returns>
        public StObjEngineAspectConfiguration? FindAspect( string name ) => _namedAspects.GetValueOrDefault( name );

        /// <summary>
        /// Finds an existing aspect or returns null.
        /// </summary>
        /// <param name="name">The aspect name.</param>
        /// <returns>The aspect or null.</returns>
        public T? FindAspect<T>() where T : StObjEngineAspectConfiguration => _aspects.OfType<T>().SingleOrDefault();

        /// <summary>
        /// Adds an aspect to these <see cref="BinPaths"/>.
        /// No existing aspect with the same type must exist and the aspect must not belong to
        /// another configuration otherwise an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="aspect">An aspect configuration to add.</param>
        public void AddAspect( StObjEngineAspectConfiguration aspect )
        {
            Throw.CheckArgument( aspect != null && aspect.Owner == null );
            Throw.CheckArgument( "An aspect of the same type already exists.", !_namedAspects.ContainsKey( aspect.Name ) );
            aspect.Owner = this;
            _aspects.Add( aspect );
            _namedAspects.Add( aspect.Name, aspect );
        }

        /// <summary>
        /// Removes an aspect from <see cref="Aspects"/>. Does nothing if the <paramref name="aspect"/>
        /// does not belong to this configuration.
        /// </summary>
        /// <param name="aspect">An aspect configuration to remove.</param>
        public void RemoveAspect( StObjEngineAspectConfiguration aspect )
        {
            Throw.CheckArgument( aspect != null );
            if( aspect.Owner == this )
            {
                _aspects.Remove( aspect );
                aspect.Owner = null;
                _namedAspects.Remove( aspect.Name );
                foreach( var b in _binPaths )
                {
                    b.RemoveAspect( aspect.Name );
                }
            }
        }

        /// <summary>
        /// Gets or sets the final Assembly name.
        /// It must be <see cref="StObjContextRoot.GeneratedAssemblyName"/> (the default "CK.StObj.AutoAssembly") or
        /// start with "CK.StObj.AutoAssembly.".
        /// <para>
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </para>
        /// </summary>
        [AllowNull]
        public string GeneratedAssemblyName
        {
            get => _generatedAssemblyName ?? StObjContextRoot.GeneratedAssemblyName;
            set
            {
                if( !String.IsNullOrWhiteSpace( value ) )
                {
                    if( FileUtil.IndexOfInvalidFileNameChars( value ) >= 0 )
                    {
                        Throw.ArgumentException( $"Invalid file character in file name '{value}'." );
                    }
                    Throw.CheckArgument( value == StObjContextRoot.GeneratedAssemblyName || value.StartsWith( StObjContextRoot.GeneratedAssemblyName + '.' ) );
                    _generatedAssemblyName = value;
                }
                else _generatedAssemblyName = null;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="System.Diagnostics.FileVersionInfo.ProductVersion"/> of
        /// the generated assembly.
        /// Defaults to null (no <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/> should be generated).
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </summary>
        public string? InformationalVersion { get; set; }

        /// <summary>
        /// Gets or sets whether the ordering of StObj that share the same rank in the dependency graph must be inverted.
        /// Defaults to false.
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </summary>
        public bool RevertOrderingNames { get; set; }

        /// <summary>
        /// Gets or sets whether the dependency graph (the set of IDependentItem) associated
        /// to the StObj objects must be send to the monitor before sorting.
        /// Defaults to false.
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </summary>
        public bool TraceDependencySorterInput { get; set; }

        /// <summary>
        /// Gets or sets whether the dependency graph (the set of ISortedItem) associated
        /// to the StObj objects must be send to the monitor once the graph is sorted.
        /// Defaults to false.
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </summary>
        public bool TraceDependencySorterOutput { get; set; }

        /// <summary>
        /// Gets or sets an optional base path that applies to relative <see cref="BinPaths"/>.
        /// When null or empty, the current directory is used.
        /// </summary>
        public NormalizedPath BasePath { get; set; }

        /// <summary>
        /// Gets the binary paths to setup (must not be empty).
        /// Their <see cref="BinPathConfiguration.Assemblies"/> or non optional <see cref="BinPathConfiguration.Types"/>
        /// must exist in the current <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public IReadOnlyList<BinPathConfiguration> BinPaths => _binPaths;

        /// <summary>
        /// Adds a BinPathConfiguration to these <see cref="BinPaths"/>.
        /// <see cref="BinPathAspectConfiguration"/> that cannot be bound to an existing <see cref="Aspects"/>
        /// are removed from the <see cref="BinPathConfiguration.Aspects"/>.
        /// </summary>
        /// <param name="binPath">A BinPath configuration to add.</param>
        public void AddBinPath( BinPathConfiguration binPath )
        {
            Throw.CheckArgument( binPath != null && binPath.Owner ==  null );
            binPath.Owner = this;
            _binPaths.Add( binPath );
            // Remove orphans BinPath configurations or bind them.
            List<BinPathAspectConfiguration>? toRemove = null; 
            foreach( var aspect in binPath.Aspects )
            {
                var a = FindAspect( aspect.Name );
                if( a != null ) aspect.AspectConfiguration = a;
                else
                {
                    toRemove ??= new List<BinPathAspectConfiguration>();
                    toRemove.Add( aspect );
                }
            }
            if( toRemove != null )
            {
                foreach( var aspect in toRemove )
                {
                    binPath.RemoveAspect( aspect );
                }
            }
        }

        /// <summary>
        /// Removes a BinPathConfiguration from <see cref="BinPaths"/>. Does nothing if the <paramref name="binPath"/>
        /// does not belong to this configuration.
        /// </summary>
        /// <param name="binPath">A BinPath configuration to remove.</param>
        public void RemoveBinPath( BinPathConfiguration binPath )
        {
            Throw.CheckArgument( binPath != null );
            if( binPath.Owner == this )
            {
                _binPaths.Remove( binPath );
                binPath.Owner = null;
            }
        }

        /// <summary>
        /// Gets a mutable set of assembly qualified type names that must be excluded from registration.
        /// This applies to all <see cref="BinPaths"/>: excluding a type here guaranties its exclusion
        /// from any BinPath.
        /// </summary>
        public HashSet<string> GlobalExcludedTypes { get; }

        /// <summary>
        /// Gets or sets a base SHA1 for the StObjMaps (CKSetup sets this to the files signature).
        /// <para>
        /// By defaults, when <see cref="SHA1Value.Zero"/> or <see cref="SHA1Value.Empty"/>, the final signatures
        /// of each BinPath are computed from the generated source code.
        /// </para>
        /// <para>
        /// When set to non zero, the BinPaths' signature are the hashes of this property and the normalized <see cref="BinPathConfiguration.Path"/>.
        /// </para>
        /// </summary>
        public SHA1Value BaseSHA1 { get; set; }

        /// <summary>
        /// Gets whether caching may occur at any level (based on <see cref="BaseSHA1"/>) or if
        /// a run must be done regardless of any previous states.
        /// Defaults to false.
        /// </summary>
        public bool ForceRun { get; set; }

    }
}
