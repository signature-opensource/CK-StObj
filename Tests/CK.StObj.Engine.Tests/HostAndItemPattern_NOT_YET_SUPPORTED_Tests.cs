using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

[TestFixture]
public class HostAndItemPattern_NOT_YET_SUPPORTED_Tests
{
    [IsMultiple]
    public interface ISomeConcept
    {
        string Name { get; }
        bool Initialized { get; }
    }

    [CKTypeDefiner]
    public abstract class SomeConceptBase : IRealObject, ISomeConcept
    {
        readonly string _name;
        bool _initialized;

        protected SomeConceptBase( string name )
        {
            _name = name;
        }

        public string Name => _name;

        public bool Initialized => _initialized;

        void OnHostStart( IActivityMonitor monitor )
        {
            _initialized = true;
            monitor.Info( $"SomeConceptBase '{_name}' start." );
        }
    }

    public class ConceptA : SomeConceptBase
    {
        public ConceptA()
            : base( "A" )
        {
        }
    }

    public class ConceptB : SomeConceptBase
    {
        public ConceptB()
            : base( "B" )
        {
        }
    }

    public sealed class ConceptHostByMultipleRequires : IRealObject
    {
        ImmutableArray<ISomeConcept> _concepts;

        void StObjConstruct( ImmutableArray<ISomeConcept> concepts )
        {
            _concepts = concepts;
        }

        //void StObjConstruct( IEnumerable<ISomeConcept> concepts )
        //{
        //    _concepts = [.. concepts];
        //}

        void OnHostStart( IActivityMonitor monitor )
        {
            _concepts.All( c => c.Initialized );
            monitor.Info( $"Host start." );
        }
    }

    [Explicit]
    [Test]
    public async Task through_IEnumerable_IsMultiple_interface_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ConceptA ), typeof( ConceptHostByMultipleRequires ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped( sp => TestHelper.Monitor );
            services.AddScoped( sp => TestHelper.Monitor.ParallelLogger );
        } );
        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Info ) )
        {
            var initializers = auto.Services.GetRequiredService<IEnumerable<Microsoft.Extensions.Hosting.IHostedService>>();
            foreach( var i in initializers )
            {
                await i.StartAsync( default );
                await i.StopAsync( default );
            }
            entries.Select( e => e.Text ).Where( t => !t.StartsWith( "Calling" ) ).Concatenate( "|" )
                .ShouldBe( "SomeConceptBase 'A' start.|Host start." );

        }
    }


}
