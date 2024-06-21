
using CK.Core;

namespace CK.Setup
{

    public static class EngineConfigurationExtensions
    {

        public static EngineResult Run( this EngineConfiguration configuration, IActivityMonitor monitor )
        {
            if( !RunningEngineConfiguration.PrepareConfiguration( monitor, configuration.Configuration ) )
            {
                return new EngineResult( false, configuration );
            }

        }

    }
}
