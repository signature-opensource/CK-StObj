using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    sealed partial class PocoSerializableServiceEngineImpl : ICSCodeGenerator, IPocoSerializationServiceEngine
    {
        ITypeScope? _pocoDirectory;
        IPocoTypeSystem? _pocoTypeSystem;
        PocoTypeNameMap? _nameMap;
        string? _getExchangeableRuntimeFilterFuncName;
        int[]? _indexes;
        ITypeScopePart? _filterPart;
        List<(string, IPocoTypeSet)>? _registeredFilters;

        /// <inheritdoc />
        /// <remarks>
        /// Starting point that creates the PocoDirectory_CK type and then <see cref="WaitForLockedTypeSystem"/>.
        /// </remarks>
        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
        {
            _pocoDirectory = c.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
            _pocoDirectory.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.IPocoDirectoryExchangeGenerated" ) );

            _getExchangeableRuntimeFilterFuncName = _pocoDirectory.FullName + ".GetExchangeableRuntimeFilter";
            using( _pocoDirectory.Region() )
            {
                _pocoDirectory.Append( "public static readonly ExchangeableRuntimeFilter[] _exRTFilter = new ExchangeableRuntimeFilter[] {" ).NewLine()
                             .CreatePart( out _filterPart )
                             .Append( "};" ).NewLine();

                _pocoDirectory.Append( "public static ExchangeableRuntimeFilter GetExchangeableRuntimeFilter( string name )" )
                             .OpenBlock()
                             .Append( "foreach( var f in _exRTFilter ) if( f.Name == name ) return f;" ).NewLine()
                             .Append( @"return Throw.ArgumentException<ExchangeableRuntimeFilter>( nameof(name), $""ExchangeableRuntimeFilter named '{name}' not found. Availables are: {_exRTFilter.Select( f => f.Name ).Concatenate()}."" );" )
                             .CloseBlock();

                _pocoDirectory.Append( "IReadOnlyCollection<ExchangeableRuntimeFilter> IPocoDirectoryExchangeGenerated.RuntimeFilters => _exRTFilter;" ).NewLine()
                             .Append( "ExchangeableRuntimeFilter IPocoDirectoryExchangeGenerated.GetRuntimeFilter( string name ) => GetExchangeableRuntimeFilter( name );" ).NewLine();
            }

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
            _registeredFilters = new List<(string, IPocoTypeSet)>();
            _indexes = null;
            // Gets the type system by locking again the builder.
            _pocoTypeSystem = typeSystemBuilder.Lock( monitor );
            _nameMap = new PocoTypeNameMap( _pocoTypeSystem.SetManager.AllSerializable );

            // Generate the "AllSerializable" and "AllExchangeable" runtime type filter.
            RegisterExchangeableRuntimeFilter( monitor, "AllSerializable", _pocoTypeSystem.SetManager.AllSerializable );
            RegisterExchangeableRuntimeFilter( monitor, "AllExchangeable", _pocoTypeSystem.SetManager.AllExchangeable );

            c.CurrentRun.ServiceContainer.Add<IPocoSerializationServiceEngine>( this );
            return CSCodeGenerationResult.Success;
        }

        IPocoTypeSystem IPocoSerializationServiceEngine.TypeSystem => _pocoTypeSystem!;

        IPocoTypeNameMap IPocoSerializationServiceEngine.SerializableNames => _nameMap!;

        IPocoTypeSet IPocoSerializationServiceEngine.AllSerializable => _nameMap!.TypeSet;

        IPocoTypeSet IPocoSerializationServiceEngine.AllExchangeable => _pocoTypeSystem!.SetManager.AllExchangeable;

        string IPocoSerializationServiceEngine.GetExchangeableRuntimeFilterStaticFunctionName => _getExchangeableRuntimeFilterFuncName!;

        /// <inheritdoc/>
        public bool RegisterExchangeableRuntimeFilter( IActivityMonitor monitor, string name, IPocoTypeSet typeSet )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( name );
            Throw.CheckNotNullArgument( typeSet );

            Throw.DebugAssert( _nameMap != null && _registeredFilters != null && _filterPart != null );

            var allSerializable = _nameMap!.TypeSet;
            if( !allSerializable.IsSupersetOf( typeSet ) )
            {
                monitor.Error( $"Error while registering ExchangeableRuntimeFilter named '{name}': its type set contains types that are not in the AllSerializable set." );
                return false;
            }

            var exists = _registeredFilters.FirstOrDefault( f => name.Equals( f.Item1, StringComparison.OrdinalIgnoreCase ) );
            if( exists.Item1 != null  )
            {
                if( !typeSet.SameContentAs( exists.Item2 ) )
                {
                    monitor.Error( $"Trying to register a ExchangeableRuntimeFilter named '{name}' that is already registered with a different type set." );
                    return false;
                }
                monitor.Warn( $"The ExchangeableRuntimeFilter named '{name}' has already been registered with the same type set." );
                return true;
            }
            _filterPart.Append( "new ExchangeableRuntimeFilter( " )
                       .AppendSourceString( name ).Append( ", " )
                       .AppendArray( typeSet.FlagArray )
                       .Append( " )," ).NewLine();

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
