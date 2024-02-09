using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    public sealed class PocoSerializableServiceEngineImpl : CSCodeGeneratorType, IPocoSerializationServiceEngine
    {
        IPocoTypeSystem? _pocoTypeSystem;
        PocoTypeNameMap? _nameMap;
        int[]? _indexes;
        ITypeScopePart? _filterPart;
        List<(string, IPocoTypeSet)>? _registeredFilters;

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor,
                                                          Type classType,
                                                          ICSCodeGenerationContext c,
                                                          ITypeScope scope )
        {
            scope.Append( "static readonly ExchangeableRuntimeFilter[] _arrayFilter = new ExchangeableRuntimeFilter[] {" ).NewLine()
                 .CreatePart( out _filterPart )
                 .Append("};" ).NewLine();

            // Waiting for .NET 8 https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.immutablecollectionsmarshal.asimmutablearray?view=net-8.0
            scope.Append( "static readonly System.Collections.Immutable.ImmutableArray<ExchangeableRuntimeFilter> _filters = System.Runtime.CompilerServices.Unsafe.As<ExchangeableRuntimeFilter[], System.Collections.Immutable.ImmutableArray<ExchangeableRuntimeFilter>>( ref _arrayFilter );" );

            scope.Append( "public override System.Collections.Immutable.ImmutableArray<ExchangeableRuntimeFilter> RuntimeFilters => _filters;" ).NewLine();

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
            _registeredFilters = null;
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

        bool IPocoSerializationServiceEngine.RegisterExchangeableRuntimeFilter( IActivityMonitor monitor, string name, IPocoTypeSet typeSet )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( name );
            Throw.CheckNotNullArgument( typeSet );

            Throw.DebugAssert( _nameMap != null && _filterPart != null );

            var allSerializable = _nameMap!.TypeSet;
            if( !allSerializable.IsSupersetOf( typeSet ) )
            {
                monitor.Error( $"Error while registering ExchangeableRuntimeFilter named '{name}': its type set contains types that are not in the AllSerializable set." );
                return false;
            }

            var exists = _registeredFilters?.FirstOrDefault( f => name.Equals( f.Item1, StringComparison.OrdinalIgnoreCase ) );
            if( exists.HasValue )
            {
                if( !typeSet.SameContentAs( exists.Value.Item2 ) )
                {
                    monitor.Error( $"Trying to register a ExchangeableRuntimeFilter named '{name}' that is already registered with a different type set." );
                    return false;
                }
                monitor.Warn( $"The ExchangeableRuntimeFilter named '{name}' has already been registered with the same type set." );
                return true;
            }
            _filterPart.Append( "new ExchangeableRuntimeFilter( " )
                       .AppendSourceString( name ).Append( ", " )
                       .AppendArray( typeSet.GetFlagArray() )
                       .Append( " )," ).NewLine();


            _registeredFilters ??= new List<(string, IPocoTypeSet)>();
            _registeredFilters.Add( (name,typeSet) );
            return true;
        }

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
