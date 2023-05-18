using System;
using System.Collections.Generic;

namespace CK.Setup
{
    public interface IEndpointResult
    {
        IReadOnlyList<IEndpointContext> EndpointContexts { get; }
        IEnumerable<Type> EndpointServices { get; }

        bool IsEndpointService( Type type );
    }
}
