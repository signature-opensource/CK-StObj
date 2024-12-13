using CK.CodeGen;
using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

/// <summary>
/// C#8 introduced Default Implementation Methods (DIM). It MUST be an "AutoImplementationClaim":
/// they don't appear as Poco properties. This perfectly fits the DIM design: they can be called only
/// through the interface, not from the implementing class nor from other interfaces.
///
/// One could be tempted here to support some automatic (intelligent?) support for this like for
/// instance a [SharedImplementation] attributes that will make the DIM visible (and relayed to the DIM
/// implementation) from the other Poco interfaces that have the property name (or a [RelayImplementation]
/// that "imports" the property implementation from another interface).
/// This is not really difficult and de facto implement a multiple inheritance capability...
///
/// However, I'm a bit reluctant to do this since this would transform
/// IPoco from a DTO structure to an Object beast. Such IPoco become far less "exchangeable" with the external
/// world since they would lost their behavior. The funny Paradox here is that this would not be a real issue
/// with "real" Methods that do things: nobody will be surprised to have "lost" these methods in Type Script,
/// but for DIM properties (typically computed values) this will definitely be surprising. In practice, the
/// code would often has to be transfered "on the other side", with the data...
///
/// Choosing here to NOT play the multiple inheritance game is clearly the best choice (at least for me :)).
/// 
/// </summary>
[TestFixture]
public class DefaultImplementationMethodsTests
{
    [CKTypeDefiner]
    public interface IRootDefiner : IPoco
    {
        IList<string> Lines { get; }

        // DIM properties require an [AutoImplementationClaim] attribute.
        [AutoImplementationClaim]
        int LineCount => Lines.Count;
    }

    public interface IActualRoot : IRootDefiner
    {
        IList<string> Rows { get; }

        [AutoImplementationClaim]
        int RowCount
        {
            get => Rows.Count;
            set
            {
                while( Rows.Count > value ) Rows.RemoveAt( Rows.Count - 1 );
            }
        }

        // DIM methods don't require an [AutoImplementationClaim] attribute.
        void Clear()
        {
            Lines.Clear();
            Rows.Clear();
        }
    }

    [Test]
    public async Task Default_Implementation_Methods_are_supported_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IActualRoot ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var fA = d.Find( "CK.StObj.Engine.Tests.Poco.DefaultImplementationMethodsTests.IActualRoot" );
        Debug.Assert( fA != null );
        var magic = (IActualRoot)fA.Create();

        magic.LineCount.Should().Be( 0 );
        magic.Lines.Add( "Crazy" );
        magic.LineCount.Should().Be( 1 );
        magic.Lines.Add( "Isn't it?" );
        magic.LineCount.Should().Be( 2 );

        magic.RowCount.Should().Be( 0 );
        magic.Rows.Add( "Dingue" );
        magic.RowCount.Should().Be( 1 );
        magic.Rows.Add( "N'est-il pas ?" );
        magic.RowCount.Should().Be( 2 );

        magic.Clear();
        magic.Lines.Should().BeEmpty();
        magic.Rows.Should().BeEmpty();
        magic.LineCount.Should().Be( 0 );
        magic.RowCount.Should().Be( 0 );
    }

    public interface IOnActual : IActualRoot
    {
        // ERROR here! Regular property but RowCount is a DIM on IActualRoot.
        new int RowCount { get; set; }
    }

    [Test]
    public void homonym_properties_must_all_be_Default_Implementation_Method_or_not_in_a_Family1()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IActualRoot ), typeof( IOnActual )],
            "has a Default Implementation Method (DIM). To be supported, all 'RowCount' properties must be DIM and use the [AutoImplementationClaim] attribute." );
    }

    public interface IFaultyRoot : IPoco
    {
        // ERROR here! 
        int X => 0;
    }

    [Test]
    public void a_DIM_property_must_use_AutoImplementationClaim_Attribute()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IFaultyRoot )], "is a Default Implemented Method (DIM), it must use the [AutoImplementationClaim] attribute." );
    }

    public interface IEmptyRoot : IPoco { }

    public interface IOther : IEmptyRoot
    {
        [AutoImplementationClaim]
        int ValidDIM => 1 + Random.Shared.Next( 50 );
    }

    public interface IAnother : IEmptyRoot
    {
        // ERROR here! IOther defines ValidDIM as a DIM.
        int ValidDIM { get; }
    }

    [Test]
    public void homonym_properties_must_all_be_Default_Implementation_Method_or_not_in_a_Family2()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IEmptyRoot ), typeof( IOther ), typeof( IAnother )],
            "has a Default Implementation Method (DIM). To be supported, all 'ValidDIM' properties must be DIM and use the [AutoImplementationClaim] attribute." );
    }


    /// <summary>
    /// Very stupid attribute that shows how easy it is to participate in code generation.
    /// Note that in real life, the code generation is implemented in a "Setup dependency" (a Runtime or Engine component)
    /// and the Attribute itself carries only the definition of the code generation: see <see cref="ContextBoundDelegationAttribute"/>
    /// to easily implement this.
    /// </summary>
    class StupidCodeAttribute : Attribute, IAutoImplementorMethod
    {
        public StupidCodeAttribute( string actualCode, bool isLamda = false )
        {
            ActualCode = actualCode;
        }

        public bool IsLambda { get; }

        public string ActualCode { get; }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, MethodInfo m, ICSCodeGenerationContext c, ITypeScope b )
        {
            IFunctionScope mB = b.CreateOverride( m );
            mB.Parent.Should().BeSameAs( b, "The function is ready to be implemented." );

            if( IsLambda ) mB.Append( "=> " ).Append( ActualCode ).Append( ';' ).NewLine();
            else mB.Append( ActualCode );

            return CSCodeGenerationResult.Success;
        }
    }

    public int DoSomethingResult;

    public interface IPocoWithAbstractAndDefaultImplementationMethods : IPoco
    {
        int One { get; set; }

        // Regular (abstract methods).
        [StupidCode( "t.DoSomethingResult = i + s.Length;", isLamda: true )]
        void DoSomething( DefaultImplementationMethodsTests t, int i, string s );

        // DIM
        void Something( DefaultImplementationMethodsTests t, string s ) => DoSomething( t, 3712, s );

        // Regular (abstract methods).
        [StupidCode( "return s != null ? i + s.Length : null;" )]
        int? DoCompute( int i, string? s );

        // DIM
        int Compute( string? s ) => DoCompute( 3712, s ) ?? -1;
    }

    [Test]
    [Ignore( "Not ready yet." )]
    public async Task poco_can_have_Abstract_and_DefaultImplementationMethods_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( PocoDirectory ), typeof( IPocoWithAbstractAndDefaultImplementationMethods ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var poco = auto.Services.GetRequiredService<PocoDirectory>();

        var o = poco.Create<IPocoWithAbstractAndDefaultImplementationMethods>();

        DoSomethingResult = 0;
        o.Something( this, "12345" );
        DoSomethingResult.Should().Be( 3712 + 5 );

        o.Compute( null ).Should().Be( -1 );
        o.Compute( "123" ).Should().Be( 3712 + 3 );
    }

}
