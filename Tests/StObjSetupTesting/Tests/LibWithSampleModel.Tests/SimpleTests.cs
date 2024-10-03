using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;

namespace LibWithSampleModel.Tests;

[TestFixture]
public class SimpleTests
{
    [Test]
    public void Lib_testing_stupid_code_generation()
    {
        var service = SharedEngine.AutomaticServices.GetRequiredService<ServiceWithStupidCodeGeneration>();
        service.GetName().Should().Be( "Hello from generated code! (touch)" );
    }

    [Test]
    public void Sample_model_has_a_Poco()
    {
        var d = SharedEngine.AutomaticServices.GetRequiredService<PocoDirectory>();
        d.Create<Sample.Model.IRegularPoco>().Should().NotBeNull();
    }
}
