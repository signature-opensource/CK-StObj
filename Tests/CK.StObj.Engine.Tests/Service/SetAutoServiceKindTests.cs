using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    public class SetAutoServiceKindTests
    {
        public interface IService
        {
        }

        public class Service : IService, IAutoService
        {
        }

        [TestCase( true )]
        [TestCase( false )]
        public void simple_front_only_registration( bool isOptional )
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+IService, CK.StObj.Engine.Tests", AutoServiceKind.IsScoped | AutoServiceKind.IsMultipleService, isOptional );
            collector.RegisterType( typeof( Service ) );

            var result = TestHelper.GetSuccessfulResult( collector );
            var d = result.EngineMap.Services.SimpleMappings[typeof( Service )];
            d.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
            d.MultipleMappings.Should().Contain( typeof( IService ) );
        }

        // This is defined as a Singleton:
        // SetAutoServiceKind( "...", AutoServiceKind.IsSingleton, true );
        public class OpenGeneric<T> { public T MagicValue; }

        public class GenService : ISingletonAutoService
        {
            public GenService( OpenGeneric<int> dep )
            {
            }
        }

        [Test]
        public void late_resolving_open_generics()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+OpenGeneric`1, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, true );
            collector.RegisterType( typeof( GenService ) );
            TestHelper.GetSuccessfulResult( collector );
        }


    }
}
