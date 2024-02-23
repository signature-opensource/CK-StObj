using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Core
{

    /// <summary>
    /// The Poco directory registers the <see cref="IPocoFactory"/> by their
    /// <see cref="IPocoFactory.Name"/> and <see cref="IPocoFactory.PreviousNames"/>.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.PocoDirectoryImpl, CK.StObj.Engine" )]
    public abstract class PocoDirectory : IRealObject
    {
        /// <summary>
        /// Gets a factory thanks to one of its names.
        /// </summary>
        /// <param name="name">The Poco name.</param>
        /// <returns>The factory or null if not found.</returns>
        public abstract IPocoFactory? Find( string name );

        /// <summary>
        /// Gets a factory from a IPoco interface.
        /// </summary>
        /// <param name="pocoInterface">The Poco interface.</param>
        /// <returns>The factory or null if not found.</returns>
        public abstract IPocoFactory? Find( Type pocoInterface );

        /// <summary>
        /// Gets a typed factory from a IPoco interface.
        /// </summary>
        /// <typeparam name="T">The IPoco interface type.</typeparam>
        /// <returns>The factory or null if not found.</returns>
        public IPocoFactory<T>? Find<T>() where T : class, IPoco => (IPocoFactory<T>?)Find( typeof( T ) );

    }
}
