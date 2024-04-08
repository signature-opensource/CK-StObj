using CK.Core;
using CK.Poco.Exc.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.CrisLike
{
    [TestFixture]
    public class CommandJsonSerializationTests
    {
        [ExternalName( "SimpleCommand" )]
        public interface ISimpleCommand : ICommand
        {
            string? SimpleValue { get; set; }
        }

        [ExternalName( "AuthCommand" )]
        public interface IAuthCommand : ICommandAuthUnsafe
        {
            string? SimpleValue { get; set; }
        }

        [ExternalName( "CriticalCommand" )]
        public interface ICriticalCommand : ICommandAuthCritical
        {
            string? SimpleValue { get; set; }
        }

        [ExternalName( "DeviceCommand" )]
        public interface IDeviceCommand : ICommandAuthDeviceId
        {
            string? SimpleValue { get; set; }
        }

        [ExternalName( "FullAuthCommand" )]
        public interface IFullAuthCommand : ICommandAuthDeviceId, ICommandAuthImpersonation, ICommandAuthCritical
        {
            string? SimpleValue { get; set; }
        }

        [ExternalName( "FullAuthCommandWithResult" )]
        public interface IFullAuthCommandWithResult : ICommand<int>, ICommandAuthDeviceId, ICommandAuthImpersonation, ICommandAuthCritical
        {
            string? SimpleValue { get; set; }
        }

        [Test]
        public void command_json_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ),
                                                     typeof( CrisCommandDirectoryLike ),
                                                     typeof( ISimpleCommand ),
                                                     typeof( IAuthCommand ),
                                                     typeof( ICriticalCommand ),
                                                     typeof( IDeviceCommand ),
                                                     typeof( IFullAuthCommand ),
                                                     typeof( IFullAuthCommandWithResult ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            Debug.Assert( services != null );

            TestRoundTrip<ISimpleCommand>( services );
            TestRoundTrip<IAuthCommand>( services );
            TestRoundTrip<ICriticalCommand>( services );
            TestRoundTrip<IDeviceCommand>( services );
            TestRoundTrip<IFullAuthCommand>( services );
            TestRoundTrip<IFullAuthCommandWithResult>( services );

            void TestRoundTrip<T>( IServiceProvider services ) where T : class, ICommand
            {
                var factory = services.GetRequiredService<IPocoFactory<T>>();
                var directory = services.GetRequiredService<PocoDirectory>();

                Debug.Assert( factory != null );

                var cmd = factory.Create();
                // We don't want a common part for this field: use reflection.
                cmd.GetType().GetProperty( "SimpleValue" )!.SetValue( cmd, "Tested Value" );
                if( cmd is ICommandAuthUnsafe auth )
                {
                    auth.ActorId = 3712;
                }
                if( cmd is ICommandAuthDeviceId dev )
                {
                    dev.DeviceId = "The device identifier...";
                }
                if( cmd is ICommandAuthImpersonation imp )
                {
                    imp.ActualActorId = 37123712;
                }

                using var readContext = new PocoJsonReadContext( directory );
                using var writeContext = new PocoJsonWriteContext( directory );

                T ReadFunc( ref Utf8JsonReader r, IUtf8JsonReaderContext ctx )
                {
                    return factory.ReadJson( ref r, (PocoJsonReadContext)ctx )!;
                };

                var cmd2 = TestHelper.JsonIdempotenceCheck( cmd, ( w, c ) => c.WriteJson( w, writeContext ), ReadFunc, readContext );
                Debug.Assert( cmd2 != null );

                cmd2.GetType().GetProperty( "SimpleValue" )!.GetValue( cmd2 ).Should().Be( "Tested Value" );
                if( cmd is ICommandAuthUnsafe )
                {
                    ((ICommandAuthUnsafe)cmd2).ActorId.Should().Be( 3712 );
                }
                if( cmd is ICommandAuthDeviceId )
                {
                    ((ICommandAuthDeviceId)cmd2).DeviceId.Should().Be( "The device identifier..." );
                }
                if( cmd is ICommandAuthImpersonation )
                {
                    ((ICommandAuthImpersonation)cmd2).ActualActorId.Should().Be( 37123712 );
                }
            }
        }
    }
}
