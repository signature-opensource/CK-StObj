using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests
{
    public class SampleAspectConfiguration : EngineAspectConfiguration
    {
        public override string AspectType => "Sample.AspectSample, In.An.Assembly.That.Depends.On.CK.StObj.Runtime";

        public SampleAspectConfiguration()
        {
            Version = 2;
            Data = string.Empty;
        }

        public SampleAspectConfiguration( XElement e )
        {
            e.Attribute( EngineConfiguration.xType ).Should().NotBeNull( "The Type attribute has been used to locate this type!" );

            // This is how Aspect version should be managed.
            int version = (int)e.AttributeRequired( EngineConfiguration.xVersion );
            if( version <= 0 || version > Version ) Throw.ArgumentOutOfRangeException( nameof( Version ) );

            if( Version == 1 ) Data = "This was not available in version 1.";
            else Data = (string?)e.Element( "Data" ) ?? "<No Data>";
        }

        public override XElement SerializeXml( XElement e )
        {
            e.Add( new XAttribute( EngineConfiguration.xVersion, Version ),
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

    public class AnotherAspectConfiguration : EngineAspectConfiguration
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

    public class TypeScriptAspectConfiguration : EngineAspectConfiguration
    {
        public TypeScriptAspectConfiguration()
        {
            GenerateDocumentation = true;
            LibraryVersions = new Dictionary<string, string>();
        }

        public TypeScriptAspectConfiguration( XElement e )
        {
            GenerateDocumentation = (bool?)e.Attribute( "GenerateDocumentation" ) ?? true;
            LibraryVersions = e.Element( "LibraryVersions" )?
                    .Elements( "Library" )
                    .Select( e => (e.Attribute( EngineConfiguration.xName )?.Value, e.Attribute( EngineConfiguration.xVersion )?.Value) )
                    .Where( e => !string.IsNullOrWhiteSpace( e.Item1 ) && !string.IsNullOrWhiteSpace( e.Item2 ) )
                    .GroupBy( e => e.Item1 )
                    .ToDictionary( g => g.Key!, g => g.Last().Item2! )
                  ?? new Dictionary<string, string>();

        }

        public bool GenerateDocumentation { get; set; }

        public Dictionary<string, string> LibraryVersions { get; }


        public override string AspectType => "The.Actual.Aspect, In.Engine.Package";

        public override XElement SerializeXml( XElement e )
        {
            e.Add( new XAttribute( EngineConfiguration.xVersion, "1" ),
                        GenerateDocumentation == false
                            ? new XAttribute( "GenerateDocumentation", false )
                            : null,
                        LibraryVersions.Count > 0
                            ? new XElement( "LibraryVersions",
                                            LibraryVersions.Select( kv => new XElement( "Library",
                                                new XAttribute( EngineConfiguration.xName, kv.Key ),
                                                new XAttribute( EngineConfiguration.xVersion, kv.Value ) ) ) )
                            : null
                 );
            return e;
        }

        public override BinPathAspectConfiguration CreateBinPathConfiguration() => new TypeScriptBinPathAspectConfiguration();
    }

    public class TypeScriptBinPathAspectConfiguration : MultipleBinPathAspectConfiguration<TypeScriptBinPathAspectConfiguration>
    {
        public TypeScriptBinPathAspectConfiguration()
        {
            Barrels = new HashSet<NormalizedPath>();
            TypeFilterName = "TypeScript";
        }

        public NormalizedPath TargetProjectPath { get; set; }
        public HashSet<NormalizedPath> Barrels { get; }
        public string TypeFilterName { get; set; }

        protected override void InitializeOneFrom( XElement e )
        {
            TargetProjectPath = e.Attribute( "TargetProjectPath" )?.Value;
            Barrels.Clear();
            Barrels.AddRange( e.Elements( "Barrels" )
                                  .Elements( "Barrel" )
                                     .Select( c => new NormalizedPath( (string?)c.Attribute( "Path" ) ?? c.Value ) ) );

            TypeFilterName = (string?)e.Attribute( "TypeFilterName" ) ?? "TypeScript";
        }

        protected override void WriteOneXml( XElement e )
        {
            e.Add( new XAttribute( "TargetProjectPath", TargetProjectPath ),
                   new XElement( "Barrels",
                                    Barrels.Select( p => new XElement( "Barrel", new XAttribute( EngineConfiguration.xPath, p ) ) ) ),
                   new XAttribute( "TypeFilterName", TypeFilterName )
                );
        }
    }


    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void EngineConfiguration_and_BinPath_tests()
        {
            var c1 = new EngineConfiguration();
            c1.BinPaths.Should().HaveCount( 1 );
            c1.FirstBinPath.Owner.Should().BeSameAs( c1 );
            c1.RemoveBinPath( c1.BinPaths[0] );
            c1.BinPaths.Should().HaveCount( 1, "Cannot remove the last BinPath." );

            var binPath1 = new BinPathConfiguration();
            binPath1.Owner.Should().BeNull();
            c1.RemoveBinPath( binPath1 );
            c1.BinPaths.Should().HaveCount( 1, "No error when removing a non existing BinPath." );

            c1.AddBinPath( binPath1 );
            binPath1.Owner.Should().BeSameAs( c1 );
            c1.BinPaths.Should().HaveCount( 2 );

            var wasC1Default = c1.FirstBinPath;
            c1.RemoveBinPath( wasC1Default );
            wasC1Default.Owner.Should().BeNull();
            c1.BinPaths.Should().HaveCount( 1 );

            c1.AddBinPath( binPath1 );
            binPath1.Owner.Should().BeSameAs( c1 );
            c1.BinPaths.Should().HaveCount( 1, "No change (the BinPath was already here)." );
            c1.FirstBinPath.Should().BeSameAs( binPath1 );

            var c2 = new EngineConfiguration();
            FluentActions.Invoking( () => c2.AddBinPath( binPath1 ) )
                .Should().Throw<ArgumentException>()
                         .WithMessage( "Invalid argument: 'binPath.Owner == null' should be true." );
        }

        [Test]
        public void EngineConfiguration_and_Aspect_tests()
        {
            var c1 = new EngineConfiguration();

            var aspect = new SampleAspectConfiguration();
            aspect.Owner.Should().BeNull();
            c1.AddAspect( aspect );
            aspect.Owner.Should().BeSameAs( c1 );

            // No 2 aspect of the same type (the same AspectName).
            FluentActions.Invoking( () => c1.AddAspect( new SampleAspectConfiguration() ) )
                .Should().Throw<ArgumentException>()
                         .WithMessage( "An aspect of the same type already exists. (Parameter '!_namedAspects.ContainsKey( aspect.AspectName )')" );


            c1.RemoveAspect( aspect );
            aspect.Owner.Should().BeNull();

            c1.FirstBinPath.Aspects.Should().BeEmpty();
            var bAspect = new SampleBinPathAspectConfiguration();
            bAspect.Owner.Should().BeNull();
            bAspect.AspectConfiguration.Should().BeNull();

            FluentActions.Invoking( () => c1.BinPaths[0].AddAspect( bAspect ) )
                .Should().Throw<ArgumentException>()
                         .WithMessage( "Unable to add the BinPath aspect configuration. The aspect 'Sample' must exist in the EngineConfiguration.Aspects. (Parameter 'aspect')" );

            c1.AddAspect( aspect );
            c1.FirstBinPath.AddAspect( bAspect );
            bAspect.Owner.Should().BeSameAs( c1.FirstBinPath );
            bAspect.AspectConfiguration.Should().BeSameAs( aspect );

            c1.FirstBinPath.RemoveAspect( bAspect );
            bAspect.Owner.Should().BeNull();
            bAspect.AspectConfiguration.Should().BeNull();

            // Add it again.
            c1.FirstBinPath.AddAspect( bAspect );
            bAspect.Owner.Should().BeSameAs( c1.FirstBinPath );
            bAspect.AspectConfiguration.Should().BeSameAs( aspect );
            c1.FirstBinPath.Aspects.Should().HaveCount( 1 );

            // Now remove the aspect: the bAspect is detached.
            c1.RemoveAspect( aspect );
            c1.FirstBinPath.Aspects.Should().BeEmpty();
            bAspect.Owner.Should().BeNull();
            bAspect.AspectConfiguration.Should().BeNull();

            // Restores the state with the aspect and the bAspect.
            c1.AddAspect( aspect );
            c1.FirstBinPath.AddAspect( bAspect );
            bAspect.Owner.Should().BeSameAs( c1.FirstBinPath );
            bAspect.AspectConfiguration.Should().BeSameAs( aspect );

            // Adds a new BinPath so we can remove the FirstBinPath.
            c1.AddBinPath( new BinPathConfiguration() );
            var first = c1.FirstBinPath;
            c1.RemoveBinPath( first );
            first.Owner.Should().BeNull();

            bAspect.Owner.Should().BeSameAs( first, "The aspect configuration still belong to the first BinPath." );
            bAspect.AspectConfiguration.Should().BeNull( "But is it no more bound to any Aspect configuration." );

            var c2 = new EngineConfiguration();
            // We cannot add the aspect that is currently bound to c1.
            FluentActions.Invoking( () => c2.AddAspect( aspect ) )
                .Should().Throw<ArgumentException>()
                         .WithMessage( "Invalid argument: 'aspect.Owner == null' should be true." );
            var aspect2 = new SampleAspectConfiguration();
            c2.AddAspect( aspect2 );

            // If we add this BinPath to another Engine configuration that has the Aspect,
            // it binds to the configuration aspect instance.
            c2.AddBinPath( first );
            bAspect.Owner.Should().BeSameAs( first, "No change." );
            bAspect.AspectConfiguration.Should().BeSameAs( aspect2, "The Aspect configuration has been bound." );

            c2.RemoveBinPath( first );

            // Adding a BinPath with BinPath aspects to a configuration without the aspect
            // removes the BinPath aspects that cannot be rebind.
            var c3 = new EngineConfiguration();
            c3.AddBinPath( first );
            first.Owner.Should().BeSameAs( c3 );
            first.Aspects.Should().BeEmpty();
            bAspect.Owner.Should().BeNull();
            bAspect.AspectConfiguration.Should().BeNull();
        }

        [Test]
        public void MultipleBinPathAspectConfiguration_add_and_remove_manage_the_composite1()
        {
            var c = new EngineConfiguration();
            c.BinPaths.Should().HaveCount( 1 );

            var aspect = new TypeScriptAspectConfiguration();
            var bAspect1 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P1" };
            var bAspect2 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P2" };
            var bAspect3 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P3" };
            c.AddAspect( aspect );

            c.FirstBinPath.AddAspect( bAspect1 );
            c.FirstBinPath.AddAspect( bAspect2 );
            c.FirstBinPath.AddAspect( bAspect3 );
            c.FirstBinPath.Aspects.Single().Should().BeSameAs( bAspect1 );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );

            bAspect1.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect2.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect2.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect3.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );

            c.FirstBinPath.RemoveAspect( bAspect2 );
            bAspect1.OtherConfigurations.Should().HaveCount( 1 ).And.Contain( bAspect3 );

            bAspect1.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect2.Owner.Should().BeNull();
            bAspect2.AspectConfiguration.Should().BeNull();
            bAspect3.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );

            c.FirstBinPath.RemoveAspect( bAspect1 );
            c.FirstBinPath.Aspects.Single().Should().BeSameAs( bAspect3 );
            bAspect3.OtherConfigurations.Should().BeEmpty();

            bAspect1.Owner.Should().BeNull();
            bAspect1.AspectConfiguration.Should().BeNull();
            bAspect2.Owner.Should().BeNull();
            bAspect2.AspectConfiguration.Should().BeNull();
            bAspect3.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );

            c.FirstBinPath.RemoveAspect( bAspect3 );
            c.FirstBinPath.Aspects.Should().BeEmpty();

            bAspect1.Owner.Should().BeNull();
            bAspect1.AspectConfiguration.Should().BeNull();
            bAspect2.Owner.Should().BeNull();
            bAspect2.AspectConfiguration.Should().BeNull();
            bAspect3.Owner.Should().BeNull();
            bAspect3.AspectConfiguration.Should().BeNull();

        }

        [Test]
        public void MultipleBinPathAspectConfiguration_add_and_remove_manage_the_composite2()
        {
            var c = new EngineConfiguration();
            var aspect = new TypeScriptAspectConfiguration();
            var bAspect1 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P1" };
            var bAspect2 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P2" };
            var bAspect3 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P3" };
            c.AddAspect( aspect );
            c.FirstBinPath.AddAspect( bAspect1 );
            c.FirstBinPath.AddAspect( bAspect2 );
            c.FirstBinPath.AddAspect( bAspect3 );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );

            c.FirstBinPath.RemoveAspect( bAspect1 );
            bAspect1.Owner.Should().BeNull();
            bAspect1.AspectConfiguration.Should().BeNull();
            bAspect1.OtherConfigurations.Should().BeEmpty();

            var newHead = c.FirstBinPath.FindAspect<TypeScriptBinPathAspectConfiguration>();
            newHead.Should().BeSameAs( bAspect2 );
            bAspect2.OtherConfigurations.Should().HaveCount( 1 ).And.Contain( bAspect3 ).And.NotContain( bAspect1 );
            bAspect3.OtherConfigurations.Should().HaveCount( 1 ).And.Contain( bAspect2 ).And.NotContain( bAspect1 );

            c.FirstBinPath.RemoveAspect( bAspect2 );
            bAspect2.Owner.Should().BeNull();
            bAspect2.AspectConfiguration.Should().BeNull();
            bAspect2.OtherConfigurations.Should().BeEmpty();

            newHead = c.FirstBinPath.FindAspect<TypeScriptBinPathAspectConfiguration>();
            newHead.Should().BeSameAs( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 0 ).And.NotContain( bAspect1 ).And.NotContain( bAspect2 );

            c.FirstBinPath.RemoveAspect( bAspect3 );
            bAspect3.Owner.Should().BeNull();
            bAspect3.AspectConfiguration.Should().BeNull();
            bAspect3.OtherConfigurations.Should().BeEmpty();
            c.FirstBinPath.Aspects.Should().BeEmpty();
        }

        [Test]
        public void MultipleBinPathAspectConfiguration_and_Engine_Aspect()
        {
            var c = new EngineConfiguration();
            var aspect = new TypeScriptAspectConfiguration();
            var bAspect1 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P1" };
            var bAspect2 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P2" };
            var bAspect3 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P3" };
            c.AddAspect( aspect );
            c.FirstBinPath.AddAspect( bAspect1 );
            c.FirstBinPath.AddAspect( bAspect2 );
            c.FirstBinPath.AddAspect( bAspect3 );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );
            bAspect1.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect2.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect2.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect3.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );
            c.FirstBinPath.Aspects.Should().HaveCount( 1 );

            c.RemoveAspect( aspect );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );
            bAspect1.Owner.Should().BeNull();
            bAspect1.AspectConfiguration.Should().BeNull();
            bAspect2.Owner.Should().BeNull();
            bAspect2.AspectConfiguration.Should().BeNull();
            bAspect3.Owner.Should().BeNull();
            bAspect3.AspectConfiguration.Should().BeNull();
            c.FirstBinPath.Aspects.Should().BeEmpty();

            c.AddAspect( aspect );
            c.FirstBinPath.AddAspect( bAspect2 );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );
            bAspect1.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect2.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect2.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect3.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );
            c.FirstBinPath.Aspects.Should().HaveCount( 1 );

            var bAspect4 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P4" };
            c.FirstBinPath.AddAspect( bAspect4 );
            bAspect1.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect2 ).And.Contain( bAspect3 ).And.Contain( bAspect4 );
            bAspect2.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect1 ).And.Contain( bAspect3 ).And.Contain( bAspect4 );
            bAspect3.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect1 ).And.Contain( bAspect2 ).And.Contain( bAspect4 );
            bAspect4.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect1 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect1.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect2.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect2.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect3.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect4.Owner.Should().BeSameAs( c.FirstBinPath );
            bAspect4.AspectConfiguration.Should().BeSameAs( aspect );
            c.FirstBinPath.Aspects.Should().HaveCount( 1 );

        }

        [Test]
        public void MultipleBinPathAspectConfiguration_and_BinPath()
        {
            var c = new EngineConfiguration();
            var bOther = new BinPathConfiguration();
            c.AddBinPath( bOther );

            var aspect = new TypeScriptAspectConfiguration();
            var bAspect1 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P1" };
            var bAspect2 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P2" };
            var bAspect3 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P3" };
            c.AddAspect( aspect );
            bOther.AddAspect( bAspect1 );
            bOther.AddAspect( bAspect2 );
            bOther.AddAspect( bAspect3 );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );
            bAspect1.Owner.Should().BeSameAs( bOther );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect2.Owner.Should().BeSameAs( bOther );
            bAspect2.AspectConfiguration.Should().BeSameAs( aspect );
            bAspect3.Owner.Should().BeSameAs( bOther );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect );
            bOther.Aspects.Should().HaveCount( 1 );

            c.RemoveBinPath( bOther );
            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );
            bAspect1.Owner.Should().BeSameAs( bOther );
            bAspect1.AspectConfiguration.Should().BeNull();
            bAspect2.Owner.Should().BeSameAs( bOther );
            bAspect2.AspectConfiguration.Should().BeNull();
            bAspect3.Owner.Should().BeSameAs( bOther );
            bAspect3.AspectConfiguration.Should().BeNull();
            bOther.Aspects.Should().HaveCount( 1 );

            var c2 = new EngineConfiguration();
            var aspect2 = new TypeScriptAspectConfiguration();
            c2.AddAspect( aspect2 );
            c2.AddBinPath( bOther );

            bAspect1.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect2.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect3 );
            bAspect3.OtherConfigurations.Should().HaveCount( 2 ).And.Contain( bAspect1 ).And.Contain( bAspect2 );
            bAspect1.Owner.Should().BeSameAs( bOther );
            bAspect1.AspectConfiguration.Should().Be( aspect2 );
            bAspect2.Owner.Should().BeSameAs( bOther );
            bAspect2.AspectConfiguration.Should().Be( aspect2 );
            bAspect3.Owner.Should().BeSameAs( bOther );
            bAspect3.AspectConfiguration.Should().Be( aspect2 );
            bOther.Aspects.Should().HaveCount( 1 );

            var bAspect4 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P4" };
            bOther.AddAspect( bAspect4 );
            bAspect1.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect2 ).And.Contain( bAspect3 ).And.Contain( bAspect4 );
            bAspect2.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect1 ).And.Contain( bAspect3 ).And.Contain( bAspect4 );
            bAspect3.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect1 ).And.Contain( bAspect2 ).And.Contain( bAspect4 );
            bAspect4.OtherConfigurations.Should().HaveCount( 3 ).And.Contain( bAspect1 ).And.Contain( bAspect2 ).And.Contain( bAspect3 );
            bAspect1.Owner.Should().Be( bOther );
            bAspect1.AspectConfiguration.Should().BeSameAs( aspect2 );
            bAspect2.Owner.Should().Be( bOther );
            bAspect2.AspectConfiguration.Should().BeSameAs( aspect2 );
            bAspect3.Owner.Should().Be( bOther );
            bAspect3.AspectConfiguration.Should().BeSameAs( aspect2 );
            bAspect4.Owner.Should().Be( bOther );
            bAspect4.AspectConfiguration.Should().BeSameAs( aspect2 );
            bOther.Aspects.Should().HaveCount( 1 );

        }

        [Test]
        public void MultipleBinPathAspectConfiguration_Xml()
        {
            var c1 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P1", TypeFilterName = "TypeScriptNumber1" };
            c1.Barrels.Add( "B1" );
            string x1 = c1.ToXml().ToString().ReplaceLineEndings();
            x1.Should().Be( """
                <TypeScript TargetProjectPath="P1" TypeFilterName="TypeScriptNumber1">
                  <Barrels>
                    <Barrel Path="B1" />
                  </Barrels>
                </TypeScript>
                """.ReplaceLineEndings() );
            var c1Back = new TypeScriptBinPathAspectConfiguration();
            c1Back.InitializeFrom( XElement.Parse( x1 ) );
            c1Back.ToXml().ToString().ReplaceLineEndings().Should().Be( x1 );

            var c2 = new TypeScriptBinPathAspectConfiguration() { TargetProjectPath = "P2", TypeFilterName = "TypeScript2" };

            c1.AddOtherConfiguration( c2 );
            string x2 = c1.ToXml().ToString().ReplaceLineEndings();
            x2.Should().Be( """
                <TypeScript>
                  <Array>
                    <TypeScript TargetProjectPath="P1" TypeFilterName="TypeScriptNumber1">
                      <Barrels>
                        <Barrel Path="B1" />
                      </Barrels>
                    </TypeScript>
                    <TypeScript TargetProjectPath="P2" TypeFilterName="TypeScript2">
                      <Barrels />
                    </TypeScript>
                  </Array>
                </TypeScript>
                """.ReplaceLineEndings() );
            var c2Back = new TypeScriptBinPathAspectConfiguration();
            c2Back.InitializeFrom( XElement.Parse( x2 ) );
            c2Back.ToXml().ToString().ReplaceLineEndings().Should().Be( x2 );

            c2Back.OtherConfigurations.Should().HaveCount( 1 );
            c2Back.OtherConfigurations.Single().OtherConfigurations.Should().HaveCount( 1 ).And.Contain( c2Back );

            var e = new EngineConfiguration();
            e.EnsureAspect<TypeScriptAspectConfiguration>();
            e.FirstBinPath.AddAspect( c2Back );

            var eX = e.ToXml().ToString().ReplaceLineEndings();
            eX.Should().Be( """
                <Setup>
                  <!--Please see https://github.com/signature-opensource/CK-StObj/blob/master/CK.Engine.Configuration/EngineConfiguration.cs for documentation.-->
                  <GlobalExcludedTypes />
                  <Aspect Type="CK.StObj.Engine.Tests.TypeScriptAspectConfiguration, CK.StObj.Engine.Tests" Version="1" />
                  <!--BinPaths: please see https://github.com/signature-opensource/CK-StObj/blob/master/CK.Engine.Configuration/BinPathConfiguration.cs for documentation.-->
                  <BinPaths>
                    <BinPath Path="" DiscoverAssembliesFromPath="false">
                      <CompileOption>None</CompileOption>
                      <Assemblies />
                      <ExcludedTypes />
                      <Types />
                      <TypeScript>
                        <Array>
                          <TypeScript TargetProjectPath="P1" TypeFilterName="TypeScriptNumber1">
                            <Barrels>
                              <Barrel Path="B1" />
                            </Barrels>
                          </TypeScript>
                          <TypeScript TargetProjectPath="P2" TypeFilterName="TypeScript2">
                            <Barrels />
                          </TypeScript>
                        </Array>
                      </TypeScript>
                    </BinPath>
                  </BinPaths>
                </Setup>
                """.ReplaceLineEndings() );
            var eBack = new EngineConfiguration( XElement.Parse( eX ) );
            eBack.ToXml().ToString().ReplaceLineEndings().Should().Be( eX );

            eBack.FindAspect<TypeScriptAspectConfiguration>().Should().NotBeNull();
            eBack.FirstBinPath.Aspects.Should().HaveCount( 1 );
            var tsConfig = eBack.FirstBinPath.FindAspect<TypeScriptBinPathAspectConfiguration>();
            Throw.DebugAssert( tsConfig != null );
            tsConfig.OtherConfigurations.Should().HaveCount( 1 );
            tsConfig.AllConfigurations.All( c => c.Owner == eBack.FirstBinPath ).Should().BeTrue();
        }

        static readonly XElement _config = XElement.Parse( """

            <Setup>
              <BasePath>/The/Base/Path</BasePath>
              <Aspect Type="CK.StObj.Engine.Tests.SampleAspectConfiguration, CK.StObj.Engine.Tests" Version="1" />
              <Aspect Type="CK.StObj.Engine.Tests.AnotherAspectConfiguration, CK.StObj.Engine.Tests">
              </Aspect>
              <BinPaths>
                <BinPath Path="../../Relative/To/Base/Debug/net8.0">
                    <Assemblies>
                        <Assembly>An.Assembly.Name</Assembly>
                        <Assembly Name="Another.Assembly" />
                    </Assemblies>
                    <Types>
                        <Type Kind="IsScoped">CK.Core.IActivityMonitor, CK.ActivityMonitor</Type>
                        <Type Name="Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions" Kind="IsMultipleService|IsSingleton" />
                        <Type Name="Microsoft.Extensions.Options.IOptions`1, Microsoft.Extensions.Options" Kind="IsSingleton" />
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
                <BinPath Path="/Absolute/Path/Bin">
                  <Assemblies />
                  <Types />
                  <ExcludedTypes />
                  <OutputPath>Another/relative/path</OutputPath>
                  <CompileOption>Compile</CompileOption>
                  <GenerateSourceFiles>True</GenerateSourceFiles>
                </BinPath>
              </BinPaths>
              <GeneratedAssemblyName>CK.GeneratedAssembly.Not the default</GeneratedAssemblyName>
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
            EngineConfiguration c = new EngineConfiguration( _config );
            c.BasePath.Should().Be( new NormalizedPath( "/The/Base/Path" ) );

            c.BinPaths.Should().HaveCount( 2 );
            var b1 = c.BinPaths[0];
            b1.Path.Should().Be( new NormalizedPath( "../../Relative/To/Base/Debug/net8.0" ) );
            b1.Assemblies.Should().BeEquivalentTo( "An.Assembly.Name", "Another.Assembly" );

            b1.Types.Should().HaveCount( 3 );
            var t1 = b1.Types.Single( tc => tc.Type == typeof(IActivityMonitor) );
            t1.Kind.Should().Be( AutoServiceKind.IsScoped );
            var t2 = b1.Types.Single( tc => tc.Type == typeof( Microsoft.Extensions.Hosting.IHostedService ) );
            t2.Kind.Should().Be( AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
            var t3 = b1.Types.Single( tc => tc.Type == typeof( Microsoft.Extensions.Options.IOptions<> ) ); ;
            t3.Kind.Should().Be( AutoServiceKind.IsSingleton );

            var bSample = b1.FindAspect<SampleBinPathAspectConfiguration>();
            Debug.Assert( bSample != null );
            bSample.Param.Should().Be( "{ProjectPath}Test" );
            bSample.A.Should().Be( "{OutputPath}InTheOutputPath" );

            var bAnother = b1.FindAspect<AnotherBinPathAspectConfiguration>();
            Debug.Assert( bAnother != null );
            bAnother.Path.Should().Be( "{BasePath}comm/ands" );

            b1.ExcludedTypes.Should().BeEquivalentTo( new[] { typeof( ActivityMonitor ), typeof( CK.Testing.StObjEngineTestHelper ) } );
            b1.OutputPath.Should().Be( new NormalizedPath( "Another/Relative" ) );
            b1.CompileOption.Should().Be( CompileOption.Parse );
            b1.GenerateSourceFiles.Should().BeTrue();

            c.GeneratedAssemblyName.Should().Be( "CK.GeneratedAssembly.Not the default" );
            c.InformationalVersion.Should().Be( "This will be in the generated Assembly." );
            c.TraceDependencySorterInput.Should().BeTrue();
            c.TraceDependencySorterOutput.Should().BeTrue();
            c.RevertOrderingNames.Should().BeTrue();
            c.GlobalExcludedTypes.Should().BeEquivalentTo( new[] { typeof( ActivityMonitor ), typeof( CK.Testing.StObjEngineTestHelper ) } );
            c.Aspects.Should().HaveCount( 2 );
            c.Aspects[0].Should().BeAssignableTo<SampleAspectConfiguration>();
            c.Aspects[1].Should().BeAssignableTo<AnotherAspectConfiguration>();
        }

        [Test]
        public void BasePath_OutputPath_and_ProjectPath_placeholders_in_BinPath_aspects()
        {
            EngineConfiguration c = new EngineConfiguration( _config );

            var sample = c.BinPaths[0].FindAspect<SampleBinPathAspectConfiguration>();
            Throw.DebugAssert( sample != null );
            sample.Param.Should().Be( "{ProjectPath}Test" );
            sample.A.Should().Be( "{OutputPath}InTheOutputPath" );
            sample.XmlData?.ToString().ReplaceLineEndings().Should().Be( """
                <XmlData>
                  <Some>
                    <Data Touched="{BasePath}InXmlIsland1" />
                    <Data>{ProjectPath}InXmlIsland2</Data>
                    <Data>Not touched {ProjectPath} must start the string.</Data>
                  </Some>
                </XmlData>
                """.ReplaceLineEndings() );

            var another = c.BinPaths[0].FindAspect<AnotherBinPathAspectConfiguration>();
            Throw.DebugAssert( another != null );
            another.Path.Should().Be( "{BasePath}comm/ands" );

            RunningEngineConfiguration.PrepareConfiguration( TestHelper.Monitor, c );

            sample.Param.Should().Be( "/The/Base/Path/Another/Relative/Test" );
            sample.A.Should().Be( "/The/Base/Path/Another/Relative/InTheOutputPath" );
            sample.XmlData?.ToString().ReplaceLineEndings().Should().Be( """
                <XmlData>
                  <Some>
                    <Data Touched="/The/Base/Path/InXmlIsland1" />
                    <Data>/The/Base/Path/Another/Relative/InXmlIsland2</Data>
                    <Data>Not touched {ProjectPath} must start the string.</Data>
                  </Some>
                </XmlData>
                """.ReplaceLineEndings() );

            another.Path.Should().Be( "/The/Base/Path/comm/ands" );
        }

        [Test]
        public void configuration_to_xml()
        {
            EngineConfiguration c1 = new EngineConfiguration( _config );
            var e1 = c1.ToXml();
            e1 = NormalizeWithoutAnyOrder( e1 );

            EngineConfiguration c2 = new EngineConfiguration( e1 );
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
