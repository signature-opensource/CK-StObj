using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Group of <see cref="SimilarConfigurations"/>: Assemblies, ExcludedTypes and Types configurations
    /// are the same.
    /// </summary>
    public interface IRunningBinPathGroup
    {
        /// <summary>
        /// Gets the root engine configuration.
        /// </summary>
        StObjEngineConfiguration EngineConfiguration { get; }

        /// <summary>
        /// Gets the first configuration in the <see cref="SimilarConfigurations"/>.
        /// </summary>
        BinPathConfiguration Configuration { get; }

        /// <summary>
        /// Gets whether this group is the purely unified one.
        /// When true, no similar configuration exist.
        /// <para>
        /// This unified BinPath has not the same Assemblies, ExcludedTypes and Types configurations as any of the actual BinPaths.
        /// This BinPath is only used to create an incomplete primary StObjMap (without AutoService resolution) that will
        /// contain all the IPoco and IRealObject. Generating the code for this BinPath will impact the real world with
        /// the unified types from all the BinPaths but this code will never be used.
        /// </para>
        /// </summary>
        [MemberNotNullWhen( false, nameof( GeneratedSource ), nameof( GeneratedAssembly ) )]
        public bool IsUnifiedPure { get; }

        /// <summary>
        /// Gets this and other configurations that are similar.
        /// </summary>
        IReadOnlyCollection<BinPathConfiguration> SimilarConfigurations { get; }

        /// <summary>
        /// Gets the SHA1 for this BinPath. All <see cref="SimilarConfigurations"/> share the same SHA1.
        /// <para>
        /// If <see cref="IsUnifiedPure"/> is true, this is always <see cref="SHA1Value.IsZero"/>.
        /// </para>
        /// <para>
        /// If no <see cref="StObjEngineConfiguration.BaseSHA1"/> has been provided, this is the SHA1 of
        /// the generated source code.
        /// </para>
        /// </summary>
        SHA1Value RunSignature { get; }

        /// <summary>
        /// Gets the name (or comma separated names) of the <see cref="SimilarConfigurations"/>.
        /// </summary>
        string Names { get; }

        /// <summary>
        /// Gets whether at least one <see cref="BinPathConfiguration.GenerateSourceFiles"/> in <see cref="SimilarConfigurations"/> is true
        /// and the <see cref="GeneratedSource"/> is not already up to date.
        /// </summary>
        [MemberNotNullWhen( true, nameof( GeneratedSource ) )]
        bool SaveSource { get; }

        /// <summary>
        /// Gets whether the generated source code must be parsed and/or compiled.
        /// This is by default the max of <see cref="BinPathConfiguration.CompileOption"/> from the <see cref="SimilarConfigurations"/>
        /// but can be set to <see cref="CompileOption.None"/> if <see cref="GeneratedAssembly"/> is already up to date.
        /// </summary>
        CompileOption CompileOption { get; }

        /// <summary>
        /// Gets the assembly file path.
        /// Null if and only if <see cref="IsUnifiedPure"/> is true.
        /// It will be generated only if <see cref="CompileOption"/> is <see cref="CompileOption.Compile"/>.
        /// </summary>
        GeneratedFileArtifactWithTextSignature? GeneratedAssembly { get; }

        /// <summary>
        /// Gets the source file path.
        /// Null if and only if <see cref="IsUnifiedPure"/> is true.
        /// It will be generated only if <see cref="SaveSource"/> is true.
        /// </summary>
        GeneratedG0Artifact? GeneratedSource { get; }

        /// <summary>
        /// Gets the <see cref="IPocoTypeSystemBuilder"/> for this BinPath if the run has not been skipped.
        /// </summary>
        IPocoTypeSystemBuilder? PocoTypeSystemBuilder { get; }

        /// <summary>
        /// Tries to load the <see cref="IStObjMap"/> from <see cref="IRunningBinPathGroup.RunSignature"/> SHA1 from
        /// already available maps (see <see cref="StObjContextRoot.Load(SHA1Value, IActivityMonitor?)"/>)
        /// or from the <see cref="IRunningBinPathGroup.GeneratedAssembly"/>.
        /// <para>
        /// This must not be called on the <see cref="IRunningBinPathGroup.IsUnifiedPure"/> otherwise an <see cref="InvalidOperationException"/>
        /// is thrown.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="embeddedIfPossible">
        /// False to skip an available map and load it from the generated assembly.
        /// By default, the map is searched in available ones before loading the assembly.
        /// </param>
        /// <returns>The map or null on error.</returns>
        IStObjMap? TryLoadStObjMap( IActivityMonitor monitor, bool embeddedIfPossible = true );

        /// <summary>
        /// Loads the <see cref="IStObjMap"/> from <see cref="IRunningBinPathGroup.RunSignature"/> SHA1 from
        /// already available maps (see <see cref="StObjContextRoot.Load(SHA1Value, IActivityMonitor?)"/>)
        /// or from the <see cref="IRunningBinPathGroup.GeneratedAssembly"/>.
        /// <para>
        /// This must not be called on the <see cref="IRunningBinPathGroup.IsUnifiedPure"/> otherwise an <see cref="InvalidOperationException"/>
        /// is thrown.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="embeddedIfPossible">
        /// False to skip an available map and load it from the generated assembly.
        /// By default, the map is searched in available ones before loading the assembly.
        /// </param>
        /// <returns>The map.</returns>
        IStObjMap LoadStObjMap( IActivityMonitor monitor, bool embeddedIfPossible = true );

    }
}
