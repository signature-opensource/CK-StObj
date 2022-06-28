using CK.Core;
using System.Collections.Generic;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="BinPathConfiguration"/> for the engine.
    /// </summary>
    public sealed class RunningBinPathConfiguration : BinPathConfiguration, IRunningBinPathConfiguration
    {
        internal RunningBinPathConfiguration()
        {
        }

        internal RunningBinPathConfiguration( XElement e )
            : base( e )
        {
        }

        public RunningBinPathGroup Group { get; internal set; }

        /// <inheritdoc />
        public bool IsUnifiedPure { get; internal set; }

        IReadOnlyList<XElement> IRunningBinPathConfiguration.AspectConfigurations => AspectConfigurations;

        IRunningBinPathGroup IRunningBinPathConfiguration.Group => Group;
    }
}
