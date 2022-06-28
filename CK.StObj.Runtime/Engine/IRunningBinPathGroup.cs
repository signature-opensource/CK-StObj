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
        /// Can be this configuration that has been chosen (it is always this if no other BinPath are similar to this one).
        /// </summary>
        IRunningBinPathConfiguration Configuration { get; }

        /// <summary>
        /// Gets this and other configurations that are similar.
        /// </summary>
        IReadOnlyCollection<IRunningBinPathConfiguration> SimilarConfigurations { get; }


        /// <summary>
        /// Gets the SHA1 for this BinPath. All <see cref="SimilarConfigurations"/> share the same SHA1.
        /// </summary>
        SHA1Value SignatureCode { get; }
    }
}
