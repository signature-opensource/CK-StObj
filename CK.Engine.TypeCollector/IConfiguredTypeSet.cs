using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Engine.TypeCollector
{
    /// <summary>
    /// Final configuration for types.
    /// <para>
    /// The initial set is the <see cref="AssemblyCache.BinPathGroup.ConfiguredTypes"/> (computed from
    /// the <see cref="AssemblyCache.BinPathGroup.HeadAssemblies"/>).
    /// </para>
    /// <para>
    /// The final set is the <see cref="BinPathTypeGroup.ConfiguredTypes"/> that applied its <see cref="BinPathConfiguration"/>
    /// to the initial one.
    /// </para>
    /// </summary>
    public interface IConfiguredTypeSet
    {
        /// <summary>
        /// Gets all the types that must be registered, including the ones in <see cref="ConfiguredTypes"/>.
        /// </summary>
        IReadOnlySet<Type> AllTypes { get; }

        /// <summary>
        /// Gets the type configurations with a <see cref="TypeConfiguration.Kind"/> that is not <see cref="ConfigurableAutoServiceKind.None"/>.
        /// </summary>
        IReadOnlyCollection<TypeConfiguration> ConfiguredTypes { get; }
    }
}
