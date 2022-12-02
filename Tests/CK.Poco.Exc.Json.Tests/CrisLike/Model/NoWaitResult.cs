using System;
using System.Collections.Generic;
using System.Text;

namespace CK.CrisLike
{
    /// <summary>
    /// Type marker for result of a fire &amp; forget command.
    /// </summary>
    public sealed class NoWaitResult
    {
        /// <summary>
        /// Gets a singleton instance of this marker type.
        /// </summary>
        public static NoWaitResult Instance = new NoWaitResult();

        NoWaitResult() {}
    }

}
