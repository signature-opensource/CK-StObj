using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjSetupTestHelper;

namespace LibWithSampleModel.Tests
{
    [TestFixture]
    public class SimpleTests
    {
        [Test]
        public void Lib_testing()
        {
            var service = TestHelper.AutomaticServices.GetRequiredService<ServiceWithStupidCodeGeneration>();
            service.GetName().Should().Be( "Hello from generated code!" );
        }
    }
}
