using System;
using System.Collections.Generic;

namespace CK.Setup
{
    public interface IEndpointContext
    {
        IStObjResult EndpointDefinition { get; }
        string Name { get; }
        IReadOnlyList<Type> ScopedServices { get; }
        IReadOnlyList<(Type Service, IEndpointContext? Owner)> SingletonServices { get; }
    }
}
