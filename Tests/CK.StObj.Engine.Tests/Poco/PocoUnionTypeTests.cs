using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public partial class PocoUnionTypeTests
    {
        // Error:
        // [UnionType] attribute on 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IInvalidPocoWithUnionTypeMissUnionTypes.Thing' requires a nested 'class UnionTypes { public (int,string) Thing { get; } }' with the types (here, (int,string) is just an example of course).
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
        // Property 'Thing' of the nested 'class CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IInvalidPocoWithUnionTypeInvalidFieldDefinition.UnionTypes' must be a value tuple (current type is string).
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
        public void Union_definition_must_be_public_properties_in_nested_class_UnionTypes()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IInvalidPocoWithUnionTypeMissUnionTypes ) );
            TestHelper.GetFailedCollectorResult( c, "[UnionType] attribute on 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IInvalidPocoWithUnionTypeMissUnionTypes.Thing' requires a nested 'class UnionTypes { public (int,string) Thing { get; } }' with the types. Here, (int,string) is just an example of course." );
            c = TestHelper.CreateTypeCollector( typeof( IInvalidPocoWithUnionTypeMissFieldDefinition ) );
            TestHelper.GetFailedCollectorResult( c, "The nested class UnionTypes requires a public value tuple 'Thing' property." );
            c = TestHelper.CreateTypeCollector( typeof( IInvalidPocoWithUnionTypeInvalidFieldDefinition ) );
            TestHelper.GetFailedCollectorResult( c, "Property 'Thing' of the nested 'class CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IInvalidPocoWithUnionTypeInvalidFieldDefinition.UnionTypes' must be a value tuple (current type is string)." );
        }

        public interface IUnionTypePropertyMustBeAnObject : IPoco
        {
            [UnionType]
            string Thing { get; set; }
        }

        [Test]
        public void UnionType_property_must_be_an_object()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IUnionTypePropertyMustBeAnObject ) );
            TestHelper.GetFailedCollectorResult( c, "Property 'Thing' on Poco interfaces: 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IUnionTypePropertyMustBeAnObject' is a UnionType: its type can only be 'object' or 'object?'." );
        }


        // Error:
        // UnionTypes cannot contain the type 'object' since this would erase all possible types.
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
        public void UnionType_definition_must_not_contain_any_object_type()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IInvalidPocoWithUnionTypeObject ) );
            TestHelper.GetFailedCollectorResult( c, "'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IInvalidPocoWithUnionTypeObject.UnionTypes.Thing' cannot define the type 'object' since this would erase all possible types." );
        }

        public interface IUnionTypeDefinitionMustBeNotNullable : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            class UnionTypes
            {
                public (string, int)? Thing { get; }
            }
        }

        public interface IUnionTypeDefinitionMustAllBeNotNullable1 : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (string, int?) Thing { get; }
            }
        }

        public interface IUnionTypeDefinitionMustAllBeNotNullable2 : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            class UnionTypes
            {
                public (IPoco?, string?, int) Thing { get; }
            }
        }

        [Test]
        public void UnionType_definitition_must_always_be_not_nullables()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IUnionTypeDefinitionMustBeNotNullable ) );
            TestHelper.GetFailedCollectorResult( c, "CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IUnionTypeDefinitionMustBeNotNullable.UnionTypes.Thing: union type definition must be a non nullable value tuple." );

            c = TestHelper.CreateTypeCollector( typeof( IUnionTypeDefinitionMustAllBeNotNullable1 ) );
            TestHelper.GetFailedCollectorResult( c, "CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IUnionTypeDefinitionMustAllBeNotNullable1.UnionTypes.Thing: type definition 'int?' must not be nullable: nullability of the union type is defined by the 'object' property nullability." );

            c = TestHelper.CreateTypeCollector( typeof( IUnionTypeDefinitionMustAllBeNotNullable2 ) );
            TestHelper.GetFailedCollectorResult( c,
                "CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IUnionTypeDefinitionMustAllBeNotNullable2.UnionTypes.Thing: type definition 'IPoco' must not be nullable: nullability of the union type is defined by the 'object' property nullability.",
                "CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IUnionTypeDefinitionMustAllBeNotNullable2.UnionTypes.Thing: type definition 'string' must not be nullable: nullability of the union type is defined by the 'object' property nullability." );
        }

        public interface IPocoWithUnionType : IPoco
        {
            [UnionType]
            object? Thing { get; set; }

            [UnionType]
            object AnotherThing { get; set; }

            class UnionTypes
            {
                public (int, string, List<string>) Thing { get; }
                public (int, string, List<string?>) AnotherThing { get; }
            }
        }

        [Test]
        public void Union_property_implementation_guards_the_setter_when_not_nullable()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( IPocoWithUnionType ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var p = auto.Services.GetRequiredService<IPocoFactory<IPocoWithUnionType>>().Create();

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
        }

        public interface IPerson : IPoco { }
        public interface IStudent : IPerson { }

        [ExternalName( "I2" )]
        public interface IPocoWithDuplicatesUnionTypes2 : IPoco
        {
            [UnionType]
            object AnotherThing { get; set; }

            class UnionTypes
            {
                public (IPerson, IStudent) AnotherThing { get; }
            }
        }

        [ExternalName( "I3" )]
        public interface IPocoWithDuplicatesUnionTypes3 : IPoco
        {
            [UnionType]
            object YetAnotherThing { get; set; }

            class UnionTypes
            {
                public (int, IStudent, string, IPerson, string, string, string) YetAnotherThing { get; }
            }
        }

        [Test]
        public void IsAssignableFrom_and_duplicates_are_removed()
        {
            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                var c = TestHelper.CreateTypeCollector( typeof( IPocoWithDuplicatesUnionTypes2 ), typeof( IPerson ), typeof( IStudent ) );
                TestHelper.GetSuccessfulCollectorResult( c );

                logs.Should().Contain( "Property 'AnotherThing' on Poco interfaces: 'I2': UnionType 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IStudent' duplicated. Removing one." );
            }
            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                var c = TestHelper.CreateTypeCollector( typeof( IPocoWithDuplicatesUnionTypes3 ), typeof( IPerson ), typeof( IStudent ) );
                TestHelper.GetSuccessfulCollectorResult( c );

                logs.Should()
                    .Contain( "Property 'YetAnotherThing' on Poco interfaces: 'I3': UnionType 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPerson' duplicated. Removing one." );
                logs.Count( t => t.Contains( "Property 'YetAnotherThing' on Poco interfaces: 'I3': UnionType 'string' duplicated. Removing one." ) )
                    .Should().Be( 3 );
            }
        }

        public interface ICompositeOfNullableOrNotNullableValueTypes : IPoco
        {
            [UnionType]
            object ListOfNullableValueTypesOrNotNullable { get; set; }

            class UnionTypes
            {
                public (List<int>, List<int?>) ListOfNullableValueTypesOrNotNullable { get; }
            }
        }

        [Test]
        public void List_Set_or_Dictionary_of_nullable_and_not_nullable_value_types_are_different()
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateTypeCollector( typeof( ICompositeOfNullableOrNotNullableValueTypes ) );
                TestHelper.GetSuccessfulCollectorResult( c );

                entries.Select( e => e.Text ).Should()
                    .NotContain( t => t.Contains( "duplicated", StringComparison.Ordinal ) );
            }
        }

        public interface ICompositeOfNullableOrNotNullableRefTypes : IPoco
        {
            [UnionType]
            object ListOfNullableReferenceTypesOrNotNullable { get; set; }

            class UnionTypes
            {
                public (List<string>, List<string?>) ListOfNullableReferenceTypesOrNotNullable { get; }
            }
        }

        [Test]
        public void List_Set_or_Dictionary_of_nullable_and_not_nullable_reference_types_are_ambiguous()
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateTypeCollector( typeof( ICompositeOfNullableOrNotNullableRefTypes ) );
                TestHelper.GetFailedCollectorResult( c, "Ambiguous UnionType 'List<string?>' is more general than 'List<string>'. Since CanBeExtended is false, types in the union must be unrelated." );
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
        public void Union_property_implementation_guards_the_setter_and_null_is_NOT_allowed()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( IPocoWithUnionTypeNoNullable ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var p = auto.Services.GetRequiredService<IPocoFactory<IPocoWithUnionTypeNoNullable>>().Create();
            p.Thing.Should().Be( 0m, "Since it's not null, it defaults to the first definition type that is 'defaultable': here the decimal." );

            p.Thing = 34;
            p.Thing.Should().Be( 34 );
            p.Thing = 555m;
            p.Thing.Should().Be( 555m );

            p.Invoking( x => x.Thing = null! ).Should().Throw<ArgumentException>();
            p.Invoking( x => x.Thing = this ).Should().Throw<ArgumentException>();
        }



        public interface IPocoNonExtendable : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (decimal, int, double) Thing { get; }
            }
        }

        public interface IPocoNonExtendableSpecializedMore : IPocoNonExtendable
        {
            [UnionType]
            new object Thing { get; set; }

            new class UnionTypes
            {
                public (decimal, int, double, string) Thing { get; }
            }
        }

        public interface IPocoNonExtendableSpecializedLess : IPocoNonExtendable
        {
            [UnionType]
            new object Thing { get; set; }

            new class UnionTypes
            {
                public (decimal, int) Thing { get; }
            }
        }

        [Test]
        public void Union_property_types_cannot_be_extended_by_default()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IPocoNonExtendable ), typeof( IPocoNonExtendableSpecializedMore ) );
            TestHelper.GetFailedCollectorResult( c, "Property 'Thing' on Poco interfaces: 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendable', 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendableSpecializedMore' is a UnionType that cannot be extended." );
            c = TestHelper.CreateTypeCollector( typeof( IPocoNonExtendable ), typeof( IPocoNonExtendableSpecializedLess ) );
            TestHelper.GetFailedCollectorResult( c, "Property 'Thing' on Poco interfaces: 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendable', 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendableSpecializedLess' is a UnionType that cannot be extended." );
        }

        public interface IPocoNonExtendableIndependent : IPoco
        {
        }

        public interface IPocoNonExtendableIndependentProperty : IPocoNonExtendableIndependent
        {
            [UnionType]
            object? AnotherThing { get; set; }

            class UnionTypes
            {
                public (string[], string, List<string>) AnotherThing { get; }
            }
        }

        public interface IPocoNonExtendableIndependentLess : IPocoNonExtendableIndependent
        {
            [UnionType]
            object? AnotherThing { get; set; }

            class UnionTypes
            {
                public (string[], string) AnotherThing { get; }
            }
        }

        public interface IPocoNonExtendableIndependentMore : IPocoNonExtendableIndependent
        {
            [UnionType]
            object? AnotherThing { get; set; }

            class UnionTypes
            {
                public (string[], string, IList<string>, ISet<string>) AnotherThing { get; }
            }
        }

        [Test]
        public void Union_property_types_cannot_be_extended_by_default_accross_independent_interfaces()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IPocoNonExtendableIndependent ), typeof( IPocoNonExtendableIndependentProperty ), typeof( IPocoNonExtendableIndependentLess ) );
            TestHelper.GetFailedCollectorResult( c, "Property 'AnotherThing' on Poco interfaces: 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendableIndependentProperty', 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendableIndependentLess' is a UnionType that cannot be extended." );
            c = TestHelper.CreateTypeCollector( typeof( IPocoNonExtendableIndependent ), typeof( IPocoNonExtendableIndependentProperty ), typeof( IPocoNonExtendableIndependentMore ) );
            TestHelper.GetFailedCollectorResult( c, "Property 'AnotherThing' on Poco interfaces: 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendableIndependentProperty', 'CK.StObj.Engine.Tests.Poco.PocoUnionTypeTests.IPocoNonExtendableIndependentMore' is a UnionType that cannot be extended." );
        }
    }
}
