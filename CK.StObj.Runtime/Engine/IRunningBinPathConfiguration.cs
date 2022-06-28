using CK.Core;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="BinPathConfiguration"/> for the engine.
    /// </summary>
    public interface IRunningBinPathConfiguration
    {
        /// <inheritdoc cref="BinPathConfiguration.AspectConfigurations"/>
        IReadOnlyList<XElement> AspectConfigurations { get; }

        /// <inheritdoc cref="BinPathConfiguration.Assemblies"/>
        HashSet<string> Assemblies { get; }

        /// <inheritdoc cref="BinPathConfiguration.CompileOption"/>
        CompileOption CompileOption { get; }

        /// <inheritdoc cref="BinPathConfiguration.ExcludedTypes"/>
        HashSet<string> ExcludedTypes { get; }

        /// <inheritdoc cref="BinPathConfiguration.GenerateSourceFiles"/>
        bool GenerateSourceFiles { get; }

        /// <inheritdoc cref="BinPathConfiguration.Name"/>
        string? Name { get; }

        /// <inheritdoc cref="BinPathConfiguration.OutputPath"/>
        NormalizedPath OutputPath { get; }

        /// <inheritdoc cref="BinPathConfiguration.Path"/>
        NormalizedPath Path { get; }

        /// <inheritdoc cref="BinPathConfiguration.ProjectPath"/>
        NormalizedPath ProjectPath { get; }

        /// <inheritdoc cref="BinPathConfiguration.Types"/>
        List<BinPathConfiguration.TypeConfiguration> Types { get; }

        /// <inheritdoc cref="BinPathConfiguration.GetAspectConfiguration(string)"/>
        XElement? GetAspectConfiguration( string aspectName );

        /// <inheritdoc cref="BinPathConfiguration.GetAspectConfiguration(Type)"/>
        XElement? GetAspectConfiguration( Type aspect );

        /// <inheritdoc cref="BinPathConfiguration.GetAspectConfiguration{T}()"/>
        XElement? GetAspectConfiguration<T>();

        /// <inheritdoc cref="BinPathConfiguration.ToXml"/>
        XElement ToXml();

        /// <summary>
        /// Gets whether this <see cref="IRunningBinPathConfiguration"/> is the purely unified one.
        /// When true, no similar configuration exist.
        /// <para>
        /// This unified BinPath has not the same Assemblies, ExcludedTypes and Types configurations as any of the actual BinPaths.
        /// This BinPath is only used to create an incomplete primary StObjMap (without AutoService resolution) that will
        /// contain all the IPoco and IRealObject. Generating the code for this BinPath will impact the real world with
        /// the unified types from all the BinPaths but this code will never be used.
        /// </para>
        /// </summary>
        bool IsUnifiedPure { get; }

        /// <summary>
        /// Gets the group of <see cref="IRunningBinPathConfiguration"/> similar to this one (including this one).
        /// </summary>
        IRunningBinPathGroup Group { get; }
    }
}
