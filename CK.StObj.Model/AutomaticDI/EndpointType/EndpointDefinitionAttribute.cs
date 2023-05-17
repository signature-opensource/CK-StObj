using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    public sealed class EndpointDefinitionAttribute : ContextBoundDelegationAttribute
    {
        public EndpointDefinitionAttribute( params Type[] handledTypes )
            : base( "CK.Setup.EndpointDefinitionImpl, CK.StObj.Engine" )
        {
            HandledTypes = handledTypes;
        }

        public IReadOnlyList<Type> HandledTypes { get; }
    }
}
