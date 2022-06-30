using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    public interface IRunningBinPathGroup
    {
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
        public bool IsUnifiedPure { get; }

        /// <summary>
        /// Gets this and other configurations that are similar.
        /// </summary>
        IReadOnlyCollection<BinPathConfiguration> SimilarConfigurations { get; }

        /// <summary>
        /// Gets the SHA1 for this BinPath. All <see cref="SimilarConfigurations"/> share the same SHA1.
        /// <para>
        /// The <see cref="IStObjEngineRunContext.PrimaryBinPath"/> always uses the <see cref="StObjEngineConfiguration.BaseSHA1"/>.
        /// </para>
        /// </summary>
        SHA1Value SignatureCode { get; }

        /// <summary>
        /// Gets the name (or comma separated names) of the <see cref="SimilarConfigurations"/>.
        /// </summary>
        string Names { get; }

        /// <summary>
        /// Gets whether at least one <see cref="BinPathConfiguration.GenerateSourceFiles"/> in <see cref="SimilarConfigurations"/> is true
        /// and the <see cref="GeneratedSource"/> is not already up to date.
        /// </summary>
        bool SaveSource { get; }

        /// <summary>
        /// Gets whether the generated source code must be parsed and/or compiled.
        /// This is by default the max of <see cref="BinPathConfiguration.CompileOption"/> from the <see cref="SimilarConfigurations"/>
        /// but can be set to <see cref="CompileOption.None"/> if <see cref="GeneratedAssembly"/> is already up to date.
        /// </summary>
        CompileOption CompileOption { get; }

        /// <summary>
        /// Gets the assembly file path.
        /// It will be generated only if <see cref="CompileOption"/> is <see cref="CompileOption.Compile"/>.
        /// </summary>
        GeneratedFileArtifactWithTextSignature GeneratedAssembly { get; }

        /// <summary>
        /// Gets the source file path.
        /// It will be generated only if <see cref="SaveSource"/> is true.
        /// </summary>
        GeneratedG0Artifact GeneratedSource { get; }
    }
}
