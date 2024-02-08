using CK.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Setup
{
    public sealed class PocoSerializableServiceEngineImpl : ICSCodeGenerator, IPocoSerializationServiceEngine
    {
        IPocoTypeSystem? _pocoTypeSystem;
        PocoTypeNameMap? _nameMap;
        int[]? _indexes;

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext ctx )
        {
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
            _indexes = null;
            _pocoTypeSystem = typeSystemBuilder.Lock( monitor );
            _nameMap = new PocoTypeNameMap( _pocoTypeSystem.SetManager.AllSerializable );
            c.CurrentRun.ServiceContainer.Add<IPocoSerializationServiceEngine>( this );
            return CSCodeGenerationResult.Success;
        }

        IPocoTypeSystem IPocoSerializationServiceEngine.TypeSystem => _pocoTypeSystem!;

        IPocoTypeNameMap IPocoSerializationServiceEngine.SerializableNames => _nameMap!;

        IPocoTypeSet IPocoSerializationServiceEngine.AllSerializable => _nameMap!.TypeSet;

        IPocoTypeSet IPocoSerializationServiceEngine.AllExchangeable => _pocoTypeSystem!.SetManager.AllExchangeable;

        int IPocoSerializationServiceEngine.GetSerializableIndex( IPocoType t )
        {
            Throw.CheckNotNullArgument( t );
            _indexes ??= CreateIndexes();
            int idx = _indexes[t.Index >> 1];
            if( idx == 0 ) Throw.ArgumentException( $"Poco type '{t}' is not serializable." );
            return t.IsNullable ? -idx : 0;
        }

        int[] CreateIndexes()
        {
            Throw.DebugAssert( _pocoTypeSystem != null );
            var indexes = new int[_pocoTypeSystem.AllNonNullableTypes.Count];
            // Skip the 0 by preindexing i. 
            int i = 0;
            foreach( var type in _pocoTypeSystem.SetManager.AllSerializable.NonNullableTypes )
            {
                indexes[type.Index >> 1] = ++i;
            }
            return indexes;
        }

    }
}
