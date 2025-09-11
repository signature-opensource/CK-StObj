using CK.Core;
using CK.Engine.TypeCollector;

namespace CK.Setup;

// Not necessarily definitive adapter.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public sealed partial class EngineResult
{
    /// <summary>
    /// Captures the result of a BinPath.
    /// </summary>
    public sealed class BinPath
    {
        readonly EngineResult _owner;
        readonly BinPathConfiguration _configuration;
        readonly BinPathGroup _group;

        internal BinPath( EngineResult owner, BinPathConfiguration c, BinPathGroup group )
        {
            _owner = owner;
            _configuration = c;
            _group = group;
        }

        /// <summary>
        /// Gets whether this BinPath succeed, failed or have been skipped.
        /// </summary>
        public RunStatus Status => _group.Status;

        public EngineResult Owner => _owner;

        public string Name => _configuration.Name;

        public BinPathConfiguration Configuration => _configuration;

        public BinPathTypeGroup TypeGroup => _group.TypeGroup;

        public IPocoTypeSystemBuilder PocoTypeSystemBuilder
        {
            get
            {
                Throw.CheckState( Owner.Status is not RunStatus.Failed );
                return _group.PocoTypeSystemBuilder;
            }
        }

        public IStObjEngineMap EngineMap
        {
            get
            {
                Throw.CheckState( Owner.Status is not RunStatus.Failed );
                return _group.EngineMap;
            }
        }

        public IStObjMap LoadMap( IActivityMonitor monitor )
        {
            Throw.CheckState( Owner.Status is not RunStatus.Failed );
            return _group.LoadStObjMap( monitor );
        }

        public IStObjMap? TryLoadMap( IActivityMonitor monitor )
        {
            Throw.CheckState( Owner.Status is not RunStatus.Failed );
            return _group.TryLoadStObjMap( monitor );
        }

        public BinPathGroup Group => _group;
    }

}
