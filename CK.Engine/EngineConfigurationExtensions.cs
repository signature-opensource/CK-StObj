
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Setup
{

    public static class EngineConfigurationExtensions
    {

        public static EngineResult Run( this EngineConfiguration configuration, IActivityMonitor monitor )
        {
            if( !RunningEngineConfiguration.PrepareConfiguration( monitor, configuration ) )
            {
                return new EngineResult( false, null );
            }

            throw new NotImplementedException();
        }

    }
}
