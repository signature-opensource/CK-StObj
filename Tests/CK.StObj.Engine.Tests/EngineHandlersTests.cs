using CK.Core;
using CK.Setup;
using CK.Testing;
using CK.TypeCache;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests;

[AttributeUsage( AttributeTargets.Class )]
public sealed class TraceHandlerAttribute : EngineAttribute
{
    public TraceHandlerAttribute( string name )
        : base( "CK.StObj.Engine.Tests.TraceHandlerAttributeImpl, CK.StObj.Engine.Tests" )
    {
        Name = name;
    }

    public string Name { get; }
}

/// <summary>
/// Records its calls. When its name is "Fail", its App method logs an error.
/// </summary>
public sealed class TraceHandlerAttributeImpl : EngineAttributeImpl<ICachedType, TraceHandlerAttribute>
{
    public static readonly List<string> Calls = new();

    void Trace( string slot, AppGenContext? app = null )
    {
        lock( Calls ) Calls.Add( app == null ? $"{Attribute.Name} {slot}" : $"{Attribute.Name} {slot} {app.Group.GroupName}" );
    }

    public void OnStarting( Starting<GenContext> gen ) => Trace( "Starting<Gen>" );

    public void OnGen( IActivityMonitor monitor, GenContext gen ) => Trace( "Gen" );

    public void OnStartingApp( Starting<AppGenContext> app ) => Trace( "Starting<App>", app.Value );

    public void OnApp( IActivityMonitor monitor, GenContext gen, AppGenContext app )
    {
        gen.Apps.ShouldContain( app );
        Trace( "App", app );
        if( Attribute.Name == "Fail" ) monitor.Error( "Failing handler." );
    }

    public void OnEndingApp( Ending<AppGenContext> app ) => Trace( "Ending<App>", app.Value );

    public void OnErrorApp( OnError<AppGenContext> app ) => Trace( "OnError<App>", app.Value );

    public void OnEnding( Ending<GenContext> gen ) => Trace( "Ending<Gen>" );

    public void OnError( OnError<GenContext> gen ) => Trace( "OnError<Gen>" );
}

[AttributeUsage( AttributeTargets.Class )]
public sealed class BadHandlerAttribute : EngineAttribute
{
    public BadHandlerAttribute()
        : base( "CK.StObj.Engine.Tests.BadHandlerAttributeImpl, CK.StObj.Engine.Tests" )
    {
    }
}

public sealed class BadHandlerAttributeImpl : EngineAttributeImpl<ICachedType, BadHandlerAttribute>
{
    public bool Bad( GenContext gen ) => true;
}

[TestFixture]
public class EngineHandlersTests
{
    [TraceHandler( "A" )]
    public sealed class A : IRealObject { }

    [TraceHandler( "B" )]
    public sealed class B : IRealObject { }

    [TraceHandler( "Fail" )]
    public sealed class Failing : IRealObject { }

    [BadHandler]
    public sealed class WithBadHandler : IRealObject { }

    [SetUp]
    public void ClearCalls() => TraceHandlerAttributeImpl.Calls.Clear();

    [Test]
    public async Task the_slots_are_called_in_order_and_the_handlers_in_the_order_of_their_types_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration( generateSourceFiles: false, CompileOption.None );
        configuration.ForceRun = true;
        configuration.FirstBinPath.Types.Add( typeof( B ), typeof( A ) );
        await configuration.RunSuccessfullyAsync();
        TraceHandlerAttributeImpl.Calls.ShouldBe( [
            "A Starting<Gen>", "B Starting<Gen>",
            "A Gen", "B Gen",
            "A Starting<App> First", "B Starting<App> First",
            "A App First", "B App First",
            "A Ending<App> First", "B Ending<App> First",
            "A Ending<Gen>", "B Ending<Gen>" ] );
    }

    [Test]
    public async Task on_error_the_OnError_methods_are_called_instead_of_the_Ending_ones_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration( generateSourceFiles: false, CompileOption.None );
        configuration.ForceRun = true;
        configuration.FirstBinPath.Types.Add( typeof( A ), typeof( Failing ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            (await configuration.RunAsync()).Status.ShouldBe( RunStatus.Failed );
            logs.ShouldContain( "Failing handler." );
        }
        TraceHandlerAttributeImpl.Calls.ShouldBe( [
            "A Starting<Gen>", "Fail Starting<Gen>",
            "A Gen", "Fail Gen",
            "A Starting<App> First", "Fail Starting<App> First",
            "A App First", "Fail App First",
            "A OnError<App> First", "Fail OnError<App> First",
            "A OnError<Gen>", "Fail OnError<Gen>" ] );
    }

    [Test]
    public async Task an_invalid_handler_method_is_an_error_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration( generateSourceFiles: false, CompileOption.None );
        configuration.ForceRun = true;
        configuration.FirstBinPath.Types.Add( typeof( WithBadHandler ) );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            (await configuration.RunAsync()).Status.ShouldBe( RunStatus.Failed );
            logs.ShouldContain( l => l.StartsWith( "Invalid engine handler method 'CK.StObj.Engine.Tests.BadHandlerAttributeImpl.Bad': it must return void" ) );
        }
    }

    [Test]
    public async Task the_App_methods_of_a_handler_are_called_only_for_the_Apps_that_contain_its_type_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration( generateSourceFiles: false, CompileOption.None );
        configuration.ForceRun = true;
        configuration.FirstBinPath.Types.Add( typeof( A ) );
        var second = new BinPathConfiguration
        {
            Name = "Second",
            Path = configuration.FirstBinPath.Path,
            ProjectPath = configuration.FirstBinPath.ProjectPath,
            CompileOption = CompileOption.None,
            GenerateSourceFiles = false
        };
        second.Types.Add( typeof( B ) );
        configuration.AddBinPath( second );
        await configuration.RunSuccessfullyAsync();
        var appCalls = TraceHandlerAttributeImpl.Calls.Where( c => c.Contains( " App " ) ).ToList();
        appCalls.ShouldContain( "A App First" );
        appCalls.ShouldContain( "B App Second" );
        appCalls.ShouldNotContain( "A App Second" );
        appCalls.ShouldNotContain( "B App First" );
        // The Gen methods are called once.
        TraceHandlerAttributeImpl.Calls.Count( c => c == "A Gen" ).ShouldBe( 1 );
    }
}
