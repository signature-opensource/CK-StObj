using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests.CrisLike
{
    /// <summary>
    /// Marker interface for commands that require the <see cref="AuthLevel.Critical"/> level to be validated.
    /// </summary>
    [CKTypeDefiner]
    public interface IAuthenticatedCriticalCommandPart : IAuthenticatedCommandPart
    {
    }
}
