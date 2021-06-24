using CK.CodeGen;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public partial class JsonTypeHierarchyTests
    {
        [Test]
        public void extending_json_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( JsonStringParseSupport ), typeof( IPocoNoIntern ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var root = s.GetService<IPocoFactory<IPocoNoIntern>>().Create();
            root.Person = new Person( "Jean" );
            root.Teacher = new Teacher( "Paul", "Aggreg" );
            root.Student = new Student( "Sartre", 3712 );

            var root2 = JsonTestHelper.Roundtrip( directory, root, text: t => TestHelper.Monitor.Info( $"ITest serialization: " + t ) );
            root2.Should().BeEquivalentTo( root );
        }

        public interface ITestBaseClassOnly : IPoco
        {
            Person? Person { get; set; }
        }


        [Test]
        public void when_a_specialization_is_not_registered_the_known_static_Type_drives_and_this_may_not_be_good()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( JsonStringParseSupport ), typeof( ITestBaseClassOnly ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var root = s.GetService<IPocoFactory<ITestBaseClassOnly>>().Create();
            root.Person =  new Student( "Sartre", 3712 );

            // Here, the serialization relies on ToString() that is virtual: the Student is serialized (it shouldn't!).
            root.ToString().Should().Be( "{\"Person\":\"Sartre|3712\"}" );

            // But the deserialization calls Person.Parse.
            FluentActions.Invoking( () => JsonTestHelper.Roundtrip( directory, root ) ).Should().Throw<ArgumentException>().Where( ex => ex.Message.StartsWith( "Invalid | in name.", StringComparison.OrdinalIgnoreCase ) );
        }

        [Test]
        public void registered_specialization_triggers_overridable_behavior()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( JsonStringParseSupport ), typeof( ITestBaseClassOnly ), typeof( IPocoAllOfThem ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var root = s.GetService<IPocoFactory<ITestBaseClassOnly>>().Create();
            root.Person =  new Student( "Sartre", 3712 );

            // The Person is not IsFinal: the actual Student is serialized.
            // Here, the serialization relies on ToString() that is virtual (so everything works fine). 
            root.ToString().Should().Be( "{\"Person\":[\"CS:CP\",\"Sartre|3712\"]}" );

            // And the deserialization, based on the type name, calls Student.Parse.
            var root2 = JsonTestHelper.Roundtrip( directory, root );

            // Testing Intern.
            root2.Person =  new Intern( "Spi", "Newbie", null );
            var root3 = JsonTestHelper.Roundtrip( directory, root2 );
            root3.Should().BeEquivalentTo( root2 );
            root3.Should().NotBeEquivalentTo( root );
        }


        public interface ITestWithCollections : IPoco
        {
            List<Person> Persons { get; }
        }

        public interface ITestWithCollectionsOfFinal : IPoco
        {
            List<Student> Students { get; }
            List<Intern?> Interns { get; }
        }

        [Test]
        public void collections_use_types_when_needed()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ),
                                                     typeof( JsonStringParseSupport ),
                                                     typeof( ITestWithCollections ),
                                                     typeof( ITestWithCollectionsOfFinal ),
                                                     typeof( IPocoNoIntern ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            {
                var root = s.GetService<IPocoFactory<ITestWithCollections>>().Create();
                root.Persons.Add( new Student( "Sartre", 3712 ) );
                root.Persons.Add( new Teacher( "Camus", "Sisyphe" ) );
                root.Persons.Add( new Person( "Albert" ) );
                root.Persons.Add( new Intern( "Spi", "Newbie", 3712 ) );

                root.ToString().Should().Be( "{\"Persons\":[[\"CS:CP\",\"Sartre|3712\"],[\"CT:CP\",\"Camus|Sisyphe\"],[\"CP\",\"Albert\"],[\"CI:CT\",\"Spi|Newbie|3712\"]]}", "Items MUST HAVE a type." );
                JsonTestHelper.Roundtrip( directory, root );
            }
            {
                var root = s.GetService<IPocoFactory<ITestWithCollectionsOfFinal>>().Create();
                root.Students.Add( new Student( "Sartre", 3712 ) );
                root.Interns.Add( new Intern( "Spi", "Newbie", 3712 ) );
                root.Interns.Add( null );
                root.Interns.Add( new Intern( "Houphouët", "Boigny", null ) );
                root.Interns.Add( null );

                root.ToString().Should().Be( "{\"Students\":[\"Sartre|3712\"],\"Interns\":[\"Spi|Newbie|3712\",null,\"Houphou\\u00EBt|Boigny|null\",null]}", "Items are NOT typed since their type is final." );
                JsonTestHelper.Roundtrip( directory, root );
            }
        }

    }
}
