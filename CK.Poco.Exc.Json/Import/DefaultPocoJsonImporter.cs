using CK.Core;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Poco.Exc.Json;

/// <summary>
/// Singleton auto service IPoco importer with <see cref="PocoJsonImportOptions.Default"/>.
/// Its <see cref="ProtocolName"/> is "Json".
/// </summary>
public sealed class DefaultPocoJsonImporter : IPocoImporter
{
    readonly PocoDirectory _pocoDirectory;

    /// <summary>
    /// Returns "Json".
    /// </summary>
    public string ProtocolName => "Json";

    /// <summary>
    /// Initializes a new "Json" <see cref="DefaultPocoJsonImporter"/> that uses <see cref="PocoJsonImportOptions.Default"/>.
    /// </summary>
    /// <param name="pocoDirectory">The poco directory.</param>
    public DefaultPocoJsonImporter( PocoDirectory pocoDirectory )
    {
        _pocoDirectory = pocoDirectory;
    }

    /// <inheritdoc />
    public bool TryRead( IActivityMonitor monitor, ReadOnlySequence<byte> input, out IPoco? data )
    {
        try
        {
            data = _pocoDirectory.ReadJson( input, PocoJsonImportOptions.Default );
            return true;
        }
        catch( Exception ex )
        {
            monitor.Error( $"While trying to read a Poco in Json.", ex );
            data = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryRead( IActivityMonitor monitor, Stream input, out IPoco? data )
    {
        try
        {
            data = _pocoDirectory.ReadJson( input, PocoJsonImportOptions.Default );
            return true;
        }
        catch( Exception ex )
        {
            monitor.Error( $"While trying to read a Poco in Json.", ex );
            data = null;
            return false;
        }
    }

    /// <inheritdoc />
    public Task<(bool Success, IPoco? Data)> TryReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel )
    {
        if( TryRead( monitor, input, out var data ) )
        {
            return Task.FromResult( (true, data) );
        }
        return Task.FromResult( (false, (IPoco?)null) );
    }

    /// <inheritdoc />
    public IPoco? Read( IActivityMonitor monitor, Stream input )
    {
        return _pocoDirectory.ReadJson( input, PocoJsonImportOptions.Default );
    }

    /// <inheritdoc />
    public Task<IPoco?> ReadAsync( IActivityMonitor monitor, Stream input, CancellationToken cancel )
    {
        return Task.FromResult( _pocoDirectory.ReadJson( input, PocoJsonImportOptions.Default ) );
    }
}
