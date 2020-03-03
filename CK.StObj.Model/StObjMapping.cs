using System;

namespace CK.Core
{
    /// <summary>
    /// Captures mapping in a <see cref="IStObjObjectMap"/>: associates a <see cref="IStObj"/> to its final implementation.
    /// </summary>
    public readonly struct StObjMapping
    {
        /// <summary>
        /// The StObj slice.
        /// </summary>
        public readonly IStObj StObj;

        /// <summary>
        /// The final implementation instance.
        /// </summary>
        public readonly IStObjFinalImplementation FinalImplementation;

        /// <summary>
        /// Initializes a new association between a <see cref="IStObj"/>
        /// and its final implementation.
        /// </summary>
        /// <param name="o">The StObj.</param>
        /// <param name="i">The implementation.</param>
        public StObjMapping( IStObj o, IStObjFinalImplementation i )
        {
            if( o == null ) throw new ArgumentNullException( nameof( o ) );
            if( i == null ) throw new ArgumentNullException( nameof( i ) );
            StObj = o;
            FinalImplementation = i;
        }
    }

}
