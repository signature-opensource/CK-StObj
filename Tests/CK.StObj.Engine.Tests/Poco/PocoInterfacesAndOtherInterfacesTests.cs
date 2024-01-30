using System;
using System.Collections.Generic;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;
using FluentAssertions;
using NUnit.Framework;
using CK.Core;
using System.Linq;
using System.Diagnostics;

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

        static readonly IEnumerable<Type> TheseValidNestedTypes = typeof( PocoInterfacesAndOtherInterfacesTests ).GetNestedTypes();

        [Test]
        public void Poco_OtherInterfaces_contains_the_definers_that_are_used()
        {
            var c = TestHelper.CreateStObjCollector( TheseValidNestedTypes.ToArray() );
            var poco = TestHelper.GetSuccessfulResult( c ).PocoTypeSystemBuilder.PocoDirectory;
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
        public void Poco_OtherInterfaces_does_NOT_contain_the_definers_that_are_not_used()
        {
            // With IFinal1 only: ICommandAuthDeviceId is not here.
            {
                var c = TestHelper.CreateStObjCollector( TheseValidNestedTypes.Where( t => t != typeof( IFinal2 ) ).ToArray() );
                var poco = TestHelper.GetSuccessfulResult( c ).PocoTypeSystemBuilder.PocoDirectory;
                Debug.Assert( poco != null );

                poco.AllInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( IFinal1 ), typeof( IIndependent ) } );
                poco.OtherInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( ICommand ),
                                                                           typeof( ICommandPart ),
                                                                           typeof( ICommandAuthUnsafe ),
                                                                           typeof( ICommandAuthNormal ),
                                                                           typeof( ICommandAuthCritical ) } );
            }
            // Without IPoco at all: no definers are referenced.
            {
                var c = TestHelper.CreateStObjCollector( TheseValidNestedTypes.Where( t => !t.Name.StartsWith( "IFinal", StringComparison.Ordinal ) ).ToArray() );
                var poco = TestHelper.GetSuccessfulResult( c ).PocoTypeSystemBuilder.PocoDirectory;
                Debug.Assert( poco != null );

                poco.AllInterfaces.Keys.Should().BeEquivalentTo( new[] { typeof( IIndependent ) } );
                poco.OtherInterfaces.Keys.Should().BeEmpty();
            }
        }

    }
}
