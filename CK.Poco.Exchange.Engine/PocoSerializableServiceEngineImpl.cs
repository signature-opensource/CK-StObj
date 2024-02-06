using CK.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Setup
{
    public sealed class PocoSerializableServiceEngineImpl : ICSCodeGenerator, IPocoSerializableServiceEngine
    {
        IPocoTypeSystemBuilder? _pocoTypeSystem;
        PocoTypeNameMap? _nameMap;
        IPocoTypeNameMap? _exchangeMap;

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext ctx )
        {
            _pocoTypeSystem = ctx.CurrentRun.ServiceContainer.GetRequiredService<IPocoTypeSystemBuilder>();
            // Wait for the type system to be locked.
            return new CSCodeGenerationResult( nameof( WaitForLockedTypeSystem ) );
        }

        CSCodeGenerationResult WaitForLockedTypeSystem( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystemBuilder typeSystemBuilder )
        {
            if( !typeSystemBuilder.IsLocked )
            {
                return new CSCodeGenerationResult( nameof( WaitForLockedTypeSystem ) );
            }
            monitor.Trace( $"PocoTypeSystemBuilder is locked: Registering the IPocoSerializableServiceEngine. Serialization code generation can start." );
            var sets = typeSystemBuilder.Lock( monitor ).SetManager;
            _nameMap = new PocoTypeNameMap( sets.AllSerializable );
            _exchangeMap = sets.AllSerializable != sets.AllExchangeable
                            ? new ExMap( _nameMap )
                            : _nameMap;
            c.CurrentRun.ServiceContainer.Add<IPocoSerializableServiceEngine>( this );
            return CSCodeGenerationResult.Success;
        }

        sealed class ExMap : IPocoTypeNameMap
        {
            readonly PocoTypeNameMap _names;
            readonly IPocoTypeSet _set;

            public ExMap( PocoTypeNameMap names )
            {
                _names = names;
                _set = _names.TypeSystem.SetManager.AllExchangeable;
            }

            public IPocoTypeSet TypeSet => _set;

            public IPocoTypeSystem TypeSystem => _set.TypeSystem;

            public string GetName( IPocoType type )
            {
                if( !_set.Contains( type ) )
                {
                    Throw.ArgumentException( nameof( type ), $"Poc type '{type}' is not exchangeable." );
                }
                return _names.GetName( type );
            }
        }

        PocoTypeNameMap IPocoSerializableServiceEngine.SerializableNames => _nameMap!;

        IPocoTypeNameMap IPocoSerializableServiceEngine.ExchangeableNames => _exchangeMap!;
    }
}
