using CK.Core;
using CK.Poco.Exc.Json;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.BinarySerialization;

/// <summary>
/// Factory for <see cref="IPoco"/> serialization drivers.
/// <para>
/// This currently uses Json serialization (the driver name is "IPocoJson").
/// Drivers are cached at the <see cref="BinarySerializerContext"/> level because
/// everything depends on the <see cref="PocoDirectory"/>.
/// </para>
/// </summary>
public sealed class PocoSerializerResolver : ISerializerResolver
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly PocoSerializerResolver Instance = new PocoSerializerResolver();

    PocoSerializerResolver()
    {
    }

    /// <inheritdoc />
    public ISerializationDriver? TryFindDriver( BinarySerializerContext context, Type t )
    {
        if( !t.IsClass || !typeof( IPoco ).IsAssignableFrom( t ) ) return null;
        var f = context.Services.GetRequiredService<PocoDirectory>().Find( t );
        if( f == null ) return null;
        var tR = typeof( PocoSerializableDriver<> ).MakeGenericType( t );
        return ((ISerializationDriver)Activator.CreateInstance( tR, f )!).Nullable;
    }

    sealed class PocoSerializableDriver<T> : ReferenceTypeSerializer<T>, ISerializationDriverTypeRewriter where T : class, IPoco
    {
        readonly IPocoFactory _factory;

        public PocoSerializableDriver( IPocoFactory factory )
        {
            _factory = factory;
        }

        public override string DriverName => "IPocoJson";

        public override int SerializationVersion => -1;

        public override SerializationDriverCacheLevel CacheLevel => SerializationDriverCacheLevel.Context;

        public Type GetTypeToWrite( Type type ) => _factory.PrimaryInterface;

        protected override void Write( IBinarySerializer s, in T o )
        {
            // We don't write the ["TypeName",{ ... }] envelope since we rely on the rewritten Type
            // that is the primary interface: the type name is free to change and it the type is
            // hooked and a new TargetType is set, this MAY work...
            //
            // We use the ToStringDefault options: Pascal case and JavaScriptEncoder.UnsafeRelaxedJsonEscaping (faster)
            // and more importantly the TypeFilterName is "AllSerializable" (whereas the PocoJsonExportOptions.Default
            // is "AllExchangeable").
            using( var m = Util.RecyclableStreamManager.GetStream() )
            {
                o.WriteJson( m, withType: false, PocoJsonExportOptions.ToStringDefault );
                if( m.Position > int.MaxValue / 2 )
                {
                    Throw.InvalidOperationException( $"Writing '{typeof( T )}' instance requires {m.Position} bytes. This is bigger than the maximal authorized size of int.MaxValue/2 ({int.MaxValue / 2})." );
                }
                s.Writer.WriteNonNegativeSmallInt32( (int)m.Position );
                var bytes = m.GetReadOnlySequence();
                foreach( var b in bytes )
                {
                    s.Writer.Write( b.Span );
                }
            }
        }
    }

}
