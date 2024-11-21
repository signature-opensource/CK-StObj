using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.StObj.Engine.Tests.Endpoint;

public interface ICallContextLike : IScopedAutoService
{
}

public class CallContextLike : ICallContextLike
{

}

public interface ITransactionalCallContextLike : ICallContextLike
{
}


public class TransactionalCallContextLike : CallContextLike, ITransactionalCallContextLike
{
}
