using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Gets a local service container, scoped to this path. This local container is backed by
        /// the <see cref="ICodeGenerationContext.GlobalServiceContainer"/> (see <see cref="SimpleServiceContainer.BaseProvider"/>).
        /// <see cref="IAutoImplementorType.Implement"/> typically registers services inside this container so that
        /// deferred implementators (<see cref="AutoImplementationResult.ImplementorType"/>) can depend on them.
        /// </summary>
        ISimpleServiceContainer ServiceContainer { get; }

        /// <summary>
        /// Gets the name (or comma seprated names) of the <see cref="BinPathConfigurations"/>.
        /// </summary>
        public string Names => BinPathConfigurations.Select( c => String.IsNullOrEmpty( c.Name ) ? "(Unified)" : c.Name ).Concatenate();
    }
}
