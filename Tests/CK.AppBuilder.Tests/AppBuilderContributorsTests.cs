using CK.Core;
using CK.Setup;
using NUnit.Framework;
using Shouldly;
using System.Collections.Generic;
using static CK.Testing.MonitorTestHelper;

[assembly: AppBuilderContributor( typeof( CK.AppBuilder.Tests.SecondContributor ) )]
[assembly: AppBuilderContributor( typeof( CK.AppBuilder.Tests.FirstContributor ) )]

namespace CK.AppBuilder.Tests;

public sealed class FirstContributor : IAppBuilderContributor
{
    public static readonly List<string> Calls = new();

    public bool Contribute( IActivityMonitor monitor, AppBuilderContext context )
    {
        Calls.Add( "First.Contribute" );
        return true;
    }

    public bool OnContributed( IActivityMonitor monitor, AppBuilderContext context )
    {
        Calls.Add( "First.OnContributed" );
        return true;
    }
}

// Requires the FirstContributor: a constructor parameter is a requirement.
public sealed class SecondContributor : IAppBuilderContributor
{
    public SecondContributor( FirstContributor first ) => First = first;

    public FirstContributor First { get; }

    public bool Contribute( IActivityMonitor monitor, AppBuilderContext context )
    {
        FirstContributor.Calls.Add( "Second.Contribute" );
        return true;
    }
}

[TestFixture]
public class AppBuilderContributorsTests
{
    [Test]
    public void the_contributors_are_found_and_ordered_by_their_constructor_parameters()
    {
        FirstContributor.Calls.Clear();
        var context = new AppBuilderContext( new EngineConfiguration(), [] );
        AppBuilderContributors.Run( TestHelper.Monitor, context, TestHelper.BinFolder ).ShouldBeTrue();
        // Declared Second first: the requirement orders them.
        FirstContributor.Calls.ShouldBe( ["First.Contribute", "Second.Contribute", "First.OnContributed"] );
    }
}
