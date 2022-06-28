using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    public sealed class RunningBinPathGroup : IRunningBinPathGroup
    {
        internal RunningBinPathGroup( RunningBinPathConfiguration head, RunningBinPathConfiguration[] similars, SHA1Value sha )
        {
            Configuration = head;
            SimilarConfigurations = similars;
            SignatureCode = sha;
        }

        /// <summary>
        /// Gets the first configuration in the <see cref="SimilarConfigurations"/>.
        /// Can be this configuration that has been chosen (it is always this if no other BinPath are similar to this one).
        /// </summary>
        public RunningBinPathConfiguration Configuration { get; }

        /// <summary>
        /// Gets this and other configurations that are similar.
        /// </summary>
        public IReadOnlyCollection<RunningBinPathConfiguration> SimilarConfigurations { get; }

        /// <inheritdoc />
        public SHA1Value SignatureCode { get; internal set; }

        IRunningBinPathConfiguration IRunningBinPathGroup.Configuration => Configuration;

        IReadOnlyCollection<IRunningBinPathConfiguration> IRunningBinPathGroup.SimilarConfigurations => SimilarConfigurations;

    }

}
