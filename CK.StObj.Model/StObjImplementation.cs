using System;

namespace CK.Core
{
    /// <summary>
    /// Captures mapping in a <see cref="IStObjObjectMap"/>: 
    /// associates a <see cref="IStObj"/> to its final implementation.
    /// </summary>
    public readonly struct StObjImplementation
    {
        /// <summary>
        /// The StObj slice.
        /// </summary>
        public readonly IStObj StObj;

        /// <summary>
        /// The final implementation instance.
        /// </summary>
        public readonly object Implementation;

        /// <summary>
        /// Initializes a new association between a <see cref="IStObj"/>
        /// and its implementation.
        /// </summary>
        /// <param name="o">The StObj.</param>
        /// <param name="i">The implementation.</param>
        public StObjImplementation( IStObj o, object i )
        {
            if( o == null ) throw new ArgumentNullException( nameof( o ) );
            if( i == null ) throw new ArgumentNullException( nameof( i ) );
            StObj = o;
            Implementation = i;
        }
    }

}
