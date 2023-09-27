using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// Options for <see cref="SampleCommandProcessorWithOptions"/>
    /// </summary>
    public sealed class SomeCommandProcessingOptions
    {
        public SomeCommandProcessingOptions()
        {
            ActivityMonitor.StaticLogger.Info( "SomeCommandProcessingOptions constructor." );
        }

        public int Power { get; set; }
    }
}
