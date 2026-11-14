using CK.Core;
using CK.Poco.Exc.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace CK.BinarySerialization;

/// <summary>
/// Registry for <see cref="IPoco"/> deserializers.
/// <para>
/// This resolver caches its drivers for the first PocoDirectory it gets from
/// the <see cref="BinaryDeserializerContext.Services"/>. This should cover
/// the vast majority of runs since in practice there's one and only one PocoDirectory
/// in a process/domain.
/// </para>
/// </summary>
public sealed class PocoDeserializerResolver : IDeserializerResolver
{
    /// <summary>
    /// Gets the registry.
    /// </summary>
    public static readonly PocoDeserializerResolver Instance = new PocoDeserializerResolver();

    PocoDeserializerResolver() { }

    static PocoDirectory? _winner;
    static readonly ConcurrentDictionary<Type, IDeserializationDriver?> _winnerDrivers = new ConcurrentDictionary<Type, IDeserializationDriver?>();

    sealed class PocoDeserializerDriver<T> : SimpleReferenceTypeDeserializer<T> where T : class, IPoco
    {
        readonly IPocoFactory<T> _factory;

        public PocoDeserializerDriver( IPocoFactory<T> factory, bool isCached )
            : base( isCached )
        {
            _factory = factory;
        }

        protected override T ReadInstance( ICKBinaryReader r, ITypeReadInfo readInfo )
        {
            var len = r.ReadNonNegativeSmallInt32();
            var buffer = ArrayPool<byte>.Shared.Rent( len );
            try
            {
                // Must loop because streams like GZipStream may return fewer bytes than requested.
                // See: https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/partial-byte-reads-in-streams
                int totalRead = 0;
                while( totalRead < len )
                {
                    int bytesRead = r.Read( buffer, totalRead, len - totalRead );
                    if( bytesRead == 0 )
                        Throw.EndOfStreamException( $"Unexpected end of stream. Expected {len} bytes, got {totalRead}." );
                    totalRead += bytesRead;
                }
                return _factory.ReadJson( buffer.AsSpan( 0, len ), PocoJsonImportOptions.ToStringDefault )!;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return( buffer );
            }
        }
    }

    /// <inheritdoc />
    public IDeserializationDriver? TryFindDriver( ref DeserializerResolverArg info )
    {
        if( info.DriverName == "IPocoJson" && typeof( IPoco ).IsAssignableFrom( info.ExpectedType ) )
        {
            var d = info.Context.Services.GetRequiredService<PocoDirectory>();
            var firstDirectory = Interlocked.CompareExchange( ref _winner, d, null );
            if( firstDirectory == null || firstDirectory == d )
            {
                return _winnerDrivers.GetOrAdd( info.ExpectedType, CreateCached );
            }
            var factory = d.Find( info.ExpectedType );
            if( factory == null ) return null;
            var tV = typeof( PocoDeserializerDriver<> ).MakeGenericType( info.ExpectedType );
            return (IDeserializationDriver)Activator.CreateInstance( tV, factory, false )!;
        }
        return null;
    }

    static IDeserializationDriver? CreateCached( Type t )
    {
        Debug.Assert( _winner != null );
        var factory = _winner.Find( t );
        if( factory == null ) return null;
        var tV = typeof( PocoDeserializerDriver<> ).MakeGenericType( t );
        return (IDeserializationDriver)Activator.CreateInstance( tV, factory, true )!;
    }

}
