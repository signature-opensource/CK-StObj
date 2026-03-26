using CK.Core;
using CK.Setup;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

[Explicit]
[TestFixture]
public class HostAndItemPatternTests
{
    [IsMultiple]
    public interface ISomeConcept
    {
        string Name { get; }
        bool Initialized { get; }
    }

    [CKTypeDefiner]
    //
    // [RealObject( RequiredBy = [typeof( ConceptHost )] )]
    //
    // Unfortunately ignored: a CKTypeDefiner is (too much) skipped!
    //
    // This should be honored and doesn't prevent the [RequiredBy] to also be on the
    // concrete classes:
    // - On the "Abstract" level, it tells the engine to call SomeConceptBase.OnHostStart, then Host.OnHostStart and
    //   ConceptA MAY be called before or after Host.OnHostStart 
    // - Putting the [RequiredBy] also an the concrete guaranties that Host will precede the concrete (and that is
    //   another, stronger, constraint).
    //
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

    // This works but is stronger than what we really need.
    [RealObject( RequiredBy = [typeof( ConceptHost )] )]
    public sealed class ConceptA : SomeConceptBase
    {
        public ConceptA()
            : base( "A" )
        {
        }
    }

    public sealed class ConceptB : SomeConceptBase
    {
        public ConceptB()
            : base( "B" )
        {
        }
    }

    public sealed class ConceptHost : IRealObject
    {
        ImmutableArray<ISomeConcept> _concepts;

        void StObjInitialize( IActivityMonitor monitor, IStObjObjectMap engineMap )
        {
            _concepts = [..engineMap.FinalImplementations.Select( i => i.Implementation ).OfType<ISomeConcept>()];
        }

        void OnHostStart( IActivityMonitor monitor )
        {
            _concepts.All( c => c.Initialized );
            monitor.Info( $"Host start." );
        }
    }

    [Test]
    public async Task with_RealObject_RequiredBy_attribute_and_StObjInitialize_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();

        // With standard ordering, the RequiredBy attribute may not exist.
        configuration.RevertOrderingNames = true;

        configuration.FirstBinPath.Types.Add( typeof( ConceptA ), typeof( ConceptHost ) );
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
