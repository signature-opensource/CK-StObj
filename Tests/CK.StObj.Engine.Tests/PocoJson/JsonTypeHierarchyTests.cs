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
        [ExternalName("CP")]
        public class Person
        {
            public Person( string name )
            {
                if( name.Contains( '|', StringComparison.Ordinal ) ) throw new ArgumentException( "Invalid | in name.", nameof( Name ) );
                Name = name;
            }

            public string Name { get; }

            public static Person Parse( string s ) => new Person( s );

            public override string ToString() => Name;

        }

        [ExternalName( "CS:CP" )]
        public class Student : Person
        {
            public Student( string name, int grade )
                : base( name )
            {
                Grade = grade;
            }

            public int Grade { get; set; }

            public new static Student Parse( string s )
            {
                var p = s.Split( "|" );
                return new Student( p[0], int.Parse( p[1] ) );
            }

            public override string ToString() => $"{Name}|{Grade}";
        }

        [ExternalName( "CT:CP" )]
        public class Teacher : Person
        {
            public Teacher( string name, string currentLevel )
                : base( name )
            {
                CurrentLevel = currentLevel;
            }

            public string CurrentLevel { get; set; }

            public new static Teacher Parse( string s )
            {
                var p = s.Split( "|" );
                return new Teacher( p[0], p[1] );
            }

            public override string ToString() => $"{Name}|{CurrentLevel}";
        }

        [ExternalName( "TestWithPersonTeacherAndStudent" )]
        public interface ITest : IPoco
        {
            Person? Person { get; set; }
            Teacher? Teacher { get; set; }
            Student? Student { get; set; }
        }


        [Test]
        public void extending_json_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( JsonStringParseSupport ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var root = s.GetService<IPocoFactory<ITest>>().Create();
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
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( JsonStringParseSupport ), typeof( ITestBaseClassOnly ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var root = s.GetService<IPocoFactory<ITestBaseClassOnly>>().Create();
            root.Person =  new Student( "Sartre", 3712 );

            // The Parson is not IsFinal: the actual Student is serialized.
            // Here, the serialization relies on ToString() that is virtual (so everything works fine). 
            root.ToString().Should().Be( "{\"Person\":[\"CS:CP\",\"Sartre|3712\"]}" );

            // And the deserialization, based on the type name, calls Student.Parse.
            JsonTestHelper.Roundtrip( directory, root );
        }


        public interface ITestWithCollections : IPoco
        {
            List<Person> Persons { get; }
        }

        public interface ITestWithCollectionsOfFinal : IPoco
        {
            List<Student> Students { get; }
            List<Teacher?> Teachers { get; }
        }

        [Test]
        public void collections_use_types_when_needed()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ),
                                                     typeof( JsonStringParseSupport ),
                                                     typeof( ITestWithCollections ),
                                                     typeof( ITestWithCollectionsOfFinal ),
                                                     typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            {
                var root = s.GetService<IPocoFactory<ITestWithCollections>>().Create();
                root.Persons.Add( new Student( "Sartre", 3712 ) );
                root.Persons.Add( new Teacher( "Camus", "Sisyphe" ) );
                root.Persons.Add( new Person( "Albert" ) );

                root.ToString().Should().Be( "{\"Persons\":[[\"CS:CP\",\"Sartre|3712\"],[\"CT:CP\",\"Camus|Sisyphe\"],[\"CP\",\"Albert\"]]}", "Items MUST HAVE a type." );
                JsonTestHelper.Roundtrip( directory, root );
            }
            {
                var root = s.GetService<IPocoFactory<ITestWithCollectionsOfFinal>>().Create();
                root.Students.Add( new Student( "Sartre", 3712 ) );
                root.Teachers.Add( new Teacher( "Camus", "Sisyphe" ) );
                root.Teachers.Add( null );
                root.Teachers.Add( new Teacher( "Houphouët", "Boigny" ) );
                root.Teachers.Add( null );

                root.ToString().Should().Be( "{\"Students\":[\"Sartre|3712\"],\"Teachers\":[\"Camus|Sisyphe\",null,\"Houphou\\u00EBt|Boigny\",null]}", "Items are NOT typed since their type is final." );
                JsonTestHelper.Roundtrip( directory, root );
            }
        }



    }
}
