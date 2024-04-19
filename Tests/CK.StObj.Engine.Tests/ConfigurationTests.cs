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
using System.Diagnostics;

namespace CK.StObj.Engine.Tests
{
    public class SampleAspectConfiguration : IStObjEngineAspectConfiguration
    {
        public string AspectType => "Sample.AspectSample, In.An.Assembly.That.Depends.On.CK.StObj.Runtime";

        public SampleAspectConfiguration( XElement e )
        {
            e.Attribute( StObjEngineConfiguration.xType ).Should().NotBeNull( "The Type attribute has been used to locate this type!" );

            // This is how Aspect version should be managed.
            int version = (int)e.AttributeRequired( StObjEngineConfiguration.xVersion );
            if( version <= 0 || version > Version ) Throw.ArgumentOutOfRangeException( nameof( Version ) );

            if( Version == 1 ) Data = "This was not available in version 1.";
            else Data = (string?)e.Element( "Data" ) ?? "<No Data>";
        }

        public XElement SerializeXml( XElement e )
        {
            e.Add( new XAttribute( StObjEngineConfiguration.xVersion, Version ),
                   new XElement( "Data", Data ) );
            return e;
        }

        public int Version { get; } = 2;


        public string Data { get; set; }
    }

    public class AnotherAspectConfiguration : IStObjEngineAspectConfiguration
    {
        public string AspectType => "Sample.AnotherAspectSample, In.An.Assembly.That.Depends.On.CK.StObj.Runtime";

        public AnotherAspectConfiguration( XElement e )
        {
        }

        public XElement SerializeXml( XElement e ) => e;
    }


    [TestFixture]
    public class ConfigurationTests
    {
        static readonly XElement _config = XElement.Parse( """

            <Setup>
              <BasePath>/The/Base/Path</BasePath>
              <BinPaths>
                <BinPath Path="../../Relative/To/Base/[Debug|Release]/netcoreapp3.1">
                    <Assemblies>
                        <Assembly>An.Assembly.Name</Assembly>
                        <Assembly Name="Another.Assembly" />
                    </Assemblies>
                    <Types>
                        <Type Name="CK.Core.IActivityMonitor, CK.ActivityMonitor" Kind="IsScoped" />
                        <Type Name="Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions" Kind="IsMultipleService|IsSingleton" Optional="True" />
                        <Type Name="Microsoft.Extensions.Options.IOptions`1, Microsoft.Extensions.Options" Kind="IsSingleton" Optional="True" />
                        <!-- This is invalid but not at the configuration parsing level. -->
                        <Type Name="StrangeService, StrangeAssembly" Kind="IsScoped|IsSingleton|IsMultipleService" Optional="True" />
                    </Types>
                    <ExcludedTypes>
                        <Type>CK.Core.ActivityMonitor, CK.ActivityMonitor</Type>
                        <Type Name="CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine" />
                    </ExcludedTypes>
                    <OutputPath>Another/Relative</OutputPath>
                    <CompileOption>Parse</CompileOption>
                    <GenerateSourceFiles>True</GenerateSourceFiles>
                    <AnotherAspect>
                        <Path>comm/ands</Path>
                    </AnotherAspect>
                    <Sample>
                        <Param>Test</Param>
                    </Sample>
                </BinPath>
                <BinPath Path="/Absolute/[Debug|Release]Path/Bin">
                  <Assemblies />
                  <Types />
                  <ExcludedTypes />
                  <OutputPath>Another/relative/path</OutputPath>
                  <CompileOption>Compile</CompileOption>
                  <GenerateSourceFiles>True</GenerateSourceFiles>
                </BinPath>
              </BinPaths>
              <GeneratedAssemblyName>CK.StObj.AutoAssembly.Not the default</GeneratedAssemblyName>
              <InformationalVersion>This will be in the generated Assembly.</InformationalVersion>
              <TraceDependencySorterInput>True</TraceDependencySorterInput>
              <TraceDependencySorterOutput>True</TraceDependencySorterOutput>
              <RevertOrderingNames>True</RevertOrderingNames>
              <GlobalExcludedTypes>
                <Type>CK.Core.ActivityMonitor, CK.ActivityMonitor</Type>
                <Type Name="CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine" />
              </GlobalExcludedTypes>
              <Aspect Type="CK.StObj.Engine.Tests.SampleAspectConfiguration, CK.StObj.Engine.Tests" Version="1">
              </Aspect>
              <Aspect Type="CK.StObj.Engine.Tests.AnotherAspectConfiguration, CK.StObj.Engine.Tests">
              </Aspect>

            </Setup>

            """ );

        [Test]
        public void parsing_a_configuration()
        {
            StObjEngineConfiguration c = new StObjEngineConfiguration( _config );
            c.BasePath.Should().Be( new NormalizedPath( "/The/Base/Path" ) );

            c.BinPaths.Should().HaveCount( 2 );
            var b1 = c.BinPaths[0];
            b1.Path.Should().Be( new NormalizedPath( "../../Relative/To/Base/[Debug|Release]/netcoreapp3.1" ) );
            b1.Assemblies.Should().BeEquivalentTo( "An.Assembly.Name", "Another.Assembly" );

            b1.Types.Should().HaveCount( 4 );
            var t1 = b1.Types[0];
            t1.Name.Should().Be( "CK.Core.IActivityMonitor, CK.ActivityMonitor" );
            t1.Kind.Should().Be( AutoServiceKind.IsScoped );
            t1.Optional.Should().BeFalse();
            var t2 = b1.Types[1];
            t2.Name.Should().Be( "Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions" );
            t2.Kind.Should().Be( AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
            t2.Optional.Should().BeTrue();
            var t3 = b1.Types[2];
            t3.Name.Should().Be( "Microsoft.Extensions.Options.IOptions`1, Microsoft.Extensions.Options" );
            t3.Kind.Should().Be( AutoServiceKind.IsSingleton );
            t3.Optional.Should().BeTrue();
            var t4 = b1.Types[3];
            t4.Name.Should().Be( "StrangeService, StrangeAssembly" );
            t4.Kind.Should().Be( AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton | AutoServiceKind.IsMultipleService );
            t4.Optional.Should().BeTrue();

            var bSample = b1.GetAspectConfiguration<SampleAspectConfiguration>();
            Debug.Assert( bSample != null );
            bSample.Element( "Param" )?.Value.Should().Be( "Test" );
            b1.GetAspectConfiguration( "SampleAspectConfiguration" ).Should().BeSameAs( bSample );
            b1.GetAspectConfiguration( "SampleConfiguration" ).Should().BeSameAs( bSample );
            b1.GetAspectConfiguration( "Sample" ).Should().BeSameAs( bSample );
            b1.GetAspectConfiguration( "SampleAspect" ).Should().BeSameAs( bSample );

            var bAnother = b1.GetAspectConfiguration<AnotherAspectConfiguration>();
            Debug.Assert( bAnother != null );
            bSample.Element( "Path" )?.Value.Should().Be( "comm/ands" );

            b1.ExcludedTypes.Should().BeEquivalentTo( "CK.Core.ActivityMonitor, CK.ActivityMonitor", "CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine" );
            b1.OutputPath.Should().Be( new NormalizedPath( "Another/Relative" ) );
            b1.CompileOption.Should().Be( CompileOption.Parse );
            b1.GenerateSourceFiles.Should().BeTrue();

            c.GeneratedAssemblyName.Should().Be( "CK.StObj.AutoAssembly.Not the default" );
            c.InformationalVersion.Should().Be( "This will be in the generated Assembly." );
            c.TraceDependencySorterInput.Should().BeTrue();
            c.TraceDependencySorterOutput.Should().BeTrue();
            c.RevertOrderingNames.Should().BeTrue();
            c.GlobalExcludedTypes.Should().BeEquivalentTo( "CK.Core.ActivityMonitor, CK.ActivityMonitor", "CK.Testing.StObjEngineTestHelper, CK.Testing.StObjEngine" );
            c.Aspects.Should().HaveCount( 2 );
            c.Aspects[0].Should().BeAssignableTo<SampleAspectConfiguration>();
            c.Aspects[1].Should().BeAssignableTo<AnotherAspectConfiguration>();
        }


        [Test]
        public void configuration_to_xml()
        {
            StObjEngineConfiguration c1 = new StObjEngineConfiguration( _config );
            var e1 = c1.ToXml();
            e1 = NormalizeWithoutAnyOrder( e1 );

            StObjEngineConfiguration c2 = new StObjEngineConfiguration( e1 );
            var e2 = c2.ToXml();
            e2 = NormalizeWithoutAnyOrder( e2 );

            e1.Should().BeEquivalentTo( e2 );
        }

        static XElement NormalizeWithoutAnyOrder( XElement element )
        {
            if( element.HasElements )
            {
                return new XElement(
                    element.Name,
                    element.Attributes().OrderBy( a => a.Name.ToString() ),
                    element.Elements()
                        .OrderBy( a => a.Name.ToString() )
                        .Select( e => NormalizeWithoutAnyOrder( e ) )
                        .OrderBy( e => e.Attributes().Count() )
                        .OrderBy( e => e.Attributes()
                                        .Select( a => a.Value )
                                        .Concatenate( "\u0001" ) )
                        .ThenBy( e => e.Value ) );
            }
            if( element.IsEmpty || string.IsNullOrEmpty( element.Value ) )
            {
                return new XElement( element.Name,
                                     element.Attributes()
                                            .OrderBy( a => a.Name.ToString() ) );
            }
            return new XElement( element.Name,
                                 element.Attributes()
                                        .OrderBy( a => a.Name.ToString() ),
                                 element.Value );
        }

    }
}
