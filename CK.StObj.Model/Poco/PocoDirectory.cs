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

        /// <summary>
        /// Gets a unique index of a registered final compliant Poco type. The "Final Types" is a subset of the
        /// non nullable registered Poco compliant types that are concrete C# types.
        /// <para>
        /// A final type can be based on oblivious types that are not final: <c>List&lt;object&gt;</c> or <c>List&lt;int?&gt;</c> are final
        /// types even if <c>object</c> and <c>int?</c> are not.
        /// </para>
        /// </summary>
        /// <param name="t">The type to challenge. A nullable value type (<see cref="Nullable{T}"/>) will not be found.</param>
        /// <returns>A zero or positive index if the type is a registered, -1 otherwise.</returns>
        public abstract int GetNonNullableFinalTypeIndex( Type t );

    }
}
