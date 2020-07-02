using CK.Setup;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// The Poco directory registers the <see cref="IPocoFactory"/> by their
    /// <see cref="IPocoFactory.Name"/> and <see cref="IPocoFactory.PreviousNames"/>.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.PocoSourceGenerator, CK.StObj.Engine" )]
    public abstract class PocoDirectory : IRealObject
    {
        /// <summary>
        /// Gets a factory thanks to one of its names.
        /// </summary>
        /// <param name="name">The Poco name.</param>
        /// <returns>The factory or null if not found.</returns>
        public abstract IPocoFactory? Find( string name );

    }
}
