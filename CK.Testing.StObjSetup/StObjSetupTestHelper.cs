using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjSetup;
using CKSetup;

namespace CK.Testing
{
    /// <summary>
    /// Exposes standard implementation of <see cref="IStObjSetupTestHelperCore"/>.
    /// </summary>
    [ResolveTarget(typeof(IStObjSetupTestHelper))]
    public class StObjSetupTestHelper : IStObjSetupTestHelperCore, ITestHelperResolvedCallback
    {
        readonly ICKSetupTestHelper _ckSetup;
        readonly IStObjMapTestHelper _stObjMap;
        IStObjSetupTestHelper? _mixin;
        EventHandler<StObjSetupRunningEventArgs>? _stObjSetupRunning;
        bool _generateSourceFiles;
        bool _revertOrderingNames;
        bool _traceGraphOrdering;

        internal StObjSetupTestHelper( TestHelperConfiguration config, ICKSetupTestHelper ckSetup, IStObjMapTestHelper stObjMap )
        {
            _ckSetup = ckSetup;
            _stObjMap = stObjMap;
            stObjMap.StObjMapLoading += OnStObjMapLoading;

            _generateSourceFiles = config.DeclareBoolean( "StObjSetup/StObjGenerateSourceFiles",
                                                          true,
                                                          "Whether the '$StObjGen/G0.cs' source file must be generated in the ProjectPath folder.",
                                                          () => _generateSourceFiles.ToString() ).Value;
            _revertOrderingNames = config.DeclareBoolean( "StObjSetup/StObjRevertOrderingNames",
                                                          false,
                                                          "Whether the ordering of StObj that share the same rank in the dependency graph must be inverted. This configuration can be reused by Aspects that also use topology sort instead of introducing another similar option.",
                                                          () => _revertOrderingNames.ToString() ).Value;
            _traceGraphOrdering = config.DeclareBoolean( "StObjSetup/StObjTraceGraphOrdering",
                                                         false,
                                                         "Whether the dependency graph (the set of IDependentItem) associated to the StObj objects must be send to the monitor before and after sorting. This configuration can be reused by aspects that also use topology sort instead of introducing another similar option.",
                                                         () => _traceGraphOrdering.ToString() ).Value;
        }

        void OnStObjMapLoading( object? sender, EventArgs e )
        {
            Debug.Assert( _mixin != null, "It has been initialized by ITestHelperResolvedCallback.OnTestHelperGraphResolved." );
            var (configuration, forceSetup) = CreateDefaultConfiguration( _mixin.Monitor, _mixin! );
            DoRunStObjSetup( configuration, forceSetup );
        }

        /// <summary>
        /// Low level helper that initializes a new <see cref="StObjEngineConfiguration"/> and computes the force setup flag
        /// that can be used by other helpers that need to run a setup.
        /// </summary>
        /// <param name="helper">The <see cref="IStObjSetupTestHelper"/> helper.</param>
        /// <returns>The configuration and the flag.</returns>
        static public (StObjEngineConfiguration Configuration, ForceSetupLevel ForceSetup) CreateDefaultConfiguration( IActivityMonitor monitor,
                                                                                                                       IStObjSetupTestHelper helper )
        {
            var stObjConf = new StObjEngineConfiguration
            {
                RevertOrderingNames = helper.StObjRevertOrderingNames,
                TraceDependencySorterInput = helper.StObjTraceGraphOrdering,
                TraceDependencySorterOutput = helper.StObjTraceGraphOrdering,
            };
            var b = new BinPathConfiguration
            {
                // The name of the BinPath to use is the current IStObjMapTestHelper.BinPathName.
                Name = helper.BinPathName,
                // Use the ClosestSUTProjectFolder for the BinPath. If it's not found, it's
                // the test BinFolder.
                Path = helper.ClosestSUTProjectFolder.Combine( helper.PathToBin ),
                // Then the OutputPath will copy the generated assembly to this bin folder.
                OutputPath = helper.BinFolder,
                CompileOption = CompileOption.Compile,
                // ...and the G0.cs to the TestProjectFolder.
                GenerateSourceFiles = helper.StObjGenerateSourceFiles,
                ProjectPath = helper.TestProjectFolder
            };
            stObjConf.BinPaths.Add( b );

            // Consider by default the CKSetup configuration that be not None,
            // but if it is None, set it to Engine: the engine must run even if
            // all the binaries are unchanged to check the G0.cs and assembly.
           
            var f = helper.CKSetup.ForceSetup;
            if( f == ForceSetupLevel.None )
            {
                monitor.Trace( $"Setting CKSetup ForceSetupLevel to Engine so it can check the required artifacts." );
                f = ForceSetupLevel.Engine;
            }
            return (stObjConf, f);
        }

        CKSetupRunResult DoRunStObjSetup( StObjEngineConfiguration stObjConf, ForceSetupLevel forceSetup )
        {
            Throw.CheckNotNullArgument( stObjConf );
            using( _ckSetup.Monitor.OpenInfo( $"Invoking StObjSetupRunning event." ) )
            {
                try
                {
                    var ev = new StObjSetupRunningEventArgs( stObjConf, forceSetup );
                    _stObjSetupRunning?.Invoke( this, ev );
                    var ckSetupConf = new SetupConfiguration( new XDocument( ev.StObjEngineConfiguration.ToXml() ), "CK.Setup.StObjEngine, CK.StObj.Engine" );
                    ckSetupConf.CKSetupName = _ckSetup.TestProjectName;
                    return _ckSetup.CKSetup.Run( ckSetupConf, forceSetup: ev.ForceSetup );
                }
                catch( Exception ex )
                {
                    _ckSetup.Monitor.Error( ex );
                    throw;
                }
            }
        }

        bool IStObjSetupTestHelperCore.StObjGenerateSourceFiles { get => _generateSourceFiles; set => _generateSourceFiles = value; }

        bool IStObjSetupTestHelperCore.StObjRevertOrderingNames { get => _revertOrderingNames; set => _revertOrderingNames = value; }

        bool IStObjSetupTestHelperCore.StObjTraceGraphOrdering { get => _traceGraphOrdering; set => _traceGraphOrdering = value; }

        event EventHandler<StObjSetupRunningEventArgs> IStObjSetupTestHelperCore.StObjSetupRunning
        {
            add => _stObjSetupRunning += value;
            remove => _stObjSetupRunning -= value;
        }

        CKSetupRunResult IStObjSetupTestHelperCore.RunStObjSetup( StObjEngineConfiguration configuration, ForceSetupLevel forceSetup ) => DoRunStObjSetup( configuration, forceSetup );

        void ITestHelperResolvedCallback.OnTestHelperGraphResolved( object resolvedObject )
        {
            _mixin = (IStObjSetupTestHelper)resolvedObject;
        }

        /// <summary>
        /// Gets the <see cref="IStObjSetupTestHelper"/> default implementation.
        /// </summary>
        public static IStObjSetupTestHelper TestHelper => TestHelperResolver.Default.Resolve<IStObjSetupTestHelper>();

    }
}
