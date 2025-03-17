using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LibWithSampleModel.Tests;

[TestFixture]
public class SimpleTests
{
    [Test]
    public void Lib_testing_stupid_code_generation()
    {
        var service = SharedEngine.AutomaticServices.GetRequiredService<ServiceWithStupidCodeGeneration>();
        service.GetName().ShouldBe( "Hello from generated code! (touch)" );
    }

    [Test]
    public void Sample_model_has_a_Poco()
    {
        var d = SharedEngine.AutomaticServices.GetRequiredService<PocoDirectory>();
        d.Create<Sample.Model.IRegularPoco>().ShouldNotBeNull();
    }
}
