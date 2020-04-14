using System;
using CK.Core;
using CK.Setup;
using CK.StObj.Engine.Tests.SimpleObjects;
using NUnit.Framework;
using System.Linq;
using FluentAssertions;

using static CK.Testing.StObjEngineTestHelper;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

namespace CK.StObj.Engine.Tests
{
    public class SampleAspectConfiguration : IStObjEngineAspectConfiguration
    {
        public string AspectType => "Sample.AspectSample, In.An.Assembly.That.Depends.On.CK.StObj.Runtime";

        public SampleAspectConfiguration( XElement e )
        {
            e.Attribute( StObjEngineConfiguration.xType ).Should().NotBeNull( "The Type attribute has been used to locate this type!" );

            // This is how Aspect version should be managed.
            int version = (int)e.Attribute( StObjEngineConfiguration.xVersion );
            if( version <= 0 || version > Version ) throw new ArgumentOutOfRangeException( nameof( Version ) );

            if( Version == 1 ) Data = "This was not available in version 1.";
            else Data = (string)e.Element( "Data" ) ?? "<No Data>";
        }

        public XElement SerializeXml( XElement e )
        {
            return new XElement( StObjEngineConfiguration.xAspect,
                        new XAttribute( StObjEngineConfiguration.xVersion, Version ),
                        new XElement( "Data", Data ) );
        }

        public int Version { get; } = 2;


        public string Data { get; set; }
    }


    [TestFixture]
    public class ConfigurationTests
    {
        static readonly XElement _config = XElement.Parse( @"
<Setup>
  <BasePath>/The/Base/Path</BasePath>
  <BinPaths>
    <BinPath Path=""../../Relative/To/Base/[Debug|Release]/netcoreapp3.1"">
        <Assemblies>
            <Assembly>An.Assembly.Name</Assembly>
            <Assembly Name=""Another.Assembly"" />
        </Assemblies>
        <Types>
            <Type Name=""CK.Core.IActivityMonitor, CK.ActivityMonitor"" Kind=""IsScoped"" />
            <Type Name=""Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions"" Kind=""IsMultipleService|IsSingleton"" Optional=""True"" />
            <Type Name=""Microsoft.Extensions.Options.IOptions&lt;&gt;, Microsoft.Extensions.Options"" Kind=""IsSingleton,IsFrontProcessService"" Optional=""True"" />
        </Types>
        <ExcludedTypes>
            <Type>CK.Core.ActivityMonitor, CK.ActivityMonitor</Type>
            <Type Name=""CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine"" />
        </ExcludedTypes>
        <OutputPath>Another/Relative</OutputPath>
        <SkipCompilation>true</SkipCompilation>
        <GenerateSourceFiles>True</GenerateSourceFiles>
    </BinPath>
    <BinPath Path=""/Absolute/[Debug|Release]Path/Bin"">
      <Assemblies />
      <Types />
      <ExcludedTypes />
      <OutputPath>Another/relative/path</OutputPath>
      <SkipCompilation>true</SkipCompilation>
      <GenerateSourceFiles>True</GenerateSourceFiles>
    </BinPath>
  </BinPaths>
  <GeneratedAssemblyName>Not the default CK.StObj.AutoAssembly</GeneratedAssemblyName>
  <InformationalVersion>This will be in the generated Assembly.</InformationalVersion>
  <TraceDependencySorterInput>True</TraceDependencySorterInput>
  <TraceDependencySorterOutput>True</TraceDependencySorterOutput>
  <RevertOrderingNames>True</RevertOrderingNames>
  <GlobalExcludedTypes>
    <Type>CK.Core.ActivityMonitor, CK.ActivityMonitor</Type>
    <Type Name=""CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine"" />
  </GlobalExcludedTypes>
  <Aspect Type=""CK.StObj.Engine.Tests.SampleAspectConfiguration, CK.StObj.Engine.Tests"" Version=""1"">
  </Aspect>

</Setup>
" ); 


        [Test]
        public void parsing_a_configuration()
        {
            StObjEngineConfiguration c = new StObjEngineConfiguration( _config );
            c.BasePath.Should().Be( new Text.NormalizedPath( "/The/Base/Path" ) );

            c.BinPaths.Should().HaveCount( 2 );
            var b1 = c.BinPaths[0];
            b1.Path.Should().Be( new Text.NormalizedPath( "../../Relative/To/Base/[Debug|Release]/netcoreapp3.1" ) );
            b1.Assemblies.Should().BeEquivalentTo( "An.Assembly.Name", "Another.Assembly" );

            b1.Types.Should().HaveCount( 3 );
            var t1 = b1.Types[0];
            t1.Name.Should().Be( "CK.Core.IActivityMonitor, CK.ActivityMonitor" );
            t1.Kind.Should().Be( AutoServiceKind.IsScoped );
            t1.Optional.Should().BeFalse();
            var t2 = b1.Types[1];
            t2.Name.Should().Be( "Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions" );
            t2.Kind.Should().Be( AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
            t2.Optional.Should().BeTrue();
            var t3 = b1.Types[2];
            t3.Name.Should().Be( "Microsoft.Extensions.Options.IOptions<>, Microsoft.Extensions.Options" );
            t3.Kind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsSingleton );
            t3.Optional.Should().BeTrue();

            b1.ExcludedTypes.Should().BeEquivalentTo( "CK.Core.ActivityMonitor, CK.ActivityMonitor", "CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine" );
            b1.OutputPath.Should().Be( new Text.NormalizedPath( "Another/Relative" ) );
            b1.SkipCompilation.Should().BeTrue();
            b1.GenerateSourceFiles.Should().BeTrue();

            c.GeneratedAssemblyName.Should().Be( "Not the default CK.StObj.AutoAssembly" );
            c.InformationalVersion.Should().Be( "This will be in the generated Assembly." );
            c.TraceDependencySorterInput.Should().BeTrue();
            c.TraceDependencySorterOutput.Should().BeTrue();
            c.RevertOrderingNames.Should().BeTrue();
            c.GlobalExcludedTypes.Should().BeEquivalentTo( "CK.Core.ActivityMonitor, CK.ActivityMonitor", "CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine" );
            c.Aspects.Should().HaveCount( 1 );
            c.Aspects[0].Should().BeAssignableTo<SampleAspectConfiguration>();
        }


    }
}
