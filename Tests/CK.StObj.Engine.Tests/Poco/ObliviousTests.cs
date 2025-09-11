using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class ObliviousTests
{
    [ExternalName( "ExternalNameForVerySimplePoco" )]
    public interface IVerySimplePoco : IPoco
    {
        int Value { get; set; }
    }

    // We need (at least) a secondary IPoco to test the CK.GRSupport.PocoList_*_CK adapter.
    // For list, when there's only the primary, we use the List<primary> that is enough because
    // IReadOnlyList is covariant and there's no list implementation to adapt.
    public interface ISecondaryVerySimplePoco : IVerySimplePoco
    {
    }

    public List<int> ListV = null!; // Oblivious
    public List<int?> ListNV = null!; // Oblivious

    public List<object> ListR = null!;
    public List<object?> ListNR = null!;// Oblivious

    public List<IVerySimplePoco> ListPR = null!;
    public List<IVerySimplePoco?> ListPNR = null!;// Oblivious

    public List<ISecondaryVerySimplePoco> ListSecPR = null!;
    public List<ISecondaryVerySimplePoco?> ListSecPNR = null!;// Oblivious

    public interface IProperListDefinition : IPoco
    {
        IList<int?> IListNV { get; }
        IList<int> IListV { get; }
        IList<object?> IListNR { get; }
        IList<object> IListR { get; }
        IList<IVerySimplePoco?> IListPNR { get; }
        IList<IVerySimplePoco> IListPR { get; }
    }

    [TestCase( false )]
    [TestCase( true )]
    public void oblivious_List( bool revert )
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IVerySimplePoco ),
            typeof( ISecondaryVerySimplePoco ),
            typeof( IProperListDefinition )] );
        var ts = r.PocoTypeSystemBuilder;

        if( !revert )
        {
            // List of Value type
            // List<int> (oblivious, final)
            ICollectionPocoType? tRV = List_Int( ts );
            // List<int?> (oblivious, final)
            ICollectionPocoType? tRNV = List_IntN( ts );
            // IList<int> (oblivious, final)
            ICollectionPocoType? tIV = IList_Int( ts );
            // IList<int?> (oblivious, final)
            ICollectionPocoType? tINV = IList_IntN( ts );

            // List of Reference type (object)
            // List<object?> (oblivious, final)
            ICollectionPocoType? tRNR = List_ObjectN( revert, ts );
            // List<object> (non oblivious, non final)
            ICollectionPocoType? tRR = List_Object( ts, tRNR );
            // IList<object?> (oblivious, non final)
            ICollectionPocoType? tINR = IList_ObjectN( revert, ts, tRNR );
            // IList<object> (non oblivious, non final)
            ICollectionPocoType? tIR = IList_Object( revert, ts, tINR, tRNR );

            // List of Reference type but IVerySimplePoco.
            var n = typeof( IVerySimplePoco ).ToCSharpName();
            // List<IVerySimplePoco?> (oblivious, final)
            ICollectionPocoType? tPRNR = List_PocoN( revert, ts, n );
            // List<IVerySimplePoco> (non oblivious, non final)
            ICollectionPocoType? tPRR = List_Poco( ts, n, tPRNR );
            // IList<IVerySimplePoco?> (oblivious, final)
            ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n );
            // IList<IVerySimplePoco> (non oblivious, non final)
            ICollectionPocoType? tPIR = IList_Poco( revert, ts, n, tPINR );

            var nSec = typeof( ISecondaryVerySimplePoco ).ToCSharpName();
            // List<ISecondaryVerySimplePoco?> (oblivious, final)
            ICollectionPocoType? tSPRNR = List_SecPocoN( revert, ts, nSec );
            // List<ISecondaryVerySimplePoco> (non oblivious, non final)
            ICollectionPocoType? tSPRR = List_SecPoco( ts, nSec, tSPRNR );

        }
        else
        {
            var nSec = typeof( ISecondaryVerySimplePoco ).ToCSharpName();
            // List<ISecondaryVerySimplePoco (non oblivious, non final)
            ICollectionPocoType? tSPRR = List_SecPoco( ts, nSec, null! );
            // List<ISecondaryVerySimplePoco?> (oblivious, final)
            ICollectionPocoType? tSPRNR = List_SecPocoN( revert, ts, nSec );

            // List of Reference type but IVerySimplePoco.
            var n = typeof( IVerySimplePoco ).ToCSharpName();
            // IList<IVerySimplePoco> (non oblivious, non final)
            ICollectionPocoType? tPIR = IList_Poco( revert, ts, n, null! );
            // IList<IVerySimplePoco?> (oblivious, final)
            ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n );
            // List<IVerySimplePoco (non oblivious, non final)
            ICollectionPocoType? tPRR = List_Poco( ts, n, null! );
            // List<IVerySimplePoco?> (oblivious, final)
            ICollectionPocoType? tPRNR = List_PocoN( revert, ts, n );

            tSPRR.ObliviousType.ShouldBeSameAs( tSPRNR.Nullable );
            tSPRR.StructuralFinalType.ShouldBeSameAs( tSPRNR.Nullable );
            tPRR.ObliviousType.ShouldBeSameAs( tPRNR.Nullable );
            tPRR.StructuralFinalType.ShouldBeSameAs( tPRNR.Nullable );
            tPIR.ObliviousType.ShouldBeSameAs( tPINR.Nullable );
            tPIR.StructuralFinalType.ShouldBeSameAs( tPINR.Nullable );

            // List of Reference type (object)
            // IList<object> (non oblivious, non final)
            ICollectionPocoType? tIR = IList_Object( revert, ts, null!, null! );
            // IList<object?> (oblivious, non final)
            ICollectionPocoType? tINR = IList_ObjectN( revert, ts, null! );
            // List<object> (non oblivious, non final)
            ICollectionPocoType? tRR = List_Object( ts, null! );
            // List<object?> (oblivious, final)
            ICollectionPocoType? tRNR = List_ObjectN( revert, ts );

            tRR.ObliviousType.ShouldBeSameAs( tRNR.Nullable );
            tRR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );
            tINR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );
            tIR.ObliviousType.ShouldBeSameAs( tINR.Nullable );
            tIR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );

            // List of Value type
            // IList<int?> (oblivious, final)
            ICollectionPocoType? tINV = IList_IntN( ts );
            // IList<int> (oblivious, final)
            ICollectionPocoType? tIV = IList_Int( ts );
            // List<int?> (oblivious, final)
            ICollectionPocoType? tRNV = List_IntN( ts );
            // List<int> (oblivious, final)
            ICollectionPocoType? tRV = List_Int( ts );
        }

        ICollectionPocoType List_Int( IPocoTypeSystemBuilder ts )
        {
            var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListV ) )! );
            Throw.DebugAssert( tRV != null );
            tRV.IsNullable.ShouldBeFalse();
            tRV.IsOblivious.ShouldBeFalse();
            tRV.Nullable.IsOblivious.ShouldBeTrue();
            tRV.CSharpName.ShouldBe( "List<int>" );
            tRV.ImplTypeName.ShouldBe( "List<int>" );
            tRV.IsStructuralFinalType.ShouldBeFalse();
            tRV.Nullable.IsStructuralFinalType.ShouldBeTrue();
            return tRV;
        }

        ICollectionPocoType List_IntN( IPocoTypeSystemBuilder ts )
        {
            var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNV ) )! );
            Debug.Assert( tRNV != null );
            tRNV.IsNullable.ShouldBeFalse();
            tRNV.IsOblivious.ShouldBeFalse();
            tRNV.Nullable.IsOblivious.ShouldBeTrue();
            tRNV.CSharpName.ShouldBe( "List<int?>" );
            tRNV.ImplTypeName.ShouldBe( "List<int?>" );
            tRNV.IsStructuralFinalType.ShouldBeFalse();
            tRNV.Nullable.IsStructuralFinalType.ShouldBeTrue();
            return tRNV;
        }

        ICollectionPocoType IList_Int( IPocoTypeSystemBuilder ts )
        {
            var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
            var tIV = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListV ) ).Type;
            Debug.Assert( tIV != null );
            tIV.IsNullable.ShouldBeFalse();
            tIV.Nullable.IsOblivious.ShouldBeTrue();
            tIV.CSharpName.ShouldBe( "IList<int>" );
            tIV.ImplTypeName.ShouldBe( "CovariantHelpers.CovNotNullValueList<int>" );
            tIV.IsStructuralFinalType.ShouldBeFalse();
            tIV.Nullable.IsStructuralFinalType.ShouldBeTrue();
            return tIV;
        }

        ICollectionPocoType IList_IntN( IPocoTypeSystemBuilder ts )
        {
            var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
            var tINV = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListNV ) ).Type;
            Debug.Assert( tINV != null );
            tINV.IsNullable.ShouldBeFalse();
            tINV.Nullable.IsOblivious.ShouldBeTrue();
            tINV.CSharpName.ShouldBe( "IList<int?>" );
            tINV.ImplTypeName.ShouldBe( "CovariantHelpers.CovNullableValueList<int>" );
            tINV.IsStructuralFinalType.ShouldBeFalse();
            tINV.Nullable.IsStructuralFinalType.ShouldBeTrue();
            return tINV;
        }


        ICollectionPocoType List_ObjectN( bool revert, IPocoTypeSystemBuilder ts )
        {
            var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNR ) )! );
            Debug.Assert( tRNR != null );
            tRNR.IsNullable.ShouldBeFalse();
            tRNR.IsOblivious.ShouldBeFalse();
            tRNR.Nullable.IsOblivious.ShouldBeTrue();
            tRNR.CSharpName.ShouldBe( "List<object?>" );
            tRNR.ImplTypeName.ShouldBe( "List<object?>" );
            tRNR.IsStructuralFinalType.ShouldBeFalse();
            tRNR.Nullable.IsStructuralFinalType.ShouldBeTrue();
            return tRNR;
        }

        ICollectionPocoType List_Object( IPocoTypeSystemBuilder ts, ICollectionPocoType tRNR )
        {
            var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListR ) )! );
            Debug.Assert( tRR != null );
            tRR.IsNullable.ShouldBeFalse();
            tRR.IsOblivious.ShouldBeFalse();
            tRR.Nullable.IsOblivious.ShouldBeFalse();
            tRR.CSharpName.ShouldBe( "List<object>" );
            tRR.ImplTypeName.ShouldBe( "List<object?>" );
            tRR.IsStructuralFinalType.ShouldBeFalse();
            tRR.Nullable.IsStructuralFinalType.ShouldBeFalse();
            if( !revert )
            {
                tRR.ObliviousType.ShouldBeSameAs( tRNR.Nullable );
                tRR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );
            }
            return tRR;
        }

        ICollectionPocoType IList_ObjectN( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRNR )
        {
            var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
            var tINR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListNR ) ).Type;
            Debug.Assert( tINR != null );
            tINR.IsNullable.ShouldBeFalse();
            tINR.Nullable.IsOblivious.ShouldBeTrue();
            tINR.CSharpName.ShouldBe( "IList<object?>" );
            tINR.ImplTypeName.ShouldBe( "List<object?>" );
            tINR.IsStructuralFinalType.ShouldBeFalse();
            tINR.Nullable.IsStructuralFinalType.ShouldBeFalse();
            if( !revert )
            {
                tINR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );
            }
            return tINR;
        }

        ICollectionPocoType IList_Object( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tINR, ICollectionPocoType tRNR )
        {
            var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
            var tIR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListR ) ).Type;
            Debug.Assert( tIR != null );
            tIR.IsOblivious.ShouldBeFalse();
            tIR.CSharpName.ShouldBe( "IList<object>" );
            tIR.ImplTypeName.ShouldBe( "List<object?>" );
            tIR.IsStructuralFinalType.ShouldBeFalse();
            if( !revert )
            {
                tIR.ObliviousType.ShouldBeSameAs( tINR.Nullable );
                tIR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );
            }
            return tIR;
        }

        ICollectionPocoType List_SecPocoN( bool revert, IPocoTypeSystemBuilder ts, string n )
        {
            var tSPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListSecPNR ) )! );
            Debug.Assert( tSPRNR != null );
            tSPRNR.IsOblivious.ShouldBeFalse();
            tSPRNR.CSharpName.ShouldBe( $"List<{n}?>" );
            tSPRNR.ImplTypeName.ShouldBe( $"List<{n}?>" );
            return tSPRNR;
        }

        ICollectionPocoType List_SecPoco( IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tSPRNR )
        {
            var tSPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListSecPR ) )! );
            Debug.Assert( tSPRR != null );
            tSPRR.IsOblivious.ShouldBeFalse();
            tSPRR.CSharpName.ShouldBe( $"List<{n}>" );
            tSPRR.ImplTypeName.ShouldBe( $"List<{n}?>" );
            if( !revert )
            {
                tSPRR.ObliviousType.ShouldBeSameAs( tSPRNR.Nullable );
            }
            return tSPRR;
        }


        ICollectionPocoType List_PocoN( bool revert, IPocoTypeSystemBuilder ts, string n )
        {
            var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPNR ) )! );
            Debug.Assert( tPRNR != null );
            tPRNR.IsOblivious.ShouldBeFalse();
            tPRNR.CSharpName.ShouldBe( $"List<{n}?>" );
            tPRNR.ImplTypeName.ShouldBe( $"List<{n}?>" );
            return tPRNR;
        }

        ICollectionPocoType List_Poco( IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tPRNR )
        {
            var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPR ) )! );
            Debug.Assert( tPRR != null );
            tPRR.IsOblivious.ShouldBeFalse();
            tPRR.CSharpName.ShouldBe( $"List<{n}>" );
            tPRR.ImplTypeName.ShouldBe( $"List<{n}?>" );
            if( !revert )
            {
                tPRR.ObliviousType.ShouldBeSameAs( tPRNR.Nullable );
            }
            return tPRR;
        }

        ICollectionPocoType IList_PocoN( bool revert, IPocoTypeSystemBuilder ts, string n )
        {
            var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
            var tPINR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListPNR ) ).Type;
            Debug.Assert( tPINR != null );
            tPINR.IsOblivious.ShouldBeFalse();
            tPINR.CSharpName.ShouldBe( $"IList<{n}?>" );
            tPINR.ImplTypeName.ShouldMatch( @"CK\.GRSupport\.PocoList_.*_CK" );
            return tPINR;
        }

        ICollectionPocoType IList_Poco( bool revert, IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tPINR )
        {
            var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
            var tPIR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListPR ) ).Type;
            Debug.Assert( tPIR != null );
            tPIR.Nullable.IsOblivious.ShouldBeFalse();
            tPIR.CSharpName.ShouldBe( $"IList<{n}>" );
            tPIR.Nullable.IsStructuralFinalType.ShouldBeFalse();
            if( !revert )
            {
                tPIR.ImplTypeName.ShouldBe( tPINR.ImplTypeName );
                tPIR.ObliviousType.ShouldBeSameAs( tPINR.Nullable );
                tPIR.StructuralFinalType.ShouldBeSameAs( tPINR.Nullable );
            }
            return tPIR;
        }

    }

    public Dictionary<string, int> DicV = null!; // Oblivious
    public Dictionary<string, int?> DicNV = null!; // Oblivious

    public Dictionary<int, object> DicR = null!; // Oblivious
    public Dictionary<int, object?> DicNR = null!;

    public Dictionary<int, IVerySimplePoco> DicPR = null!; // Oblivious
    public Dictionary<int, IVerySimplePoco?> DicPNR = null!;


    public interface IProperDictionaryDefinition : IPoco
    {
        IDictionary<string, int?> IDicNV { get; }
        IDictionary<string, int> IDicV { get; }
        IDictionary<int, object?> IDicNR { get; }
        IDictionary<int, object> IDicR { get; }
        IDictionary<int, IVerySimplePoco?> IDicPNR { get; }
        IDictionary<int, IVerySimplePoco> IDicPR { get; }
    }

    [Test]
    public void Oblivious_and_Final_Dictionary()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IVerySimplePoco ), typeof( IProperDictionaryDefinition )] );
        var ts = r.PocoTypeSystemBuilder;

        // Dictionary of Value type for the value (int)

        // Dictionary<string,int> (oblivious, final).
        var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicV ) )! );
        Debug.Assert( tRV != null );
        tRV.IsNullable.ShouldBeFalse();
        tRV.IsOblivious.ShouldBeFalse();
        tRV.Nullable.IsOblivious.ShouldBeTrue();
        tRV.CSharpName.ShouldBe( "Dictionary<string,int>" );
        tRV.ImplTypeName.ShouldBe( "Dictionary<string,int>" );
        tRV.IsStructuralFinalType.ShouldBeFalse();
        tRV.Nullable.IsStructuralFinalType.ShouldBeTrue();

        // Dictionary<string,int?> (oblivious, final).
        var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNV ) )! );
        Debug.Assert( tRNV != null );
        tRNV.IsNullable.ShouldBeFalse();
        tRNV.Nullable.IsOblivious.ShouldBeTrue();
        tRNV.CSharpName.ShouldBe( "Dictionary<string,int?>" );
        tRNV.ImplTypeName.ShouldBe( "Dictionary<string,int?>" );
        tRNV.Nullable.IsStructuralFinalType.ShouldBeTrue();

        var defPoco = ts.FindByType<IPrimaryPocoType>( typeof( IProperDictionaryDefinition ) );
        Throw.DebugAssert( defPoco != null );

        // IDictionary<string,int> (oblivious, final)
        var tIV = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == "IDicV" ).Type;
        Debug.Assert( tIV != null );
        tIV.IsNullable.ShouldBeFalse();
        tIV.Nullable.IsOblivious.ShouldBeTrue();
        tIV.CSharpName.ShouldBe( "IDictionary<string,int>" );
        tIV.ImplTypeName.ShouldBe( "CovariantHelpers.CovNotNullValueDictionary<string,int>" );
        tIV.Nullable.IsStructuralFinalType.ShouldBeTrue();

        // IDictionary<string,int?> (oblivious, final)
        var tINV = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == "IDicNV" ).Type;
        Debug.Assert( tINV != null );
        tINV.IsNullable.ShouldBeFalse();
        tINV.Nullable.IsOblivious.ShouldBeTrue();
        tINV.CSharpName.ShouldBe( "IDictionary<string,int?>" );
        tINV.ImplTypeName.ShouldBe( "CovariantHelpers.CovNullableValueDictionary<string,int>" );
        tINV.Nullable.IsStructuralFinalType.ShouldBeTrue();

        ////// Dictionary of reference type (object) for the value.

        // Dictionary<int,object?> (oblivious, final)
        var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNR ) )! );
        Debug.Assert( tRNR != null );
        tRNR.IsNullable.ShouldBeFalse();
        tRNR.Nullable.IsOblivious.ShouldBeTrue();
        tRNR.CSharpName.ShouldBe( "Dictionary<int,object?>" );
        tRNR.ImplTypeName.ShouldBe( "Dictionary<int,object?>" );
        tRNR.Nullable.IsStructuralFinalType.ShouldBeTrue();

        // Dictionary<int,object> (non oblivious, non final).
        var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicR ) )! );
        Debug.Assert( tRR != null );
        tRR.IsNullable.ShouldBeFalse();
        tRR.Nullable.IsOblivious.ShouldBeFalse();
        tRR.CSharpName.ShouldBe( "Dictionary<int,object>" );
        tRR.ImplTypeName.ShouldBe( "Dictionary<int,object?>" );
        tRR.Nullable.IsStructuralFinalType.ShouldBeFalse();
        tRR.ObliviousType.ShouldBeSameAs( tRNR.Nullable );
        tRR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );

        // IDictionary<int,object?> (oblivious, non final)
        var tINR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicNR ) ).Type;
        Debug.Assert( tINR != null );
        tINR.IsNullable.ShouldBeFalse();
        tINR.Nullable.IsOblivious.ShouldBeTrue();
        tINR.CSharpName.ShouldBe( "IDictionary<int,object?>" );
        tINR.ImplTypeName.ShouldBe( "Dictionary<int,object?>" );
        tINR.Nullable.IsStructuralFinalType.ShouldBeFalse();
        tINR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );

        // IDictionary<int,object> (non oblivious, non final)
        var tIR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicR ) ).Type;
        Debug.Assert( tIR != null );
        tIR.IsNullable.ShouldBeFalse();
        tIR.NonNullable.IsOblivious.ShouldBeFalse();
        tIR.CSharpName.ShouldBe( "IDictionary<int,object>" );
        tIR.ImplTypeName.ShouldBe( "Dictionary<int,object?>" );
        tIR.NonNullable.IsStructuralFinalType.ShouldBeFalse();
        tIR.ObliviousType.ShouldBeSameAs( tINR.Nullable );
        tIR.StructuralFinalType.ShouldBeSameAs( tRNR.Nullable );

        // Dictionary of IPoco type (IVerySimplePoco) for the value.
        var n = typeof( IVerySimplePoco ).ToCSharpName();

        // Dictionary<int,IVerySimplePoco?> (oblivious, final)
        var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPNR ) )! );
        Debug.Assert( tPRNR != null );
        tPRNR.IsNullable.ShouldBeFalse();
        tPRNR.Nullable.IsOblivious.ShouldBeTrue();
        tPRNR.CSharpName.ShouldBe( $"Dictionary<int,{n}?>" );
        tPRNR.ImplTypeName.ShouldBe( $"Dictionary<int,{n}?>" );
        tPRNR.Nullable.IsStructuralFinalType.ShouldBeTrue();

        // Dictionary<int,IVerySimplePoco> (non oblivious, non final)
        var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPR ) )! );
        Debug.Assert( tPRR != null );
        tPRR.Nullable.IsOblivious.ShouldBeFalse();
        tPRR.CSharpName.ShouldBe( $"Dictionary<int,{n}>" );
        tPRR.ImplTypeName.ShouldBe( $"Dictionary<int,{n}?>" );
        tPRR.Nullable.IsStructuralFinalType.ShouldBeFalse();
        tPRNR.ObliviousType.ShouldBeSameAs( tPRNR.Nullable );
        tPRNR.StructuralFinalType.ShouldBeSameAs( tPRNR.Nullable );

        // IDictionary<int,IVerySimplePoco?> (oblivious, final)
        var tPINR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicPNR ) ).Type;
        Debug.Assert( tPINR != null );
        tPINR.Nullable.IsOblivious.ShouldBeTrue();
        tPINR.CSharpName.ShouldBe( $"IDictionary<int,{n}?>" );
        tPINR.ImplTypeName.ShouldMatch( @"CK\.GRSupport\.PocoDictionary_.*_.*_CK" );
        tPINR.Nullable.IsStructuralFinalType.ShouldBeTrue();

        // IDictionary<int,IVerySimplePoco> (non oblivious, non final)
        var tPIR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicPR ) ).Type;
        Debug.Assert( tPIR != null );
        tPIR.Nullable.IsOblivious.ShouldBeFalse();
        tPIR.CSharpName.ShouldBe( $"IDictionary<int,{n}>" );
        tPIR.ImplTypeName.ShouldBe( tPINR.ImplTypeName, "Same implementation as the Dictionary<int,IVerySimplePoco>." );
        tPIR.Nullable.IsStructuralFinalType.ShouldBeFalse();
        tPIR.ObliviousType.ShouldBeSameAs( tPINR.Nullable );
        tPIR.StructuralFinalType.ShouldBeSameAs( tPINR.Nullable );
    }

    // AnonymousSampleO is the common ObliviousType: 
    public (IVerySimplePoco?, List<IVerySimplePoco?>?) AnonymousSampleO = default;
    public (IVerySimplePoco?, List<IVerySimplePoco?>? X) AnonymousSample1 = default;
    public (IVerySimplePoco?, List<IVerySimplePoco>?) AnonymousSample2 = default;
    public (IVerySimplePoco? A, List<IVerySimplePoco?>?) AnonymousSample3 = default;
    public (IVerySimplePoco, List<IVerySimplePoco?>?) AnonymousSample4 = default;

    [TestCase( "ObliviousFirst" )]
    [TestCase( "ObliviousLast" )]
    public void oblivious_anonymous_record( string mode )
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IVerySimplePoco )] );
        var ts = r.PocoTypeSystemBuilder;

        IRecordPocoType? tO = null;
        if( mode == "ObliviousFirst" ) tO = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSampleO ) )! );

        var t1 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample1 ) )! );
        Debug.Assert( t1 != null );
        var t2 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample2 ) )! );
        Debug.Assert( t2 != null );
        var t3 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample3 ) )! );
        Debug.Assert( t3 != null );
        var t4 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample4 ) )! );
        Debug.Assert( t4 != null );

        if( mode == "ObliviousLast" ) tO = (IRecordPocoType?)ts.FindByType( GetType().GetField( nameof( AnonymousSampleO ) )!.FieldType );
        Debug.Assert( tO != null );

        (tO.IsAnonymous && tO.IsOblivious).ShouldBeTrue();
        t1.IsOblivious.ShouldBeFalse();
        t2.IsOblivious.ShouldBeFalse();
        t3.IsOblivious.ShouldBeFalse();
        t4.IsOblivious.ShouldBeFalse();

        tO.ObliviousType.ShouldBeSameAs( tO );
        t1.ObliviousType.ShouldBeSameAs( tO );
        t2.ObliviousType.ShouldBeSameAs( tO );
        t3.ObliviousType.ShouldBeSameAs( tO );
        t4.ObliviousType.ShouldBeSameAs( tO );

        new object[] { tO, t1, t2, t3, t4 }.Distinct().Count().ShouldBe( 5, "Different PocoTypes." );
    }

    public List<List<Dictionary<int, object?>?>?>? ListCollectionO = null!;
    public (
                List<List<Dictionary<int, object?>>> C1,
                List<List<Dictionary<int, object?>?>> C2,
                List<List<Dictionary<int, object?>?>?> C3,
                List<List<Dictionary<int, object>>>? C4,
                List<List<Dictionary<int, object>>?> C5,
                List<List<Dictionary<int, object>?>> C6,
                List<List<Dictionary<int, object?>>> C7
           )
           ListCollections = default!;

    [TestCase( "ObliviousFirst" )]
    [TestCase( "ObliviousLast" )]
    public void oblivious_anonymous_record_and_collections( string mode )
    {
        var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );

        ICollectionPocoType? tO = null;
        if( mode == "ObliviousFirst" ) tO = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollectionO ) )! );

        IRecordPocoType? others = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollections ) )! );
        Throw.DebugAssert( others != null );

        if( mode == "ObliviousLast" ) tO = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollectionO ) )! );

        Throw.DebugAssert( tO != null );

        var culprits = others.Fields.Where( f => f.Type.ObliviousType != tO );
        culprits.ShouldBeEmpty( "All these collections have the same oblivious type." );

        var oA = others.ObliviousType;
        oA.IsOblivious.ShouldBeTrue();
        oA.Fields.Where( f => !f.IsUnnamed ).ShouldBeEmpty( "The oblivious anonymous record has no field name." );
        oA.Fields.Where( f => f.Type != tO ).ShouldBeEmpty( "The oblivious anonymous record has oblivious field types." );
    }

}
