using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    public sealed class EndpointDefinitionAttribute : ContextBoundDelegationAttribute
    {
        public EndpointDefinitionAttribute()
            : base( "CK.Setup.EndpointDefinitionImpl, CK.StObj.Engine" )
        {
        }
    }
}
