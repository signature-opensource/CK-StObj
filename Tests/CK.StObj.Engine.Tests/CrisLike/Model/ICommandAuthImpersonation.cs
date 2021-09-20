using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests.CrisLike
{
    [CKTypeDefiner]
    public interface ICommandAuthImpersonation : ICommandAuthUnsafe
    {
        int ActualActorId { get; set; }
    }
}
