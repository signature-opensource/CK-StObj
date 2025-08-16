using CK.Core;

namespace CK.Engine.TypeCollector;

public sealed partial class GlobalTypeCache
{
    public sealed class WellKnownTypes
    {
        readonly GlobalTypeCache _cache;
        ICachedType? _void;
        ICachedType? _iRealObject;
        ICachedType? _iPoco;
        ICachedType? _iAutoService;
        ICachedType? _task;
        ICachedType? _genericTaskDefinition;
        ICachedType? _valueTask;
        ICachedType? _genericValueTaskDefinition;

        internal WellKnownTypes( GlobalTypeCache cache )
        {
            _cache = cache;
        }

        public ICachedType Void => _void ??= _cache.Get( typeof( void ) );
        public ICachedType IRealObject => _iRealObject ??= _cache.Get( typeof( IRealObject ) );
        public ICachedType IPoco => _iPoco ??= _cache.Get( typeof( IPoco ) );
        public ICachedType IAutoService => _iAutoService ??= _cache.Get( typeof( IAutoService ) );
        public ICachedType Task => _task ??= _cache.Get( typeof( System.Threading.Tasks.Task ) );
        public ICachedType GenericTaskDefinition => _genericTaskDefinition ??= _cache.Get( typeof( System.Threading.Tasks.Task<> ) );
        public ICachedType ValueTask => _valueTask ??= _cache.Get( typeof( System.Threading.Tasks.ValueTask ) );
        public ICachedType GenericValueTaskDefinition => _genericValueTaskDefinition ??= _cache.Get( typeof( System.Threading.Tasks.ValueTask<> ) );
    }
}
