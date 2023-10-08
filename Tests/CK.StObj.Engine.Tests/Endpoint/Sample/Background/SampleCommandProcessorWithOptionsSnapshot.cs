using CK.Core;
using Microsoft.Extensions.Options;
using System.Threading;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// Simulates a simple command handler that has IOptionsSnapshot.
    /// </summary>
    public sealed class SampleCommandProcessorWithOptionsSnapshot : ISampleCommandProcessor, IAutoService
    {
        readonly IOptionsSnapshot<SomeCommandProcessingOptions> _options;
        readonly SampleCommandMemory _commandHistory;

        public SampleCommandProcessorWithOptionsSnapshot( IOptionsSnapshot<SomeCommandProcessingOptions> options, SampleCommandMemory commandHistory )
        {
            _options = options;
            _commandHistory = commandHistory;
        }

        public void Process( object command )
        {
            _commandHistory.Trace( $"{command.GetType().ToCSharpName( withNamespace: false )} - {_options.Value.Power}" );
        }
    }

    /// <summary>
    /// Simulates a simple command handler that has IOptionsMonitor.
    /// </summary>
    public sealed class SampleCommandProcessorWithOptionsMonitor : ISampleCommandProcessor, IAutoService
    {
        readonly IOptionsMonitor<SomeCommandProcessingOptions> _options;
        readonly SampleCommandMemory _commandHistory;

        public SampleCommandProcessorWithOptionsMonitor( IOptionsMonitor<SomeCommandProcessingOptions> options, SampleCommandMemory commandHistory )
        {
            _options = options;
            _commandHistory = commandHistory;
        }

        public void Process( object command )
        {
            var initialValue = _options.CurrentValue.Power;
            _commandHistory.Trace( $"{command.GetType().ToCSharpName( withNamespace: false )} - {initialValue}" );
            while( initialValue == _options.CurrentValue.Power ) Thread.Sleep( 200 );
            _commandHistory.Trace( $"{command.GetType().ToCSharpName( withNamespace: false )} - {_options.CurrentValue.Power}" );
        }
    }
}
