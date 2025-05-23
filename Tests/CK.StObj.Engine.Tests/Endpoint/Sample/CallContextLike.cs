using CK.Core;

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
