using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests
{
    public class SampleAspectConfiguration : StObjEngineAspectConfiguration
    {
        public override string AspectType => "Sample.AspectSample, In.An.Assembly.That.Depends.On.CK.StObj.Runtime";

        public SampleAspectConfiguration( XElement e )
        {
            e.Attribute( StObjEngineConfiguration.xType ).Should().NotBeNull( "The Type attribute has been used to locate this type!" );

            // This is how Aspect version should be managed.
            int version = (int)e.AttributeRequired( StObjEngineConfiguration.xVersion );
            if( version <= 0 || version > Version ) Throw.ArgumentOutOfRangeException( nameof( Version ) );

            if( Version == 1 ) Data = "This was not available in version 1.";
            else Data = (string?)e.Element( "Data" ) ?? "<No Data>";
        }

        public override XElement SerializeXml( XElement e )
        {
            e.Add( new XAttribute( StObjEngineConfiguration.xVersion, Version ),
                   new XElement( "Data", Data ) );
            return e;
        }

        public override BinPathAspectConfiguration CreateBinPathConfiguration() => new SampleBinPathAspectConfiguration();

        public int Version { get; } = 2;


        public string Data { get; set; }
    }

    public class SampleBinPathAspectConfiguration : BinPathAspectConfiguration
    {
        public SampleBinPathAspectConfiguration()
        {
            Param = string.Empty;
            A = string.Empty;
        }

        public string Param { get; set; }
        public string A { get; set; }
        public XElement? XmlData { get; set; }

        public override void InitializeFrom( XElement e )
        {
            Param = (string)e.Element( "Param" )!;
            Throw.CheckData( Param != null );
            A = (string)e.Attribute( "A" )!;
            Throw.CheckData( A != null );
            var d = e.Element( "XmlData" );
            if( d != null ) XmlData = new XElement( d );
        }

        protected override void WriteXml( XElement e )
        {
            e.Add( new XAttribute( "A", A ),
                   new XElement( "Param", Param ),
                   XmlData != null ? new XElement( XmlData ) : null );
        }
    }

    public class AnotherAspectConfiguration : StObjEngineAspectConfiguration
    {
        public override string AspectType => "Sample.AnotherAspectSample, In.An.Assembly.That.Depends.On.CK.StObj.Runtime";

        public AnotherAspectConfiguration( XElement e )
        {
        }

        public override XElement SerializeXml( XElement e ) => e;

        public override BinPathAspectConfiguration CreateBinPathConfiguration() => new AnotherBinPathAspectConfiguration();
    }

    public class AnotherBinPathAspectConfiguration : BinPathAspectConfiguration
    {
        public string? Path { get; set; }

        public override void InitializeFrom( XElement e )
        {
            Path = (string?)e.Element( "Path" );
        }

        protected override void WriteXml( XElement e )
        {
            e.Add( new XElement( "Path", Path ) );
        }
    }


    [TestFixture]
    public class ConfigurationTests
    {
        static readonly XElement _config = XElement.Parse( """

            <Setup>
              <BasePath>/The/Base/Path</BasePath>
              <Aspect Type="CK.StObj.Engine.Tests.SampleAspectConfiguration, CK.StObj.Engine.Tests" Version="1" />
              <Aspect Type="CK.StObj.Engine.Tests.AnotherAspectConfiguration, CK.StObj.Engine.Tests">
              </Aspect>
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
                    <Another>
                        <Path>{BasePath}comm/ands</Path>
                    </Another>
                    <Sample A="{OutputPath}InTheOutputPath">
                        <Param>{ProjectPath}Test</Param>
                        <XmlData>
                            <Some>
                                <Data Touched="{BasePath}InXmlIsland1" />
                                <Data>{ProjectPath}InXmlIsland2</Data>
                                <Data>Not touched {ProjectPath} must start the string.</Data>
                        </Some>
                        </XmlData>
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

            var bSample = b1.FindAspect<SampleBinPathAspectConfiguration>();
            Debug.Assert( bSample != null );
            bSample.Param.Should().Be( "{ProjectPath}Test" );
            bSample.A.Should().Be( "{OutputPath}InTheOutputPath" );

            var bAnother = b1.FindAspect<AnotherBinPathAspectConfiguration>();
            Debug.Assert( bAnother != null );
            bAnother.Path.Should().Be( "{BasePath}comm/ands" );

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
        public void BasePath_OutputPath_and_ProjectPath_placeholders_in_BinPath_aspects()
        {
            StObjEngineConfiguration c = new StObjEngineConfiguration( _config );

            var sample = c.BinPaths[0].FindAspect<SampleBinPathAspectConfiguration>();
            Throw.DebugAssert( sample != null );
            sample.Param.Should().Be( "{ProjectPath}Test" );
            sample.A.Should().Be( "{OutputPath}InTheOutputPath" );
            sample.XmlData?.ToString().Should().Be( """
                <XmlData>
                  <Some>
                    <Data Touched="{BasePath}InXmlIsland1" />
                    <Data>{ProjectPath}InXmlIsland2</Data>
                    <Data>Not touched {ProjectPath} must start the string.</Data>
                  </Some>
                </XmlData>
                """ );

            var another = c.BinPaths[0].FindAspect<AnotherBinPathAspectConfiguration>();
            Throw.DebugAssert( another != null );
            another.Path.Should().Be( "{BasePath}comm/ands" );

            RunningStObjEngineConfiguration.CheckAndValidate( TestHelper.Monitor, c );

            sample.Param.Should().Be( "/The/Base/Path/Another/Relative/Test" );
            sample.A.Should().Be( "/The/Base/Path/Another/Relative/InTheOutputPath" );
            sample.XmlData?.ToString().Should().Be( """
                <XmlData>
                  <Some>
                    <Data Touched="/The/Base/Path/InXmlIsland1" />
                    <Data>/The/Base/Path/Another/Relative/InXmlIsland2</Data>
                    <Data>Not touched {ProjectPath} must start the string.</Data>
                  </Some>
                </XmlData>
                """ );

            another.Path.Should().Be( "/The/Base/Path/comm/ands" );
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
