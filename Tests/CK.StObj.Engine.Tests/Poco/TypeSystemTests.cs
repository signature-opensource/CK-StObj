using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class TypeSystemTests
    {
        [CKTypeDefiner]
        public interface ILinkedListPart : IPoco
        {
            ILinkedListPart? Next { get; set; }
        }

        public interface IPartWithAnonymous : ILinkedListPart
        {
            ref (int Count, string Name) Anonymous { get; }
        }

        public interface IPartWithRecAnonymous : ILinkedListPart
        {
            ref (int Count, string Name, (int Count, string Name) Inside) RecAnonymous { get; }
        }

        public interface IWithList : ILinkedListPart
        {
            List<(int Count, string Name)> Thing { get; }
        }

        public record struct RecAnonymous( int Count, string Name, (int Count, string Name) Inside );

        public RecAnonymous GetRecAnonymous => default;

        [Test]
        public void AllTypes_and_identity_test()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ILinkedListPart ),
                                                     typeof( IPartWithAnonymous ),
                                                     typeof( IPartWithRecAnonymous ),
                                                     typeof( IWithList ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

            const int basicTypesCount = 18;
            const int pocoTypesCount = 4 + 2; // IPoco and IClosedPoco
            const int listTypesCount = 1;
            const int anonymousTypesCount = 2; //(Count,Name) and (Count,Name,Inside)

            ts.AllTypes.Count.Should().Be( (basicTypesCount + pocoTypesCount + listTypesCount + anonymousTypesCount) * 2 );

            int before = ts.AllTypes.Count;
            IPocoType? tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetRecAnonymous ) )! );
            Debug.Assert( tRec != null );
            ts.AllTypes.Count.Should().Be( before + 2 );
            tRec.Kind.Should().Be( PocoTypeKind.Record );

            IPrimaryPocoType wA = ts.GetPrimaryPocoType( typeof( IPartWithAnonymous ) )!;
            IPocoType countAndName = wA.Fields[0].Type;

            IPrimaryPocoType wR = ts.GetPrimaryPocoType( typeof( IPartWithRecAnonymous ) )!;
            ((IRecordPocoType)wR.Fields[0].Type).Fields[2].Type.Should().BeSameAs( countAndName );
        }

        // Same structure but not same field names.
        public struct SameAsRecAnonymous
        {
            public int A;
            [DefaultValue( "" )]
            public string B;
            public (int, string N) C;
        }

        public SameAsRecAnonymous GetSameNakedAsRecAnonymous => default;

        // Same field names but not same default.
        public struct NotSameAsRecAnonymous
        {
            public int A;
            [DefaultValue( "Not the default string." )]
            public string B;
            public (int, string N) C;
        }

        public NotSameAsRecAnonymous GetNotSameNakedAsRecAnonymous => default;

        [Test]
        public void NakedRecord_embedds_the_default_values()
        {
            var ts = new PocoTypeSystem();

            IPocoType? tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetRecAnonymous ) )! );
            Debug.Assert( tRec != null );
            IPocoType? tSameAsRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetSameNakedAsRecAnonymous ) )! );
            Debug.Assert( tSameAsRec != null );

            tSameAsRec.Should().NotBeSameAs( tRec );

            var tRecDef = tRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;
            var tSameAsRecDef = tSameAsRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tSameAsRecDef.Should().Be( "new(){B = \"\", C = (default, \"\")}" );
            tRecDef.Should().Be( "new(){Name = \"\", Inside = (default, \"\")}" );

            IPocoType? tNotSameAsRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameNakedAsRecAnonymous ) )! );
            Debug.Assert( tNotSameAsRec != null );
            tNotSameAsRec.Should().NotBeSameAs( tRec );
            var tNotSameAsRecDef = tNotSameAsRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tNotSameAsRecDef.Should().Be( "new(){B = @\"Not the default string.\", C = (default, \"\")}" );
        }

        public struct AnEmptyOne { }

        public AnEmptyOne GetAnEmptyOne => default;

        [Test]
        public void an_empty_records_is_handled()
        {
            var ts = new PocoTypeSystem();
            var tEmptyOne = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetAnEmptyOne ) )! );
            Debug.Assert( tEmptyOne != null );
        }

    }
}
