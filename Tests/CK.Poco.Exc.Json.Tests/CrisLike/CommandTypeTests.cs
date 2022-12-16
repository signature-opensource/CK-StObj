using CK.Core;
using NUnit.Framework;
using System.Collections.Generic;
using CK.CrisLike;
using static CK.Testing.StObjEngineTestHelper;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using FluentAssertions;
using System.ComponentModel;

namespace CK.Poco.Exc.Json.Tests.CrisLike
{
    [ExternalName( "BatchCommand" )]
    public interface IBatchCommand : ICommand<ICrisResult[]>
    {
        ICommand? First { get; set; }
        IList<ICommand> OtherCommands { get; }
    }

    public record struct Person( string Name, int Power );

    [ExternalName( "PersonCommand" )]
    public interface IPersonCommand : ICommand<bool>
    {
        ref Person Root { get; }
        Person[] Friends { get; set; }
    }

    [ExternalName( "SimpleCommand" )]
    public interface ISimpleCommand : ICommand
    {
        [DefaultValue(42)]
        int Power { get; set; }
    }

    public record struct Account( int AccountId, long Balance, List<Person> Members );

    [ExternalName( "AccountCommand" )]
    public interface IAccountCommand : ICommand
    {
        IDictionary<int,Account> Accounts { get; }
    }

    [TestFixture]
    public class CommandTypeTests
    {
        [Test]
        public void commands_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ),
                                                     typeof( ISimpleCommand ),
                                                     typeof( IPersonCommand ),
                                                     typeof( IAccountCommand ),
                                                     typeof( IBatchCommand ),
                                                     typeof( ICrisResult ),
                                                     typeof( ICrisResultError ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var person = directory.Create<IPersonCommand>( c =>
            {
                c.Root.Name = "Albert";
                c.Friends = new[] { new Person( "Olivier", 1 ), new Person( "John", 2 ) }; 
            } );
            var account = directory.Create<IAccountCommand>( c =>
            {
                c.Accounts.Add( 1, new( 1, 3712, new List<Person>() { new( "P1", 1 ), new( "P2", 2 ), new( "P3", 3 ) } ) );
                c.Accounts.Add( 2, new( 2, 42, new List<Person>() ) );
            } );
            var batch = directory.Create<IBatchCommand>( c =>
            {
                c.First = directory.Create<ISimpleCommand>();
                c.OtherCommands.Add( person );
                c.OtherCommands.Add( account );
            } );

            var result = @"
{
	""First"": [""SimpleCommand"", { ""Power"": 42 }],
	""OtherCommands"": [
		[""PersonCommand"", {
			""Root"": {
				""Name"": ""Albert"",
				""Power"": 0
			},
			""Friends"": [{
				""Name"": ""Olivier"",
				""Power"": 1
			}, {
				""Name"": ""John"",
				""Power"": 2
			}]
		}],
		[""AccountCommand"", {
			""Accounts"": [
				[1, {
					""AccountId"": 1,
					""Balance"": ""3712"",
					""Members"": [{
						""Name"": ""P1"",
						""Power"": 1
					}, {
						""Name"": ""P2"",
						""Power"": 2
					}, {
						""Name"": ""P3"",
						""Power"": 3
					}]
				}],
				[2, {
					""AccountId"": 2,
					""Balance"": ""42"",
					""Members"": []
				}]
			]
		}]
	]
}
";
            var toString = batch.ToString();
            toString.Should().Be ( result.Replace( "\r", "" ).Replace( "\n", "" ).Replace( "\t", "" ).Replace( " ", "" ) );

            var batch2 = JsonTestHelper.Roundtrip( directory, batch );
            Debug.Assert( batch2 != null );
            batch2.Should().BeEquivalentTo( batch );
        }
    }
}
