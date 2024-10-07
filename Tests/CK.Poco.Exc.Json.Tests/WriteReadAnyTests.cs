using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;

[TestFixture]
public partial class WriteReadAnyTests
{

    [ExternalName( "SomeType" )]
    public interface ISomeTypes : IPoco
    {
        int[] Value { get; set; }
        IList<ISomeTypes> Friends { get; }

        public static ISomeTypes CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<ISomeTypes>( v => FillRandom( directory, r, v ) );
        }
    }

    static void FillRandom( PocoDirectory directory, Random r, ISomeTypes v )
    {
        v.Value = Enumerable.Range( 0, r.Next( 10 ) ).Select( i => i + r.Next( 10 ) ).ToArray();
        FillRandom( directory, r, v.Friends );
    }

    static void FillRandom( PocoDirectory directory, Random r, IList<ISomeTypes> friends )
    {
        int nbFriend = r.Next( 2 );
        for( int i = 0; i < nbFriend; i++ )
        {
            friends.Add( ISomeTypes.CreateRandom( directory, r ) );
        }
    }

    [Test]
    public void ReadAnyJson_tests()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( ISomeTypes ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        directory.ReadAnyJson( "null" ).Should().BeNull();
        directory.ReadAnyJson( "3712" ).Should().Be( 3712.0 );
        directory.ReadAnyJson( "true" ).Should().Be( true );
        directory.ReadAnyJson( "false" ).Should().Be( false );
        var def = directory.ReadAnyJson( """["SomeType",{}]""" );
        Throw.DebugAssert( def != null );
        var tDef = (ISomeTypes)def;
        tDef.Value.Should().NotBeNull().And.BeEmpty();
        tDef.Friends.Should().BeEmpty();

        var withFriends = directory.ReadAnyJson( """
                                                 ["SomeType",{
                                                   "Value": [1],
                                                   "Friends": [
                                                     {
                                                     },
                                                     {
                                                        "Value": [1,2]
                                                     },
                                                     {
                                                        "Value": [1,2,3],
                                                        "Friends": [{"Value": [1,2,3,4]},{"Value": [1,2,3,4,5]}]
                                                     }
                                                   ]
                                                 }]
                                                 """ );
        Throw.DebugAssert( withFriends != null );
        var tWithFriends = (ISomeTypes)withFriends;
        tWithFriends.Value.Should().HaveCount( 1 ).And.Contain( 1 );
        tWithFriends.Friends.Should().HaveCount( 3 );
        tWithFriends.Friends[0].Should().BeEquivalentTo( tDef );
        tWithFriends.Friends[1].Value.Should().HaveCount( 2 ).And.Contain( new[] { 1, 2 } );
        tWithFriends.Friends[2].Value.Should().HaveCount( 3 ).And.Contain( new[] { 1, 2, 3 } );
        tWithFriends.Friends[2].Friends[0].Value.Should().HaveCount( 4 ).And.Contain( new[] { 1, 2, 3, 4 } );
        tWithFriends.Friends[2].Friends[1].Value.Should().HaveCount( 5 ).And.Contain( new[] { 1, 2, 3, 4, 5 } );
    }

    [CKTypeDefiner]
    public interface ICommand : IPoco
    {
        public static ICommand CreateRandom( PocoDirectory directory, Random r )
        {
            return r.Next( 5 ) switch
            {
                0 => IBatchCommand.CreateRandom( directory, r ),
                _ => IMissionCommand.CreateRandom( directory, r )
            };
        }
    }

    public interface IBatchCommand : ICommand
    {
        IList<ICommand> Commands { get; }
        IList<ICommand> OtherCommands { get; }

        public static new IBatchCommand CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IBatchCommand>( c =>
            {
                int nbCommand = 1 + r.Next( 3 );
                for( int i = 0; i < nbCommand; i++ )
                {
                    c.Commands.Add( ICommand.CreateRandom( directory, r ) );
                    c.OtherCommands.Add( ICommand.CreateRandom( directory, r ) );
                }
            } );
        }
    }

    [CKTypeDefiner]
    public interface IMission : IPoco
    {
        string MissionId { get; set; }

        public static IMission CreateRandom( PocoDirectory directory, Random r )
        {
            return r.Next( 6 ) switch
            {
                0 => IDispatchMission.CreateRandom( directory, r ),
                1 => IPickingMission.CreateRandom( directory, r ),
                2 => IMultiMission.CreateRandom( directory, r ),
                3 => IMultiMission2.CreateRandom( directory, r ),
                4 => IVerySimpleMission.CreateRandom( directory, r ),
                _ => ISimpleMission.CreateRandom( directory, r )
            };
        }
    }

    [CKTypeDefiner]
    public interface ICanBeRequiredMission : IMission
    {
        bool Required { get; set; }
    }

    public interface IMissionCommand : ICommand
    {
        IMission? Mission { get; set; }

        public static new IMissionCommand CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IMissionCommand>( c => c.Mission = IMission.CreateRandom( directory, r ) );
        }
    }

    public interface IMultiMission : IMission
    {
        IDictionary<string, IMission> Missions { get; }

        public static new IMultiMission CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IMultiMission>( m =>
            {
                m.MissionId = Util.GetRandomBase64UrlString( 14 );
                int nb = r.Next( 4 );
                for( int i = 0; i < nb; i++ )
                {
                    var one = IMission.CreateRandom( directory, r );
                    m.Missions.Add( one.MissionId, IMission.CreateRandom( directory, r ) );
                }
            } );
        }
    }

    public interface IMultiMission2 : IMission
    {
        IDictionary<string, IMission> Missions { get; }

        IList<IVerySimpleMission> VerySimpleMissions { get; }

        public static new IMultiMission2 CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IMultiMission2>( m =>
            {
                m.MissionId = Util.GetRandomBase64UrlString( 14 );
                int nb = 1 + r.Next( 4 );
                for( int i = 0; i < nb; i++ )
                {
                    var one = IMission.CreateRandom( directory, r );
                    m.Missions.Add( one.MissionId, one );
                }
                nb = 1 + r.Next( 4 );
                for( int i = 0; i < nb; i++ )
                {
                    m.VerySimpleMissions.Add( r.Next( 2 ) == 0
                                                ? IVerySimpleMission.CreateRandom( directory, r )
                                                : ISimpleMission.CreateRandom( directory, r ) );
                }
            } );
        }
    }

    public interface IOrder : IPoco
    {
        IList<(string Ref, int Quantity)> Lines { get; }

        public static IOrder CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IOrder>( o => FillRandom( r, o ) );
        }
    }

    static void FillRandom( Random r, IOrder o )
    {
        int nbProduct = 1 + r.Next( 6 );
        for( int i = 0; i < nbProduct; i++ )
        {
            o.Lines.Add( (Util.GetRandomBase64UrlString( 5 ), r.Next( 1000 )) );
        }
    }

    public interface IDestination : IPoco
    {
        [UnionType]
        object Destination { get; set; }

        class UnionTypes
        {
            public (string, int, List<string>) Destination { get; }
        }

        public static IDestination CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create( (Action<IDestination>)(o => FillRandom( r, o )) );
        }
    }

    static void FillRandom( Random r, IDestination o )
    {
        o.Destination = r.Next( 3 ) switch
        {
            0 => r.Next( 1000000 ),
            1 => Util.GetRandomBase64UrlString( 20 ),
            _ => new List<string>() { Util.GetRandomBase64UrlString( 6 ), Util.GetRandomBase64UrlString( 4 ), Util.GetRandomBase64UrlString( 2 ) }
        };
    }

    public interface IDispatchMission : IMission
    {
        IDestination Destination { get; }
        IOrder Order { get; }
        IList<ISomeTypes> SomeTypes { get; }

        public static new IDispatchMission CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IDispatchMission>( m =>
            {
                m.MissionId = Util.GetRandomBase64UrlString( 10 );
                FillRandom( directory, r, m );
            } );
        }
    }

    public interface ITotallySimpleMission : IMission
    {
    }

    public interface IVerySimpleMission : ITotallySimpleMission
    {
        [DefaultValue( "A very simple mission" )]
        string SimpleMissionName { get; set; }

        public static new IVerySimpleMission CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IVerySimpleMission>( m =>
            {
                m.MissionId = Util.GetRandomBase64UrlString( 10 );
            } );
        }
    }

    public interface ISimpleMission : IVerySimpleMission, ICanBeRequiredMission
    {
        IDestination Destination { get; }

        public static new ISimpleMission CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<ISimpleMission>( m =>
            {
                m.MissionId = Util.GetRandomBase64UrlString( 10 );
                m.SimpleMissionName = "A simple mission";
                m.Required = r.Next( 2 ) == 0;
                FillRandom( r, m.Destination );
            } );
        }
    }

    static void FillRandom( PocoDirectory directory, Random r, IDispatchMission m )
    {
        FillRandom( r, m.Destination );
        FillRandom( r, m.Order );
        FillRandom( directory, r, m.SomeTypes );
    }

    public interface IPickingMission : ICanBeRequiredMission
    {
        IList<IDispatchMission> DispatchMissions { get; }

        public static new IPickingMission CreateRandom( PocoDirectory directory, Random r )
        {
            return directory.Create<IPickingMission>( m =>
            {
                m.Required = r.Next( 2 ) == 0;
                m.MissionId = Util.GetRandomBase64UrlString( 10 );
                FillRandom( directory, r, m.DispatchMissions );
            } );
        }
    }

    static void FillRandom( PocoDirectory directory, Random r, IList<IDispatchMission> m )
    {
        int nb = 1 + r.Next( 6 );
        for( int i = 0; i < nb; i++ )
        {
            m.Add( IDispatchMission.CreateRandom( directory, r ) );
        }
    }



    [TestCase( new int[] { 1, 3712, 42, -1, 57 } )]
    public void roundtrip_and_write_read_any_big( int[] seeds )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ),
                                        typeof( ISomeTypes ),
                                        typeof( ICommand ),
                                        typeof( IBatchCommand ),
                                        typeof( IMission ),
                                        typeof( IMissionCommand ),
                                        typeof( ICanBeRequiredMission ),
                                        typeof( IOrder ),
                                        typeof( IDestination ),
                                        typeof( IMultiMission ),
                                        typeof( IMultiMission2 ),
                                        typeof( IDispatchMission ),
                                        typeof( ISimpleMission ),
                                        typeof( IPickingMission ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        foreach( var seed in seeds )
        {
            var r = new Random( seed );
            var b = IBatchCommand.CreateRandom( directory, r );
            TestHelper.Monitor.Info( $"Roundtripping:{Environment.NewLine}{b}" );
            JsonTestHelper.Roundtrip( directory, b );
        }

    }
}
