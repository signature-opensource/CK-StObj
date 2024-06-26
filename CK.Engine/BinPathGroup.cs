using CK.Core;
using CK.Core.Impl;
using CK.Engine.TypeCollector;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CK.Setup
{
    public sealed class BinPathGroup
    {
        readonly BinPathTypeGroup _typeGroup;
        // Temporary.
        internal IRunningBinPathGroup? _runningGroup;

        internal BinPathGroup( BinPathTypeGroup typeGroup )
        {
            _typeGroup = typeGroup;
        }

        public RunStatus Status => _typeGroup.Success && _runningGroup != null ? RunStatus.Succeed : RunStatus.Failed;

        public bool IsUnifiedPure => _typeGroup.IsUnifiedPure;

        public BinPathTypeGroup TypeGroup => _typeGroup;

        public IPocoTypeSystemBuilder PocoTypeSystemBuilder
        {
            get
            {
                Throw.CheckState( "Empty object pattern not implementedt yet.", _runningGroup?.PocoTypeSystemBuilder != null );
                return _runningGroup.PocoTypeSystemBuilder;
            }
        }

        public IStObjEngineMap EngineMap
        {
            get
            {
                Throw.CheckState( "Empty object pattern not implementedt yet.", _runningGroup?.EngineMap != null );
                return _runningGroup.EngineMap;
            }
        }

        public IStObjMap LoadStObjMap( IActivityMonitor monitor )
        {
            Throw.CheckState( "Empty object pattern not implementedt yet.", _runningGroup != null );
            return _runningGroup.LoadStObjMap( monitor );
        }

        public IStObjMap? TryLoadStObjMap( IActivityMonitor monitor )
        {
            Throw.CheckState( "Empty object pattern not implementedt yet.", _runningGroup != null );
            return _runningGroup.TryLoadStObjMap( monitor );
        }
    }

}
