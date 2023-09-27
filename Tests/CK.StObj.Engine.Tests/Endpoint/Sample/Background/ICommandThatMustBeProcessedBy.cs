using System;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// A command that knows the processor that must be used.
    /// </summary>
    public interface ICommandThatMustBeProcessedBy
    {
        /// <summary>
        /// Acts as the CrisPocoModel: the command handler is known.
        /// </summary>
        Type GetCommandProcessorType();
    }

    public class CommandThatMustBeProcessedBy<T> : ICommandThatMustBeProcessedBy where T : ISampleCommandProcessor
    {
        public Type GetCommandProcessorType() => typeof( T );
    }

}
