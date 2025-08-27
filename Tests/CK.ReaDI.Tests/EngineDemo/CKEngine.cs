using CK.Core;
using System.Threading.Tasks;

namespace CK.Demo;

public static class CKEngine
{
    public static async Task<bool> RunAsync( this EngineConfiguration configuration, IActivityMonitor monitor )
    {
        if( !configuration.NormalizeConfiguration( monitor ) )
        {
            return false;
        }
        //var typeGroups = BinPathTypeGroup.Run( monitor, configuration );
        var typeGroups = BinPathTypeGroup.Run( monitor, configuration );

        return true;
    }

}
