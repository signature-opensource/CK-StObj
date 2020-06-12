using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Captures the result of a BinPath analysis: multiple equivalent (compatible) <see cref="BinPathConfiguration"/>
    /// are bound to the same <see cref="EngineMap"/>.
    /// </summary>
    public interface IGeneratedBinPath
    {
        /// <summary>
        /// Gets the engine map that concentrates all the Real Objects, Poco and AutoServices information.
        /// </summary>
        IStObjEngineMap EngineMap { get; }

        /// <summary>
        /// Gets one or more <see cref="BinPathConfiguration"/> that share/are compatible with this <see cref="EngineMap"/>.
        /// </summary>
        IReadOnlyCollection<BinPathConfiguration> BinPathConfigurations { get; }
    }
}
