using System;
using CK.Setup;
using CK.Core;

namespace CK.StObj.Engine.Tests
{
    class StructuralConfiguratorHelper : IStObjTypeFilter, IStObjStructuralConfigurator
    {
        readonly Action<IStObjMutableItem> _conf;
        readonly Func<Type,bool> _typeFilter;

        public StructuralConfiguratorHelper( Action<IStObjMutableItem> conf = null, Func<Type, bool> typefilter = null )
        {
            _conf = conf;
            _typeFilter = typefilter;
        }

        public bool TypeFilter( IActivityMonitor monitor, Type t )
        {
            return _typeFilter?.Invoke( t ) ?? true;
        }

        public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
        {
            _conf?.Invoke( o );
        }
    }
}
