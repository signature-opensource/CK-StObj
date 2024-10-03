using System;
using System.Collections.Generic;
using System.Text;

namespace CK.StObj.Engine.Tests.CrisLike;

public interface ICommandAuthUnsafe : ICommandPart
{
    int ActorId { get; set; }
}
