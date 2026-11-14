using CK.BinarySerialization;
using CK.Testing;
using Shouldly;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using static CK.Testing.MonitorTestHelper;

namespace CK.Core;

static class TestHelperExtensions
{
    public static bool CheckObjectReferences = true;

    /// <summary>
    /// Gets or sets whether SaveAndLoad methods check the reference management
    /// by writing a first (empty) object before and after the actual write, and yet another
    /// (empty) instance after twice (to use the reference tracking). 
    /// When reading, we first read this empty instance before the actual read, read it again
    /// and reads twice the second instance.
    /// <para>
    /// Defaults to true.
    /// </para>
    /// </summary>
    /// <param name="this">This tester.</param>
    /// <param name="check">Check the references.</param>
    public static void SetCheckObjectReferences( this IMonitorTestHelper @this, bool check )
    {
        CheckObjectReferences = check;
    }


    [return: NotNullIfNotNull( nameof( o ) )]
    public static T SaveAndLoadObject<T>( this IMonitorTestHelper @this, T o,
                                                                         BinarySerializerContext? serializerContext = null,
                                                                         BinaryDeserializerContext? deserializerContext = null ) where T : class
    {
        return SaveAndLoad( @this, o, ( x, w ) => w.WriteObject( x ), r => r.ReadObject<T>(), serializerContext, deserializerContext );
    }

    [return: NotNullIfNotNull( nameof( o ) )]
    public static object? SaveAndLoadAny( this IMonitorTestHelper @this, object? o,
                                                                         BinarySerializerContext? serializerContext = null,
                                                                         BinaryDeserializerContext? deserializerContext = null )
    {
        return SaveAndLoad( @this, o, ( x, w ) => w.WriteAnyNullable( o ), r => r.ReadAnyNullable(), serializerContext, deserializerContext )!;
    }

    [return: NotNullIfNotNull( nameof( o ) )]
    public static T? SaveAnyAndLoad<T>( this IMonitorTestHelper @this, object o,
                                                                       BinarySerializerContext? serializerContext = null,
                                                                       BinaryDeserializerContext? deserializerContext = null )
    {
        return SaveAnyAndLoad( @this, o, r => r.ReadAnyNullable<T>(), serializerContext, deserializerContext )!;
    }

    public static T SaveAndLoadValue<T>( this IMonitorTestHelper @this, in T v,
                                                                        BinarySerializerContext? serializerContext = null,
                                                                        BinaryDeserializerContext? deserializerContext = null ) where T : struct
    {
        return SaveAndLoad<T>( @this, v, ( x, w ) => w.WriteValue( x ), r => r.ReadValue<T>(), serializerContext, deserializerContext );
    }

    public static T? SaveAndLoadNullableValue<T>( this IMonitorTestHelper @this, in T? v,
                                                                                 BinarySerializerContext? serializerContext = null,
                                                                                 BinaryDeserializerContext? deserializerContext = null ) where T : struct
    {
        return SaveAndLoad<T?>( @this, v, ( x, w ) => w.WriteNullableValue( x ), r => r.ReadNullableValue<T>(), serializerContext, deserializerContext );
    }

    public static T SaveAndLoad<T>( this IMonitorTestHelper @this, in T o,
                                                                   Action<T, IBinarySerializer> w,
                                                                   Func<IBinaryDeserializer, T> r,
                                                                   BinarySerializerContext? serializerContext = null,
                                                                   BinaryDeserializerContext? deserializerContext = null )
    {
        try
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
            {
                object o1 = BeforeWrite( writer );
                w( o, writer );
                AfterWrite( writer, o1 );
                s.Position = 0;
                return BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(),
                    d =>
                    {
                        object? r1 = BeforeRead( d );
                        T result = r( d );
                        AfterRead( d, r1 );
                        return result;
                    } )
                    .GetResult();
            }
        }
        catch( Exception ex )
        {
            TestHelper.Monitor.Error( ex );
            throw;
        }
    }

    public static T SaveAnyAndLoad<T>( this IMonitorTestHelper @this, in object o,
                                                                      Func<IBinaryDeserializer, T> r,
                                                                      BinarySerializerContext? serializerContext = null,
                                                                      BinaryDeserializerContext? deserializerContext = null )
    {
        try
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
            {
                object o1 = BeforeWrite( writer );
                writer.WriteAny( o );
                AfterWrite( writer, o1 );
                s.Position = 0;
                return BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(),
                    d =>
                    {
                        object? r1 = BeforeRead( d );
                        T result = r( d );
                        AfterRead( d, r1 );
                        return result;
                    } )
                    .GetResult();
            }
        }
        catch( Exception ex )
        {
            TestHelper.Monitor.Error( ex );
            throw;
        }
    }

    public static void SaveAndLoad( this IMonitorTestHelper @this, Action<IBinarySerializer> w,
                                                                   Action<IBinaryDeserializer> r,
                                                                   BinarySerializerContext? serializerContext = null,
                                                                   BinaryDeserializerContext? deserializerContext = null )
    {
        try
        {
            using( var s = new MemoryStream() )
            using( var writer = BinarySerializer.Create( s, serializerContext ?? new BinarySerializerContext() ) )
            {
                object o1 = BeforeWrite( writer );
                w( writer );
                AfterWrite( writer, o1 );
                s.Position = 0;
                BinaryDeserializer.Deserialize( s, deserializerContext ?? new BinaryDeserializerContext(),
                    d =>
                    {
                        object? r1 = BeforeRead( d );
                        r( d );
                        AfterRead( d, r1 );
                    } )
                    .ThrowOnInvalidResult();
            }
        }
        catch( Exception ex )
        {
            TestHelper.Monitor.Error( ex );
            throw;
        }
    }

    static object BeforeWrite( IBinarySerializer s )
    {
        s.DebugWriteMode( true );

        var o1 = new object();
        if( CheckObjectReferences )
        {
            s.WriteAny( o1 );
        }

        s.DebugWriteSentinel();
        return o1;
    }

    static void AfterWrite( IBinarySerializer s, object o1 )
    {
        s.DebugWriteSentinel();

        if( CheckObjectReferences )
        {
            s.WriteAny( o1 );
            var o2 = new object();
            s.WriteAny( o2 );
            s.WriteAny( o2 );
        }
    }

    static object? BeforeRead( IBinaryDeserializer d )
    {
        d.DebugReadMode();

        object? r1 = null;
        if( CheckObjectReferences )
        {
            r1 = d.ReadAny();
        }
        d.DebugCheckSentinel();
        return r1;
    }

    static void AfterRead( IBinaryDeserializer d, object? r1 )
    {
        d.DebugCheckSentinel();

        if( CheckObjectReferences )
        {
            d.ReadAny().ShouldBeSameAs( r1 );
            var r2 = d.ReadAny();
            r2.ShouldBeOfType<object>();
            d.ReadAny().ShouldBeSameAs( r2 );
        }
    }

}

