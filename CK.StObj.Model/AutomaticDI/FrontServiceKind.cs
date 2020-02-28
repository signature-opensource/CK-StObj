using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Defines front-related aspects of services.
    /// </summary>
    [Flags]
    public enum FrontServiceKind
    {
        /// <summary>
        /// The service is not a Front one.
        /// </summary>
        None = 0,

        /// <summary>
        /// The service is bound to the End Point process.
        /// </summary>
        IsProcess = 1,

        /// <summary>
        /// The service is bound to the end point.
        /// This flag implies <see cref="IsProcess"/>
        /// </summary>
        IsEndPoint = 2,

        /// <summary>
        /// The service has an associated marchaller: thanks to this marshalling, this service
        /// is no more "front only".
        /// This flag implies <see cref="IsProcess"/>
        /// </summary>
        IsMarshallable = 4

    }
}
