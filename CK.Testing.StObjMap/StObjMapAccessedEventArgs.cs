using CK.Core;
using System;

namespace CK.Testing.StObjMap
{
    /// <summary>
    /// Event of <see cref="IStObjMapTestHelperCore.StObjMapAccessed"/>.
    /// This event can be used to challenge external conditions that may require to reload the <see cref="IStObjMapTestHelperCore.StObjMap"/>.
    /// </summary>
    public class StObjMapAccessedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new <see cref="StObjMapAccessedEventArgs"/>.
        /// </summary>
        /// <param name="current">The current StObjMap. Must not be null.</param>
        /// <param name="deltaLastAccess">Time span from last access.</param>
        /// <param name="loadedTime">The current loaded time.</param>
        public StObjMapAccessedEventArgs( IStObjMap current, TimeSpan deltaLastAccess, TimeSpan loadedTime )
        {
            Throw.CheckNotNullArgument( current );
            Current = current;
            DeltaLastAccessTime = deltaLastAccess;
            CurrentLoadedTime = loadedTime;
        }

        /// <summary>
        /// Gets the current StObjMap. Never null.
        /// </summary>
        public IStObjMap Current { get; }

        /// <summary>
        /// Gets or sets whether the <see cref="Current"/> StObjMap should be reset
        /// and a brand new one should be created and loaded.
        /// </summary>
        public bool ShouldReset { get; set; }

        /// <summary>
        /// Gets the time span from the load of the <see cref="Current"/> database.
        /// This can be used to de-bounce operations.
        /// </summary>
        public TimeSpan CurrentLoadedTime { get; }

        /// <summary>
        /// Gets the time span from the last StObjMap access.
        /// This can be used to de-bounce operations.
        /// </summary>
        public TimeSpan DeltaLastAccessTime { get; }
    }
}
