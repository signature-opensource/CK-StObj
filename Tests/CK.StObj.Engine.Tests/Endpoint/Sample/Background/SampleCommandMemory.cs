using CK.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// Simple singleton that captures string traces.
/// </summary>
public sealed class SampleCommandMemory : ISingletonAutoService
{
    ConcurrentBag<string> _executionTrace = new ConcurrentBag<string>();

    public void Trace( string message ) => _executionTrace.Add( message );

    public IReadOnlyCollection<string> ExecutionTrace => _executionTrace;
}
