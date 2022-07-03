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

        internal StObjSetupTestHelper( ITestHelperConfiguration config, ICKSetupTestHelper ckSetup, IStObjMapTestHelper stObjMap )
        {
            _ckSetup = ckSetup;
            _stObjMap = stObjMap;
            stObjMap.StObjMapLoading += OnStObjMapLoading;

            _generateSourceFiles = config.GetBoolean( "StObjSetup/StObjGenerateSourceFiles" ) ?? true;
            _revertOrderingNames = config.GetBoolean( "StObjSetup/StObjRevertOrderingNames" ) ?? false;
            _traceGraphOrdering = config.GetBoolean( "StObjSetup/StObjTraceGraphOrdering" ) ?? false;
        }

        void OnStObjMapLoading( object? sender, EventArgs e )
        {
            Debug.Assert( _mixin != null, "It has been initialized by ITestHelperResolvedCallback.OnTestHelperGraphResolved." );
            var (configuration, forceSetup) = CreateDefaultConfiguration( _mixin! );
            DoRunStObjSetup( configuration, forceSetup );
        }

        /// <summary>
        /// Low level helper that initializes a new <see cref="StObjEngineConfiguration"/> and computes the force setup flag
        /// that can be used by other helpers that need to run a setup.
        /// </summary>
        /// <param name="helper">The <see cref="IStObjSetupTestHelper"/> helper.</param>
        /// <returns>The configuration and the flag.</returns>
        static public (StObjEngineConfiguration Configuration, ForceSetupLevel ForceSetup) CreateDefaultConfiguration( IStObjSetupTestHelper helper )
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
                // Here, we should be able to decorate the tested project reference with an Item Metadata:
                //      <ProjectReference Include="..\..\Component\Component.csproj" StObjSetup="true" />
                // and get the target path here from a const? a static that a generated code set?...
                //
                // This would be better than relying on the project naming conventions...
                //
                Path = helper.BinFolder,
                // Then the OutputPath will copy the generated assembly to this bin folder.
                OutputPath = helper.BinFolder,
                CompileOption = CompileOption.Compile,

                GenerateSourceFiles = helper.StObjGenerateSourceFiles,
                ProjectPath = helper.TestProjectFolder
            };
            stObjConf.BinPaths.Add( b );

            return (stObjConf, helper.CKSetup.DefaultForceSetup);
        }

        CKSetupRunResult DoRunStObjSetup( StObjEngineConfiguration stObjConf, ForceSetupLevel forceSetup )
        {
            Throw.CheckNotNullArgument( stObjConf );
            using( _ckSetup.Monitor.OpenInfo( $"Running StObjSetup." ) )
            {
                try
                {
                    var ev = new StObjSetupRunningEventArgs( stObjConf, forceSetup );
                    _stObjSetupRunning?.Invoke( this, ev );
                    var ckSetupConf = new SetupConfiguration( new XDocument( ev.StObjEngineConfiguration.ToXml() ), "CK.Setup.StObjEngine, CK.StObj.Engine" );
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
