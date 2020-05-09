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
            collector.SetAutoServiceKind( "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+IService, CK.StObj.Engine.Tests", AutoServiceKind.IsScoped|AutoServiceKind.IsMultipleService, isOptional );
            collector.RegisterType( typeof( Service ) );

            var result = TestHelper.GetSuccessfulResult( collector );
            var d = result.Services.SimpleMappings[typeof( Service )];
            d.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
            d.MultipleMappings.Should().Contain( typeof(IService) );
        }

    }
}
