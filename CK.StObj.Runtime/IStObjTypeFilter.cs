using System;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// This interface allows type filtering.
    /// </summary>
    public interface IStObjTypeFilter
    {
        /// <summary>
        /// Type filtering can remove any type from the registration phasis.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">Type to accept ot not.</param>
        /// <returns>True to keep the type, false to exclude it.</returns>
        bool TypeFilter( IActivityMonitor monitor, Type t );
    }
}
