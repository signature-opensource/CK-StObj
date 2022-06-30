using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates configuration of the StObjEngine.
    /// This configuration is compatible with CKSetup SetupConfiguration object.
    /// </summary>
    public sealed partial class StObjEngineConfiguration
    {
        /// <summary>
        /// Gets the mutable list of all configuration aspects that must participate to setup.
        /// </summary>
        public List<IStObjEngineAspectConfiguration> Aspects { get; }

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
        /// Gets a list of binary paths to setup (must not be empty).
        /// Their <see cref="BinPathConfiguration.Assemblies"/> or non optional <see cref="BinPathConfiguration.Types"/>
        /// must exist in the current <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public List<BinPathConfiguration> BinPaths { get; }

        /// <summary>
        /// Gets a mutable set of assembly qualified type names that must be excluded from registration.
        /// This applies to all <see cref="BinPaths"/>: excluding a type here guaranties its exclusion
        /// from any BinPath.
        /// </summary>
        public HashSet<string> GlobalExcludedTypes { get; }

        /// <summary>
        /// Gets or sets a base SHA1 for the StObjMaps (CKSetup sets this to the files signature).
        /// <para>
        /// By defaults, when <see cref="SHA1Value.Zero"/> or <see cref="SHA1Value.Empty"/>, the base signature is random
        /// (any cache is disabled).
        /// </para>
        /// <para>
        /// The final signatures are the hashes of this property and the normalized <see cref="BinPathConfiguration.Path"/>.
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
