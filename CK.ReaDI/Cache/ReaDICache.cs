using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core;

public sealed class ReaDICache
{
    readonly Dictionary<ICachedType, HandlerType> _handlerTypes;
    readonly Dictionary<ICachedType, ParameterType> _parameters;
    readonly GlobalTypeCache _typeCache;
    readonly ParameterType _pActivityMonitor;
    readonly ParameterType _pReaDIEngine;


    public ReaDICache( GlobalTypeCache typeCache )
    {
        _typeCache = typeCache;
        _pActivityMonitor = new ParameterType( _typeCache.KnownTypes.IActivityMonitor );
        _pReaDIEngine = new ParameterType( _typeCache.Get( typeof( ReaDIEngine ) ) );
    }

    public ParameterType ActivityMonitorParameter => _pActivityMonitor;

    public ParameterType ReaDIEngineParameter => _pReaDIEngine;

    public GlobalTypeCache TypeCache => _typeCache;
}
