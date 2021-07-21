using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public partial class PocoUnionTypeTests
    {
        // Error:
        // [UnionType] attribute on 'IInvalidPocoWithUnionTypeMissUnionTypes.Thing' requires a nested 'class UnionTypes { public (int?,string) Thing { get; } }' with the types (here, (int?,string) is just an example of course).
        public interface IInvalidPocoWithUnionTypeMissUnionTypes : IPoco
        {
            [UnionType]
            object Thing { get; set; }
        }

        // Error:
        // The nested class UnionTypes requires a public value tuple 'Thing' property.
        public interface IInvalidPocoWithUnionTypeMissFieldDefinition : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (string, int) NotTheThing { get; }
            }
        }

        // Error:
        // The 'Thing' property of the nested class UnionTypes must be a value tuple (current type is String).
        public interface IInvalidPocoWithUnionTypeInvalidFieldDefinition : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
                public string Thing { get; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            }
        }

        [Test]
        public void Union_definition_must_be_public_properties_in_UnionTypes()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeMissUnionTypes ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeMissFieldDefinition ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeInvalidFieldDefinition ) );
            TestHelper.GetFailedResult( c );
        }

        // Error:
        // Union type 'int' is incompatible with the property type 'String'.
        public interface IInvalidPocoWithUnionTypeMismatch1 : IPoco
        {
            [UnionType]
            string Thing { get; set; }

            class UnionTypes
            {
                public (string, int) Thing { get; }
            }
        }

        // Error:
        // Union types 'string' ,'DateTime' are incompatible with the property type 'IPoco'.
        public interface IInvalidPocoWithUnionTypeMismatch2 : IPoco
        {
            [UnionType]
            IPoco Thing { get; set; }

            class UnionTypes
            {
                public (string, DateTime) Thing { get; }
            }
        }

        // Error:
        // Union type 'IPoco' is incompatible with the property type 'Int32'.
        public interface IInvalidPocoWithUnionTypeMismatch3 : IPoco
        {
            [UnionType]
            int Thing { get; set; }

            class UnionTypes
            {
                public (IPoco, int) Thing { get; }
            }
        }

        // Error:
        // UnionTypes cannot define the type 'object' since this would erase all possible types.
        public interface IInvalidPocoWithUnionTypeObject : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (int,object) Thing { get; }
            }
        }

        [Test]
        public void Union_property_types_must_all_be_assignable_to_the_union_property()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeMismatch1 ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeMismatch2 ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeMismatch3 ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionTypeObject ) );
            TestHelper.GetFailedResult( c );
        }

        // Error:
        // Union type 'int?' must NOT be nullable since 'Object Thing { get; }' is not nullable.
        public interface INotNullablePropertyConflict1 : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (string, int?) Thing { get; }
            }
        }

        // Error:
        // Union types 'IPoco?' ,'string?' must NOT be nullable since 'Object Thing { get; }' is not nullable.
        public interface INotNullablePropertyConflict2 : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (IPoco?, string?, int) Thing { get; }
            }
        }

        [Test]
        public void Not_nullable_Union_property_requires_NOT_nullable_union_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( INotNullablePropertyConflict1 ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( INotNullablePropertyConflict2 ) );
            TestHelper.GetFailedResult( c );
        }

        // Error:
        // None of the union types are nullable but 'Object? Thing { get; }' is nullable.
        public interface INullablePropertyConflict : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            class UnionTypes
            {
                public (string, int) Thing { get; }
            }
        }

        [Test]
        public void Nullable_Union_property_expects_at_least_ONE_nullable_union_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( INullablePropertyConflict ) );
            TestHelper.GetFailedResult( c );
        }

        public interface IPocoWithUnionType : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            [UnionType]
            object AnotherThing { get; set; }

            class UnionTypes
            {
                public (int?, string?, IList<string>) Thing { get; }
                public (int, string, IList<string?>) AnotherThing { get; }
            }
        }

        [Test]
        public void Union_property_implementation_guards_the_setter_and_null_is_allowed_if_one_of_the_variant_is_nullable()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithUnionType ), typeof( PocoJsonSerializer ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var p = s.GetRequiredService<IPocoFactory<IPocoWithUnionType>>().Create();

            p.Thing = 34;
            p.Thing.Should().Be( 34 );
            p.Thing = "lklk";
            p.Thing.Should().Be( "lklk" );
            p.Thing = null;
            p.Thing.Should().BeNull();

            p.Invoking( x => x.Thing = 25.88 ).Should().Throw<ArgumentException>();
            p.Invoking( x => x.Thing = this ).Should().Throw<ArgumentException>();

            // AnotherThing must not be null.
            p.AnotherThing = 3;
            var p2 = JsonTestHelper.Roundtrip( directory, p );
            p.Should().BeEquivalentTo( p2 );
        }

        public class Person { }
        public class Student : Person { }

        [ExternalName("I1")]
        public interface IPocoWithDuplicatesUnionTypes1 : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            class UnionTypes
            {
                public (int?, IEnumerable<string>, IList<string>, string) Thing { get; }
            }
        }

        [ExternalName( "I2" )]
        public interface IPocoWithDuplicatesUnionTypes2 : IPoco
        {
            [UnionType]
            object AnotherThing { get; set; }

            class UnionTypes
            {
                public (Person, Student) AnotherThing { get; }
            }
        }

        [ExternalName( "I3" )]
        public interface IPocoWithDuplicatesUnionTypes3 : IPoco
        {
            [UnionType]
            object YetAnotherThing { get; set; }

            class UnionTypes
            {
                public (int, Student, string, Person, string, string, string ) YetAnotherThing { get; }
            }
        }

        [Test]
        public void IsAssignableFrom_and_duplicates_are_removed()
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( IPocoWithDuplicatesUnionTypes1 ) );
                TestHelper.GetSuccessfulResult( c );

                entries.Select( e => e.Text ).Should()
                    .Contain( t => t.Contains( "'System.Collections.Generic.IEnumerable<string>' is assignable from (is more general than) 'System.Collections.Generic.IList<string>'. Removing the second one.", StringComparison.Ordinal ) );
            }
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( IPocoWithDuplicatesUnionTypes2 ) );
                TestHelper.GetSuccessfulResult( c );

                entries.Select( e => e.Text ).Should()
                    .Contain( t => t.Contains( "'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.Person' is assignable from (is more general than) 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.Student'. Removing the second one.", StringComparison.Ordinal ) )
                    .And.Contain( t => t.Contains( "UnionType contains only one type. This is weird (but ignored).", StringComparison.Ordinal ) );
            }
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( IPocoWithDuplicatesUnionTypes3 ) );
                TestHelper.GetSuccessfulResult( c );

                entries.Select( e => e.Text ).Should()
                    .Contain( t => t.Contains( "Property 'YetAnotherThing' of type 'object' on Poco interfaces: 'I3'. UnionType 'string' duplicated. Removing one.", StringComparison.Ordinal ) );

                entries.Select( e => e.Text ).Should()
                    .Contain( t => t.Contains( "'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.Person' is assignable from (is more general than) 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.Student'. Removing the second one.", StringComparison.Ordinal ) );
            }
        }

        [ExternalName( "I4" )]
        public interface IPocoWithConcreteListInUnion : IPoco
        {
            [UnionType]
            object ConcreteList { get; set; }

            class UnionTypes
            {
                public (int, List<string>) ConcreteList { get; }
            }
        }

        [ExternalName( "I4" )]
        public interface IPocoWithConcreteDictionaryInUnion : IPoco
        {
            [UnionType]
            object ConcreteDictionary { get; set; }

            class UnionTypes
            {
                public (int, Dictionary<string,int>) ConcreteDictionary { get; }
            }
        }

        [ExternalName( "I5" )]
        public interface IPocoWithHashSetInUnion : IPoco
        {
            [UnionType]
            object ConcreteSet { get; set; }

            class UnionTypes
            {
                public (int, HashSet<string>) ConcreteSet { get; }
            }
        }

        [Test]
        public void Union_property_types_must_be_ISet_IDictionary_and_IList_not_corresponding_concrete_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithConcreteListInUnion ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IPocoWithConcreteDictionaryInUnion ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IPocoWithHashSetInUnion ) );
            TestHelper.GetFailedResult( c );
        }

        [Test]
        public void boxing_lifts_nullable_value_types_so_nullable_value_type_in_union_when_set_can_only_result_in_value_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithDuplicatesUnionTypes1 ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var p = s.GetRequiredService<IPocoFactory<IPocoWithDuplicatesUnionTypes1>>().Create();

            FluentActions.Invoking( () => p.Thing = 5 ).Should().NotThrow();
            p.Thing.Should().NotBeOfType<int?>().And.Subject.Should().BeOfType<int>( "Unfortunately..." );

            ((object)(byte?)5).Should().BeOfType<byte>( "Here is why." );
        }

        public interface IPocoWithNullableAndNotNullableUnionTypes1 : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            class UnionTypes
            {
                public (string?, string, int, int?) Thing { get; }
            }
        }

        [Test]
        public void Nullables_are_removed_union_contain_only_non_nullable_types()
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( IPocoWithNullableAndNotNullableUnionTypes1 ) );
                TestHelper.GetSuccessfulResult( c );

                entries.Select( e => e.Text ).Should()
                    .Contain( t => t.Contains( "UnionType 'string' duplicated. Removing one.", StringComparison.Ordinal ) )
                    .And.Contain( t => t.Contains( "UnionType 'int' duplicated. Removing one.", StringComparison.Ordinal ));
            }
        }

        public interface ICompositeOfNullableOrNotNullableValueTypes : IPoco
        {
            [UnionType]
            object ListOfNullableValueTypesOrNotNullable { get; set; }

            class UnionTypes
            {
                public (IList<int>, IList<int?>) ListOfNullableValueTypesOrNotNullable { get; }
            }
        }

        [Test]
        public void generics_of_nullable_and_not_nullable_value_types_are_diffferent()
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( ICompositeOfNullableOrNotNullableValueTypes ) );
                TestHelper.GetSuccessfulResult( c );

                entries.Select( e => e.Text ).Should()
                    .NotContain( t => t.Contains( "Removing the non nullable one.", StringComparison.Ordinal ) );
            }
        }

        public interface ICompositeOfNullableOrNotNullableRefTypes : IPoco
        {
            [UnionType]
            object ListOfNullableReferenceTypesOrNotNullable { get; set; }

            class UnionTypes
            {
                public (IList<string>, IList<string?>) ListOfNullableReferenceTypesOrNotNullable { get; }
            }
        }

        [Test]
        public void generics_of_nullable_and_not_nullable_reference_types_are_the_same()
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( ICompositeOfNullableOrNotNullableRefTypes ) );
                TestHelper.GetSuccessfulResult( c );

                entries.Select( e => e.Text ).Should()
                    .NotContain( t => t.Contains( "Removing the non nullable one.", StringComparison.Ordinal ) );
            }
        }


        public interface IPocoWithUnionTypeNoNullable : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (decimal, int) Thing { get; }
            }
        }

        [Test]
        public void Union_property_implementation_guards_the_setter_and_null_is_NOT_allowed_if_none_of_the_variant_is_nullable()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithUnionTypeNoNullable ), typeof( PocoJsonSerializer ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var p = s.GetRequiredService<IPocoFactory<IPocoWithUnionTypeNoNullable>>().Create();

            p.Thing = 34;
            p.Thing.Should().Be( 34 );
            p.Thing = (decimal)555;
            p.Thing.Should().Be( (decimal)555 );

            p.Invoking( x => x.Thing = null! ).Should().Throw<ArgumentException>();
            p.Invoking( x => x.Thing = this ).Should().Throw<ArgumentException>();

            var p2 = JsonTestHelper.Roundtrip( directory, p );
            p.Should().BeEquivalentTo( p2 );
        }

    }
}
