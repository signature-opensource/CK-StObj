using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests.CrisLike
{
    /// <summary>
    /// Extends the basic <see cref="IAuthenticatedCommandPart"/> to add the <see cref="DeviceId"/> field.
    /// </summary>
    [CKTypeDefiner]
    public interface IAuthenticatedDeviceCommandPart : IAuthenticatedCommandPart
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the current <see cref="IAuthenticationInfo.DeviceId"/>.
        /// </summary>
        string? DeviceId { get; set; }
    }
}
