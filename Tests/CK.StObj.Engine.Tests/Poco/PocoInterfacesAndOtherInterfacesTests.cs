using CK.Core;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoInterfacesAndOtherInterfacesTests
    {
        [CKTypeDefiner]
        public interface ICommand : IPoco
        {
        }

        [CKTypeSuperDefiner]
        public interface ICommandPart : ICommand
        {
        }

        public interface ICommandAuthUnsafe : ICommandPart
        {
        }

        [CKTypeDefiner]
        public interface ICommandAuthDeviceId : ICommandAuthUnsafe
        {
        }

        [CKTypeDefiner]
        public interface ICommandAuthNormal : ICommandAuthUnsafe
        {
        }

        [CKTypeDefiner]
        public interface ICommandAuthCritical : ICommandAuthNormal
        {
        }

        public interface IFinal1 : ICommandAuthCritical
        {
        }
        public interface IFinal2 : ICommandAuthDeviceId
        {
        }

        public interface IIndependent : IPoco
        {
        }

        public static readonly IEnumerable<Type> TheseValidNestedTypes = typeof( PocoInterfacesAndOtherInterfacesTests ).GetNestedTypes();

        [Test]
        public void Poco_OtherInterfaces_contains_the_definers_that_are_used()
        {
            var poco = TestHelper.GetSuccessfulCollectorResult( TheseValidNestedTypes ).PocoTypeSystemBuilder.PocoDirectory;
            Debug.Assert( poco != null );

            poco.AllInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( IFinal1 ), typeof( IFinal2 ), typeof( IIndependent ) } );
            poco.OtherInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( ICommand ),
                                                                       typeof( ICommandPart ),
                                                                       typeof( ICommandAuthUnsafe ),
                                                                       typeof( ICommandAuthNormal ),
                                                                       typeof( ICommandAuthCritical ),
                                                                       typeof( ICommandAuthDeviceId ) } );
        }

        [Test]
        public void Poco_OtherInterfaces_contains_the_definers_that_are_not_used()
        {
            // With IFinal1 only: ICommandAuthDeviceId is not here.
            {
                var types = TheseValidNestedTypes.Where( t => t != typeof( IFinal2 ) );
                var poco = TestHelper.GetSuccessfulCollectorResult( types ).PocoTypeSystemBuilder.PocoDirectory;
                Debug.Assert( poco != null );

                poco.AllInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( IFinal1 ), typeof( IIndependent ) } );
                poco.OtherInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( ICommand ),
                                                                           typeof( ICommandPart ),
                                                                           typeof( ICommandAuthUnsafe ),
                                                                           typeof( ICommandAuthNormal ),
                                                                           typeof( ICommandAuthCritical ),
                                                                           typeof( ICommandAuthDeviceId ) } );
            }
            // Without IPoco at all: no definers are referenced.
            {
                var types = TheseValidNestedTypes.Where( t => !t.Name.StartsWith( "IFinal", StringComparison.Ordinal ) );
                var poco = TestHelper.GetSuccessfulCollectorResult( types ).PocoTypeSystemBuilder.PocoDirectory;
                Debug.Assert( poco != null );

                poco.AllInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( IIndependent ) } );
                poco.OtherInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( ICommand ),
                                                                           typeof( ICommandPart ),
                                                                           typeof( ICommandAuthUnsafe ),
                                                                           typeof( ICommandAuthNormal ),
                                                                           typeof( ICommandAuthCritical ),
                                                                           typeof( ICommandAuthDeviceId ) } );
            }
        }

    }
}
