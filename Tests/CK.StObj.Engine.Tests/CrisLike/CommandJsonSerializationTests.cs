using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.CrisLike
{
    [TestFixture]
    public class CommandJsonSerializationTests
    {
        [ExternalName( "SimpleCommand" )]
        public interface ISimpleCommand : ICommand
        {
            string? SimpleValue { get; }
        }

        [ExternalName( "AuthCommand" )]
        public interface IAuthCommand : IAuthenticatedCommandPart
        {
            string? SimpleValue { get; }
        }

        [ExternalName( "CriticalAuthCommand" )]
        public interface ICriticalAuthCommand : IAuthenticatedCriticalCommandPart
        {
            string? SimpleValue { get; }
        }

        [ExternalName( "AuthDeviceCommand" )]
        public interface IAuthDeviceCommand : IAuthenticatedDeviceCommandPart
        {
            string? SimpleValue { get; }
        }

        [ExternalName( "FullAuthCommand" )]
        public interface IFullAuthCommand : IAuthenticatedDeviceCommandPart, IAuthenticatedImpersonationCommandPart, IAuthenticatedCriticalCommandPart
        {
            string? SimpleValue { get; }
        }

        [Test]
        public void command_json_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ),
                                                     typeof( CrisCommandDirectoryLike ),
                                                     typeof( ISimpleCommand ),
                                                     typeof( IAuthCommand ),
                                                     typeof( ICriticalAuthCommand ),
                                                     typeof( IAuthDeviceCommand ),
                                                     typeof( IFullAuthCommand ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            Debug.Assert( services != null );

            TestRoundTrip<ISimpleCommand>( services );
            TestRoundTrip<IAuthCommand>( services );
            TestRoundTrip<ICriticalAuthCommand>( services );
            TestRoundTrip<IAuthDeviceCommand>( services );
            TestRoundTrip<IFullAuthCommand>( services );

            void TestRoundTrip<T>( IServiceProvider services ) where T : class, ICommand
            {
                var factory = services.GetRequiredService<IPocoFactory<T>>();
                Debug.Assert( factory != null );

                var cmd = factory.Create();
                // We don't want a common part for this field: use reflection.
                cmd.GetType().GetProperty( "SimpleValue" )!.SetValue( cmd, "Tested Value" );
                if( cmd is IAuthenticatedCommandPart auth )
                {
                    auth.ActorId = 3712;
                }
                if( cmd is IAuthenticatedDeviceCommandPart dev )
                {
                    dev.DeviceId = "The device identifier...";
                }
                if( cmd is IAuthenticatedImpersonationCommandPart imp )
                {
                    imp.ActualActorId = 37123712;
                }

                var cmd2 = PocoJson.PocoJsonTests.Roundtrip( services, cmd );
                Debug.Assert( cmd2 != null );

                cmd2.GetType().GetProperty( "SimpleValue" )!.GetValue( cmd2 ).Should().Be( "Tested Value" );
                if( cmd is IAuthenticatedCommandPart )
                {
                    ((IAuthenticatedCommandPart)cmd2).ActorId.Should().Be( 3712 );
                }
                if( cmd is IAuthenticatedDeviceCommandPart )
                {
                    ((IAuthenticatedDeviceCommandPart)cmd2).DeviceId.Should().Be( "The device identifier..." );
                }
                if( cmd is IAuthenticatedImpersonationCommandPart )
                {
                    ((IAuthenticatedImpersonationCommandPart)cmd2).ActualActorId.Should().Be( 37123712 );
                }
            }
        }
    }
}
