using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjSetup;
using CK.Text;
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
        IStObjSetupTestHelper _mixin;
        EventHandler<StObjSetupRunningEventArgs> _stObjSetupRunning;
        bool _generateSourceFiles;
        bool _revertOrderingNames;
        bool _traceGraphOrdering;

        internal StObjSetupTestHelper( ITestHelperConfiguration config, ICKSetupTestHelper ckSetup, IStObjMapTestHelper stObjMap )
        {
            _ckSetup = ckSetup;
            _stObjMap = stObjMap;
            stObjMap.StObjMapLoading += OnStObjMapLoading;

            var oldConfig = config.GetConfigValue( "DBSetup/GenerateSourceFiles" );
            if( oldConfig.HasValue ) throw new Exception( $"Configuration DBSetup/GenerateSourceFiles entry in '{oldConfig.Value.BasePath}' must be updated to StObjSetup/StObjGenerateSourceFiles."  );

            _generateSourceFiles = config.GetBoolean( "StObjSetup/StObjGenerateSourceFiles" ) ?? true;
            _revertOrderingNames = config.GetBoolean( "StObjSetup/StObjRevertOrderingNames" ) ?? false;
            _traceGraphOrdering = config.GetBoolean( "StObjSetup/StObjTraceGraphOrdering" ) ?? false;
        }

        void OnStObjMapLoading( object sender, EventArgs e )
        {
            var file = _stObjMap.BinFolder.AppendPart( _stObjMap.GeneratedAssemblyName + ".dll" );
            if( !File.Exists( file ) )
            {
                _stObjMap.Monitor.Info( $"File '{file}' does not exist. Running StObjSetup to create it." );
                var defaultConf = CreateDefaultConfiguration( _mixin );
                DoRunStObjSetup( defaultConf.Configuration, defaultConf.ForceSetup );
            }
        }

        /// <summary>
        /// Low level helper that initializes a new <see cref="StObjEngineConfiguration"/> and computes the force setup flag
        /// that can be used by other helpers that need to run a DBSetup.
        /// </summary>
        /// <param name="helper">The <see cref="IStObjSetupTestHelper"/> helper.</param>
        /// <returns>The configuration and the flag.</returns>
        static public (StObjEngineConfiguration Configuration, bool ForceSetup) CreateDefaultConfiguration( IStObjSetupTestHelper helper )
        {
            bool forceSetup = helper.CKSetup.DefaultForceSetup
                                || helper.CKSetup.DefaultBinPaths.Append( helper.BinFolder )
                                        .Select( p => p.AppendPart( helper.GeneratedAssemblyName + ".dll" ) )
                                        .Any( p => !File.Exists( p ) );

            var stObjConf = new StObjEngineConfiguration();
            stObjConf.RevertOrderingNames = helper.StObjRevertOrderingNames;
            stObjConf.TraceDependencySorterInput = helper.StObjTraceGraphOrdering;
            stObjConf.TraceDependencySorterOutput = helper.StObjTraceGraphOrdering;
            stObjConf.GeneratedAssemblyName = helper.GeneratedAssemblyName;
            var b = new BinPath();
            b.Path = helper.BinFolder;
            b.GenerateSourceFiles = helper.StObjGenerateSourceFiles;
            stObjConf.BinPaths.Add( b );

            return (stObjConf, forceSetup);
        }

        CKSetupRunResult DoRunStObjSetup( StObjEngineConfiguration stObjConf, bool forceSetup )
        {
            if( stObjConf == null ) throw new ArgumentNullException( nameof( stObjConf ) );
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

        CKSetupRunResult IStObjSetupTestHelperCore.RunStObjSetup( StObjEngineConfiguration configuration, bool forceSetup ) => DoRunStObjSetup( configuration, forceSetup );

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
