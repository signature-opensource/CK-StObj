using CK.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public sealed class SampleCommandMemory : ISingletonAutoService
    {
        ConcurrentBag<string> _executionTrace = new ConcurrentBag<string>();

        public void Trace( string message ) => _executionTrace.Add( message );

        public IReadOnlyCollection<string> ExecutionTrace => _executionTrace;
    }
}
