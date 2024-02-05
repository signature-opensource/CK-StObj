using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class TypeSystemTests
    {
        public enum AnEnum { Value }

        [CKTypeDefiner]
        // This is currently ignored: an AbstractPoco has no use of an ExternalName, its CSharpName is always used.
        // TODO: this should be an error.
        [ExternalName( "IGNORED" )] 
        public interface ILinkedListPart : IPoco
        {
            ILinkedListPart? Next { get; set; }

            AnEnum AnEnum { get; set; }
        }

        public interface IPartWithAnonymous : ILinkedListPart
        {
            ref (int Count, string Name) Anonymous { get; }
        }

        [ExternalName( "ExternalNameForPartWithRecAnonymous" )]
        public interface IPartWithRecAnonymous : ILinkedListPart
        {
            ref (int Count, string Name, (int Count, string Name) Inside) RecAnonymous { get; }
        }

        public interface IWithList : ILinkedListPart
        {
            IList<(int Count, string Name)> Thing { get; }
        }

        public record struct NamedRec( int Count, string Name, (int Count, string Name) Inside );

        public static NamedRec GetNamedRec => default;

        [Test]
        public void AllTypes_and_identity_test()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ILinkedListPart ),
                                                     typeof( IPartWithAnonymous ),
                                                     typeof( IPartWithRecAnonymous ),
                                                     typeof( IWithList ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var builder = r.PocoTypeSystemBuilder;

            const int basicTypesCount = 19;
            const int globalizationTypesCount = 7; // SimpleUserMessage, UserMessage, FormattedString, MCString, CodeString, NormalizedCultureInfo, ExtendedCultureInfo.
            const int enumTypesCount = 1; // AnEnum
            const int pocoTypesCount = 4 + 2; // IPoco and IClosedPoco
            const int listTypesCount = 1 + 1; // List<(int,string)> and List<(int Count, string Name)>
            const int anonymousTypesCount = 2 + 2; //(Count,Name) and (Count,Name,Inside) and their respective oblivious types.

            builder.Count.Should().Be( (basicTypesCount + globalizationTypesCount + enumTypesCount + pocoTypesCount + listTypesCount + anonymousTypesCount) * 2 );

            int before = builder.Count;
            var tRec = builder.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNamedRec ) )! );
            Debug.Assert( tRec != null );
            builder.Count.Should().Be( before + 2 );
            tRec.Kind.Should().Be( PocoTypeKind.Record );
            tRec.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.NamedRec" );

            IPrimaryPocoType wA = builder.FindByType<IPrimaryPocoType>( typeof( IPartWithAnonymous ) )!;
            wA.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.IPartWithAnonymous" );
            IPocoType countAndName = wA.Fields[0].Type;

            IPrimaryPocoType wR = builder.FindByType<IPrimaryPocoType>( typeof( IPartWithRecAnonymous ) )!;
            wR.StandardName.Should().Be( "ExternalNameForPartWithRecAnonymous" );
            ((IRecordPocoType)wR.Fields[0].Type).Fields[2].Type.Should().BeSameAs( countAndName );

            wR.Fields[0].Type.StandardName.Should().Be( "(int:Count,string:Name,(int:Count,string:Name):Inside)" );
            wR.Fields[1].Type.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.ILinkedListPart?" );
            wR.Fields[2].Type.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.AnEnum" );
        }


        [Test]
        public void FindObliviousType_finds_AbstractIPoco_and_PocoClass_implementation_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ILinkedListPart ),
                                                     typeof( IWithList ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.PocoTypeSystemBuilder;

            var p = ts.FindByType<IPrimaryPocoType>( typeof( IWithList ) );
            Debug.Assert( p != null );
            p.IsNullable.Should().BeFalse();
            p.IsOblivious.Should().BeTrue();
            p.Should().BeAssignableTo<IPrimaryPocoType>();

            var p2 = ts.FindByType( typeof( IWithList ) );
            p2.Should().BeSameAs( p );

            var a = ts.FindByType( typeof( ILinkedListPart ) );
            Debug.Assert( a != null );
            a.Should().BeAssignableTo<IAbstractPocoType>();

            ((IAbstractPocoType)a).AllowedTypes.Should().Contain( p );

            var impl = ts.FindByType( p.FamilyInfo.PocoClass );
            impl.Should().BeSameAs( p );
        }

        public record struct EmptyRec();

        public interface IValidEmptyRec : IPoco
        {
            ref EmptyRec R { get; }
        }

        [Test]
        public void an_empty_record_is_valid_in_the_Poco_world()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IValidEmptyRec ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystemBuilder.Lock();
            var emptyRec = ts.FindByType( typeof( EmptyRec ) );
            var poco = ts.FindByType( typeof( IValidEmptyRec ) );
            Throw.DebugAssert( emptyRec != null && poco != null );
            ts.SetManager.All.Contains( emptyRec ).Should().BeTrue();
            ts.SetManager.All.Contains( poco ).Should().BeTrue();
            ts.SetManager.AllSerializable.Should().BeSameAs( ts.SetManager.All );
            ts.SetManager.AllExchangeable.Should().BeSameAs( ts.SetManager.All );

            // When excluding empty records:
            var noEmptyRecSet = ts.SetManager.All.ExcludeEmptyRecords();
            noEmptyRecSet.Contains( emptyRec ).Should().BeFalse();
            noEmptyRecSet.Contains( poco ).Should().BeTrue();

            // When excluding empty records and empty pocos:
            var noEmptyRecSetAndPoco1 = noEmptyRecSet.ExcludeEmptyPocos();
            var noEmptyRecSetAndPoco2 = ts.SetManager.All.ExcludeEmptyRecordsAndPocos();

            noEmptyRecSetAndPoco1.Contains( emptyRec ).Should().BeFalse();
            noEmptyRecSetAndPoco2.Contains( emptyRec ).Should().BeFalse();

            noEmptyRecSetAndPoco1.Contains( poco ).Should().BeFalse();
            noEmptyRecSetAndPoco2.Contains( poco ).Should().BeFalse();
        }

        public enum EmptyEnum
        {
        }

        public interface IInvalidEmptyEnum : IPoco
        {
            EmptyEnum Invalid { get; set; }
        }

        [Test]
        public void even_if_an_empty_enum_is_csharp_valid_it_is_invalid_in_the_Poco_world()
        {
            EmptyEnum t = default;
            t.Should().Be( 0 );
            t = 0;
            t.Should().Be( 0 );
            // Compilation error.
            // t = 1;
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidEmptyEnum ) );
            TestHelper.GetFailedResult( c, "Enum type 'CK.StObj.Engine.Tests.Poco.TypeSystemTests.EmptyEnum' is empty. Empty enum are not valid in a Poco Type System." );
        }


        [CKTypeDefiner]
        public interface IAbstractPoco : IPoco
        {
            IWithList? Optional { get; }

            IWithList Required { get; }

            int Power { get; }
        }

        public interface IWillHaveOnlyRequired : IAbstractPoco
        {
        }

        public interface IWithPower : IAbstractPoco
        {
            new int Power { get; set; }
        }

        public interface IWithOptional : IAbstractPoco
        {
            new IWithList? Optional { get; set; }
        }

        public interface IWithAll : IAbstractPoco
        {
            new int Power { get; set; }
            new IWithList? Optional { get; set; }
        }

        [TestCase( typeof( IWillHaveOnlyRequired ), new[] { "Required" } )]
        [TestCase( typeof( IWithPower ), new[] { "Required", "Power" } )]
        [TestCase( typeof( IWithOptional ), new[] { "Required", "Optional" } )]
        [TestCase( typeof( IWithAll ), new[] { "Required", "Optional", "Power" } )]
        public void AbstractPocoField_test( Type impl, string[] names )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractPoco ),
                                                     typeof( IWithList ),
                                                     impl );
            var r = TestHelper.GenerateCode( c, null, generateSourceFile: true, CompileOption.Compile );
            var ts = r.CollectorResult.PocoTypeSystemBuilder;
            var abs = ts.FindByType<IAbstractPocoType>( typeof( IAbstractPoco ) );
            Debug.Assert( abs != null );
            abs.Fields.Should().HaveCount( names.Length );
            abs.Fields.Select( f => f.Name ).Should().BeEquivalentTo( names );
        }

        // Same structure but not same field names.
        public struct NotSameFieldNameAsNamedRec : IEquatable<NotSameFieldNameAsNamedRec>
        {
            public int A;
            [DefaultValue( "" )]
            public string B;
            public (int, string N) C;

            public readonly bool Equals( NotSameFieldNameAsNamedRec other )
            {
                return A == other.A && B == other.B && EqualityComparer<(int, string)>.Default.Equals( C, other.C );
            }

            public override readonly bool Equals( object? obj ) => obj is NotSameFieldNameAsNamedRec rec && Equals( rec );

            public override readonly int GetHashCode() => HashCode.Combine( A, B, C );

            public static bool operator ==( NotSameFieldNameAsNamedRec left, NotSameFieldNameAsNamedRec right ) => left.Equals( right );

            public static bool operator !=( NotSameFieldNameAsNamedRec left, NotSameFieldNameAsNamedRec right ) => !(left == right);
        }

        public static NotSameFieldNameAsNamedRec GetNotSameFieldNameAsNamedRec => default;

        // Same field names but not same default.
        public struct NotSameDefaultAsNamedRec : IEquatable<NotSameDefaultAsNamedRec>
        {
            public int A;
            [DefaultValue( "Not the default string." )]
            public string B;
            public (int, string N) C;

            public readonly bool Equals( NotSameDefaultAsNamedRec other )
            {
                return A == other.A && B == other.B && EqualityComparer<(int, string)>.Default.Equals( C, other.C );
            }

            public override readonly bool Equals( object? obj ) => obj is NotSameDefaultAsNamedRec rec && Equals( rec );

            public override readonly int GetHashCode() => HashCode.Combine( A, B, C );

            public static bool operator ==( NotSameDefaultAsNamedRec left, NotSameDefaultAsNamedRec right ) => left.Equals( right );

            public static bool operator !=( NotSameDefaultAsNamedRec left, NotSameDefaultAsNamedRec right ) => !(left == right);
        }

        public static NotSameDefaultAsNamedRec GetNotSameDefaultAsNamedRec => default;

        [Test]
        public void named_record_embedds_the_default_values()
        {
            var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );

            var tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNamedRec ) )! );
            Debug.Assert( tRec != null );
            tRec.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.NamedRec" );

            var tNotFieldName = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameFieldNameAsNamedRec ) )! );
            Debug.Assert( tNotFieldName != null );

            tNotFieldName.Should().NotBeSameAs( tRec );
            tNotFieldName.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.NotSameFieldNameAsNamedRec" );

            var tRecDef = tRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;
            var tNotFieldNameDef = tNotFieldName.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tRecDef.Should().Be( "new(){Name = \"\", Inside = (default, \"\")}" );
            tNotFieldNameDef.Should().Be( "new(){B = \"\", C = (default, \"\")}" );

            var tNotSameDefault = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameDefaultAsNamedRec ) )! );
            Debug.Assert( tNotSameDefault != null );
            tNotSameDefault.Should().NotBeSameAs( tRec );
            tNotSameDefault.StandardName.Should().Be( "CK.StObj.Engine.Tests.Poco.TypeSystemTests.NotSameDefaultAsNamedRec" );

            var tNotSameDefaultDef = tNotSameDefault.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tNotSameDefaultDef.Should().Be( "new(){B = @\"Not the default string.\", C = (default, \"\")}" );
        }

        public struct AnEmptyOne : IEquatable<AnEmptyOne>
        {
            public readonly bool Equals( AnEmptyOne other ) => true;

            public override readonly bool Equals( object? obj ) => obj is AnEmptyOne one && Equals( one );

            public static bool operator ==( AnEmptyOne left, AnEmptyOne right ) => left.Equals( right );

            public static bool operator !=( AnEmptyOne left, AnEmptyOne right ) => !(left == right);

            public override readonly int GetHashCode() => 0;
        }

    }

}
