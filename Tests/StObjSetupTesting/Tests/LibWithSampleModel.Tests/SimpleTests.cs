using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using static CK.Testing.StObjSetupTestHelper;

namespace LibWithSampleModel.Tests
{
    [TestFixture]
    public class SimpleTests
    {
        [Test]
        public void Lib_testing_stupid_code_generation()
        {
            var service = TestHelper.AutomaticServices.GetRequiredService<ServiceWithStupidCodeGeneration>();
            service.GetName().Should().Be( "Hello from generated code! (touch)" );
        }

        [Test]
        public void Lib_internal_duck_typing()
        {
            var d = TestHelper.AutomaticServices.GetRequiredService<PocoDirectory>();
            d.Create<Sample.Model.IRegularPoco>().Should().NotBeNull();
            FluentActions.Invoking( () => d.Create<Sample.Model.IHiddenPoco>() )
                .Should().Throw<Exception>().WithMessage( "Unable to resolve IPoco interface 'Sample.Model.IHiddenPoco' from PocoDirectory." );
        }
    }
}
