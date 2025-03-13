using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

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

    // This record is read only compliant, hence "hash safe".
    public record struct NamedRec( int Count, string Name, (int Count, string Name) Inside );

    public static NamedRec GetNamedRec => default;

    public interface IWithAllBasicTypes : IPoco
    {
        // 6 basic reference types.
        object? PObject { get; set; } // Must be nullable to have a valid default value.
        string PString { get; set; }
        MCString PMCString { get; set; }
        CodeString PCodeString { get; set; }
        ExtendedCultureInfo PExtendedCultureInfo { get; set; }
        NormalizedCultureInfo PNormalizedCultureInfo { get; set; }

        // 20 basic value types.
        bool PBool { get; set; }
        int PInt { get; set; }
        short PShort { get; set; }
        byte PByte { get; set; }
        long PLong { get; set; }
        double PDouble { get; set; }
        float PFloat { get; set; }
        DateTime PDateTime { get; set; }
        DateTimeOffset PDateTimeOffset { get; set; }
        TimeSpan PTimeSpan { get; set; }
        Guid PGuid { get; set; }
        BigInteger PBigInteger { get; set; }
        decimal PDecimal { get; set; }
        uint PUInt { get; set; }
        ulong PULong { get; set; }
        ushort PUShort { get; set; }
        sbyte PSByte { get; set; }

        // Default of SimpleUserMessage, UserMessage and FormattedString are not valid.
        // We forbid the default for them: they can't be Poco fields unless they are nullables.
        SimpleUserMessage? PSimpleUserMessage { get; set; }
        UserMessage? PUserMessage { get; set; }
        FormattedString? PFormattedString { get; set; }
    }

    [Test]
    public void AllTypes_and_identity_test()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( ILinkedListPart ),
            typeof( IPartWithAnonymous ),
            typeof( IPartWithRecAnonymous ),
            typeof( IWithList ),
            typeof( IWithAllBasicTypes )] );
        var builder = r.PocoTypeSystemBuilder;

        const int basicTypesCount = 26; // See IWithAllBasicTypes.
        const int enumTypesCount = 1; // AnEnum
        const int pocoTypesCount = 5 + 1; // The 5 Pocos + the IPoco.
        const int listTypesCount = 1 + 1 + 1 + 1 + 1; // IList<(int Count, string Name)>
                                                      //  - its ConcreteCollection List<(int Count, string Name)>
                                                      //   - its RegularCollection List<(int,string)>,
                                                      //   - its oblivious IList<(int, string?)>
                                                      //   - and the oblivious's RegularCollection List<(int,string?)> 
        const int anonymousTypesCount = 2 + 2 + 2; //(Count,Name) and (Count,Name,Inside) and their respective unnamed and oblivious types.

        builder.Count.ShouldBe( (basicTypesCount + enumTypesCount + pocoTypesCount + listTypesCount + anonymousTypesCount) * 2 );

        int before = builder.Count;
        var tRec = builder.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNamedRec ) )! );
        Throw.DebugAssert( tRec != null );
        builder.Count.ShouldBe( before + 2 );
        tRec.Kind.ShouldBe( PocoTypeKind.Record );
        ((IRecordPocoType)tRec).IsReadOnlyCompliant.ShouldBeTrue();

        IPrimaryPocoType wA = builder.FindByType<IPrimaryPocoType>( typeof( IPartWithAnonymous ) )!;
        IPocoType countAndName = wA.Fields[0].Type;

        IPrimaryPocoType wR = builder.FindByType<IPrimaryPocoType>( typeof( IPartWithRecAnonymous ) )!;
        ((IRecordPocoType)wR.Fields[0].Type).Fields[2].Type.ShouldBeSameAs( countAndName );

        var tAnonymous = ((IRecordPocoType)tRec).Fields.Single( f => f.Name == "Inside" ).Type as IRecordPocoType;
        Throw.DebugAssert( tAnonymous != null );
        tAnonymous.IsOblivious.ShouldBeFalse();
        tAnonymous.IsAnonymous.ShouldBeTrue();
        tAnonymous.RegularType.ShouldNotBeSameAs( tAnonymous );
        tAnonymous.RegularType.Fields.All( f => f.IsUnnamed && f.Type.IsRegular ).ShouldBeTrue();
        tAnonymous.RegularType.IsOblivious.ShouldBeFalse();

        tAnonymous.RegularType.ObliviousType.ShouldNotBeSameAs( tAnonymous );
        tAnonymous.RegularType.ObliviousType.ShouldNotBeSameAs( tAnonymous.RegularType );

        tAnonymous.RegularType.ObliviousType.ShouldBeSameAs( tAnonymous.ObliviousType );
    }


    [Test]
    public void FindByType_finds_AbstractIPoco_and_PocoClass_implementation_type()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( ILinkedListPart ), typeof( IWithList )] );
        var ts = r.PocoTypeSystemBuilder;

        var p = ts.FindByType<IPrimaryPocoType>( typeof( IWithList ) );
        Throw.DebugAssert( p != null );
        p.IsOblivious.ShouldBeTrue();
        p.IsNullable.ShouldBeTrue();
        p.ShouldBeAssignableTo<IPrimaryPocoType>();

        var p2 = ts.FindByType( typeof( IWithList ) );
        p2.ShouldBeSameAs( p );

        var a = ts.FindByType( typeof( ILinkedListPart ) );
        Debug.Assert( a != null );
        a.IsOblivious.ShouldBeTrue();
        a.IsNullable.ShouldBeTrue();
        a.ShouldBeAssignableTo<IAbstractPocoType>();

        ((IAbstractPocoType)a).AllowedTypes.ShouldContain( p );

        var impl = ts.FindByType( p.FamilyInfo.PocoClass );
        impl.ShouldBeSameAs( p );
    }

    public record struct EmptyRec();

    public interface IValidEmptyRec : IPoco
    {
        ref EmptyRec R { get; }
    }

    [Test]
    public void an_empty_record_is_valid_in_the_Poco_world()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IValidEmptyRec )] );
        var ts = r.CKTypeResult.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );
        var emptyRec = ts.FindByType( typeof( EmptyRec ) );
        var poco = ts.FindByType( typeof( IValidEmptyRec ) );
        Throw.DebugAssert( emptyRec != null && poco != null );
        ts.SetManager.All.Contains( emptyRec ).ShouldBeTrue();
        ts.SetManager.All.Contains( poco ).ShouldBeTrue();
        ts.SetManager.AllSerializable.ShouldBeSameAs( ts.SetManager.All );
        ts.SetManager.AllExchangeable.ShouldBeSameAs( ts.SetManager.All );

        // When excluding empty records:
        var noEmptyRecSet = ts.SetManager.All.ExcludeEmptyRecords();
        noEmptyRecSet.Contains( emptyRec ).ShouldBeFalse();
        noEmptyRecSet.Contains( poco ).ShouldBeTrue();

        // When excluding empty records and empty pocos:
        var noEmptyRecSetAndPoco1 = noEmptyRecSet.ExcludeEmptyPocos();
        var noEmptyRecSetAndPoco2 = ts.SetManager.All.ExcludeEmptyRecordsAndPocos();

        noEmptyRecSetAndPoco1.Contains( emptyRec ).ShouldBeFalse();
        noEmptyRecSetAndPoco2.Contains( emptyRec ).ShouldBeFalse();

        noEmptyRecSetAndPoco1.Contains( poco ).ShouldBeFalse();
        noEmptyRecSetAndPoco2.Contains( poco ).ShouldBeFalse();
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
        ((int)t).ShouldBe( 0 );
        t = 0;
        ((int)t).ShouldBe( 0 );
        // Compilation error.
        // t = 1;
        TestHelper.GetFailedCollectorResult( [typeof( IInvalidEmptyEnum )], "Enum type 'CK.StObj.Engine.Tests.Poco.TypeSystemTests.EmptyEnum' is empty. Empty enum are not valid in a Poco Type System." );
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
    public async Task AbstractPocoField_test_Async( Type impl, string[] names )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractPoco ),
                                        typeof( IWithList ),
                                        impl );
        var engineResult = await configuration.RunSuccessfullyAsync().ConfigureAwait( false );
        var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

        var abs = ts.FindByType<IAbstractPocoType>( typeof( IAbstractPoco ) );
        Debug.Assert( abs != null );
        abs.Fields.Count().ShouldBe( names.Length );
        abs.Fields.Select( f => f.Name ).ShouldBe( names );
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

        var tNotFieldName = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameFieldNameAsNamedRec ) )! );
        Debug.Assert( tNotFieldName != null );

        tNotFieldName.ShouldNotBeSameAs( tRec );

        var tRecDef = tRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;
        var tNotFieldNameDef = tNotFieldName.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

        tRecDef.ShouldBe( "new(){Name = \"\", Inside = (default, \"\")}" );
        tNotFieldNameDef.ShouldBe( "new(){B = \"\", C = (default, \"\")}" );

        var tNotSameDefault = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameDefaultAsNamedRec ) )! );
        Debug.Assert( tNotSameDefault != null );
        tNotSameDefault.ShouldNotBeSameAs( tRec );

        var tNotSameDefaultDef = tNotSameDefault.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

        tNotSameDefaultDef.ShouldBe( "new(){B = @\"Not the default string.\", C = (default, \"\")}" );
    }

    public struct AnEmptyOne : IEquatable<AnEmptyOne>
    {
        public readonly bool Equals( AnEmptyOne other ) => true;

        public override readonly bool Equals( object? obj ) => obj is AnEmptyOne one && Equals( one );

        public static bool operator ==( AnEmptyOne left, AnEmptyOne right ) => left.Equals( right );

        public static bool operator !=( AnEmptyOne left, AnEmptyOne right ) => !(left == right);

        public override readonly int GetHashCode() => 0;
    }

    public record struct NotReadOnlyCompliant( List<int> Mutable );

    public HashSet<NamedRec> SetROCompliantValid => default!;

    public HashSet<NotReadOnlyCompliant> SetROCompliantInvalid => default!;
    public HashSet<IPoco> SetROCompliantInvalidPoco => default!;
    public HashSet<object> SetROCompliantInvalidObject => default!;

    public Dictionary<NamedRec, object> DicROCompliantValid => default!;

    public Dictionary<NotReadOnlyCompliant, object> DicROCompliantInvalid => default!;
    public Dictionary<IPoco, object> DicROCompliantInvalidPoco => default!;
    public Dictionary<object, object> DicROCompliantInvalidObject => default!;

    public IReadOnlySet<object> ROSetROCompliantValidObject => default!;
    public IReadOnlyDictionary<object, object> RODicROCompliantValidObject => default!;

    public IReadOnlySet<NamedRec> ROSetROCompliantValid => default!;

    public IReadOnlySet<NotReadOnlyCompliant> ROSetROCompliantInvalid => default!;
    public IReadOnlySet<IPoco> ROSetROCompliantInvalidPoco => default!;

    public IReadOnlyDictionary<NamedRec, object> RODicROCompliantValid => default!;

    public IReadOnlyDictionary<NotReadOnlyCompliant, object> RODicROCompliantInvalid => default!;
    public IReadOnlyDictionary<IPoco, object> RODicROCompliantInvalidPoco => default!;

    [Test]
    public void IsROCompliant_tests()
    {
        var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );

        // Readonly compliant records are valid keys.
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( SetROCompliantValid ) )! ).ShouldNotBeNull();
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( DicROCompliantValid ) )! ).ShouldNotBeNull();
        // Non readonly compliant records and Poco are invalid.
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( SetROCompliantInvalid ) )! ).ShouldBeNull();
            logs.ShouldContain( "Property 'CK.StObj.Engine.Tests.Poco.TypeSystemTests.SetROCompliantInvalid': " +
                                   "'HashSet<TypeSystemTests.NotReadOnlyCompliant>' item type cannot be " +
                                   "'CK.StObj.Engine.Tests.Poco.TypeSystemTests.NotReadOnlyCompliant' because this type is not read-only compliant." );
        }
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( DicROCompliantInvalid ) )! ).ShouldBeNull();
            logs.ShouldContain( "Property 'CK.StObj.Engine.Tests.Poco.TypeSystemTests.DicROCompliantInvalid': " +
                                   "'Dictionary<TypeSystemTests.NotReadOnlyCompliant,object>' key cannot be " +
                                   "'CK.StObj.Engine.Tests.Poco.TypeSystemTests.NotReadOnlyCompliant' because this type is not read-only compliant." );
        }
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( SetROCompliantInvalidPoco ) )! ).ShouldBeNull();
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( DicROCompliantInvalidPoco ) )! ).ShouldBeNull();

        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( SetROCompliantInvalidObject ) )! ).ShouldBeNull();
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( DicROCompliantInvalidObject ) )! ).ShouldBeNull();

        // ReadOnly
        // This is the same for IReadOnlySet/Dictionary, except that IReadOnlySet/Dictionary<object> is allowed.

        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( ROSetROCompliantValidObject ) )! ).ShouldNotBeNull( "IReadOnlySet<object> is valid." );
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( RODicROCompliantValidObject ) )! ).ShouldNotBeNull( "IReadOnlyDictionary<object,...> is valid." );

        // Readonly compliant records are valid keys.
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( ROSetROCompliantValid ) )! ).ShouldNotBeNull();
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( RODicROCompliantValid ) )! ).ShouldNotBeNull();
        // Non readonly compliant records and Poco are invalid.
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( ROSetROCompliantInvalid ) )! ).ShouldBeNull();
            logs.ShouldContain( "Property 'CK.StObj.Engine.Tests.Poco.TypeSystemTests.ROSetROCompliantInvalid': " +
                                   "'IReadOnlySet<TypeSystemTests.NotReadOnlyCompliant>' item type cannot be " +
                                   "'CK.StObj.Engine.Tests.Poco.TypeSystemTests.NotReadOnlyCompliant' because this type is not read-only compliant." );
        }
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( RODicROCompliantInvalid ) )! ).ShouldBeNull();
            logs.ShouldContain( "Property 'CK.StObj.Engine.Tests.Poco.TypeSystemTests.RODicROCompliantInvalid': " +
                                   "'IReadOnlyDictionary<TypeSystemTests.NotReadOnlyCompliant,object>' key cannot be " +
                                   "'CK.StObj.Engine.Tests.Poco.TypeSystemTests.NotReadOnlyCompliant' because this type is not read-only compliant." );
        }
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( ROSetROCompliantInvalidPoco ) )! ).ShouldBeNull();
        ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( RODicROCompliantInvalidPoco ) )! ).ShouldBeNull();


    }
}
