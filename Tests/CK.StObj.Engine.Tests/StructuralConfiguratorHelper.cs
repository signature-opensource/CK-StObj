using System;
using CK.Setup;
using CK.Core;

namespace CK.StObj.Engine.Tests;

class StructuralConfiguratorHelper : IStObjStructuralConfigurator
{
    readonly Action<IStObjMutableItem>? _conf;

    public StructuralConfiguratorHelper( Action<IStObjMutableItem>? conf = null )
    {
        _conf = conf;
    }

    public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
    {
        _conf?.Invoke( o );
    }
}
