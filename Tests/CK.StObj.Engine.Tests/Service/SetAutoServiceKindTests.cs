using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            var d = map.Services.SimpleMappings[typeof( Service )];
            d.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
            d.MultipleMappings.Should().Contain( typeof( IService ) );
        }

        // This is defined as a Singleton:
        // SetAutoServiceKind( "...+OpenGeneric`1, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, true );
        public class OpenGeneric<T> where T : struct { public T MagicValue; }

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
