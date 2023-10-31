using CK.Core;
using Microsoft.Extensions.Options;
using System;

namespace CK.StObj.Engine.Tests.Endpoint
{

    /// <summary>
    /// Simulates a simple command handler that has IOptions.
    /// </summary>
    public sealed class SampleCommandProcessorWithOptions : ISampleCommandProcessor, IAutoService
    {
        readonly IOptions<SomeCommandProcessingOptions> _options;
        readonly SampleCommandMemory _commandHistory;

        public SampleCommandProcessorWithOptions( IOptions<SomeCommandProcessingOptions> options,
                                                  SampleCommandMemory commandHistory )
        {
            _options = options;
            _commandHistory = commandHistory;
        }

        public void Process( object command )
        {
            _commandHistory.Trace( $"{command.GetType().ToCSharpName( withNamespace: false )} - {_options.Value.Power}" );
        }
    }
}
