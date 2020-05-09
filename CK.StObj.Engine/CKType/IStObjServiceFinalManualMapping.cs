using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Extends base class factory with the index in the list of the manual services.
    /// </summary>
    interface IStObjServiceFinalManualMapping : IStObjServiceClassFactory
    {
        /// <summary>
        /// Gets the unique number that identifies this factory.
        /// </summary>
        int Number { get; }
    }

}
