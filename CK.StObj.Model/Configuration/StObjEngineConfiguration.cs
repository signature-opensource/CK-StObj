using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates configuration of the StObjEngine.
    /// This configuration is compatible with CKSetup SetupConfiguration object.
    /// </summary>
    public sealed partial class StObjEngineConfiguration
    {
        /// <summary>
        /// Default assembly name.
        /// </summary>
        public const string DefaultGeneratedAssemblyName = "CK.StObj.AutoAssembly";

        /// <summary>
        /// Gets the mutable list of all configuration aspects that must participate to setup.
        /// </summary>
        public List<IStObjEngineAspectConfiguration> Aspects { get; }

        /// <summary>
        /// Gets or sets the final Assembly name.
        /// When set to null (the default), <see cref="DefaultGeneratedAssemblyName"/> "CK.StObj.AutoAssembly" is returned.
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </summary>
        public string GeneratedAssemblyName
        {
            get => String.IsNullOrWhiteSpace(_generatedAssemblyName) ? DefaultGeneratedAssemblyName : _generatedAssemblyName;
            set
            {
                if( value != null && FileUtil.IndexOfInvalidFileNameChars( value ) >= 0 )
                {
                    throw new ArgumentException( $"Invalid file character in file name '{value}'." );
                }
                _generatedAssemblyName = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="System.Diagnostics.FileVersionInfo.ProductVersion"/> of
        /// the <see cref="GeneratedAssemblyName"/> assembly or assemblies.
        /// Defaults to null (no <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/> should be generated).
        /// This is a global configuration that applies to all the <see cref="BinPaths"/>.
        /// </summary>
        public string InformationalVersion { get; set; }

        /// <summary>
        /// Gets ors sets whether the ordering of StObj that share the same rank in the dependency graph must be inverted.
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
        /// Their <see cref="BinPath.Assemblies"/> must exist in the current <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public List<BinPath> BinPaths { get; }

        /// <summary>
        /// Gets a mutable set of assembly qualified type names that must be excluded from registration.
        /// This applies to all <see cref="BinPaths"/>: excluding a type here guaranties its exclusion
        /// from any BinPath.
        /// </summary>
        public HashSet<string> GlobalExcludedTypes { get; }

    }
}
