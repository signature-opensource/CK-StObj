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
            var fName = _stObjMap.GeneratedAssemblyName + ".dll";
            var fNameSig = fName + StObjEngineConfiguration.ExistsSignatureFileExtension;

            var file = _stObjMap.BinFolder.AppendPart( fName );
            var fileSig = _stObjMap.BinFolder.AppendPart( fNameSig );

            bool fExists = File.Exists( file );
            bool fSigExists = File.Exists( fileSig );

            if( !fExists && !fSigExists )
            {
                using( _stObjMap.Monitor.OpenInfo( $"File '{file}' does not exist nor '{fNameSig}'. Running StObjSetup to create it." ) )
                {
                    Debug.Assert( _mixin != null, "It has been initialized by ITestHelperResolvedCallback.OnTestHelperGraphResolved." );
                    var defaultConf = CreateDefaultConfiguration( _mixin! );
                    DoRunStObjSetup( defaultConf.Configuration, defaultConf.ForceSetup );
                }
            }
            else if( fExists && fSigExists )
            {
                _stObjMap.Monitor.Info( $"Both file '{fileSig}' and '{fName}' files found. An existing loaded StObjMap with the signature should exist otherwise the '{fName}' will be loaded." );
            }
            else
            {
                if( fSigExists ) _stObjMap.Monitor.Info( $"Signature file '{fileSig}' found: An existing loaded StObjMap with the signature should exist." );
                else _stObjMap.Monitor.Info( $"File '{file}' found. It will be loaded." );
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

            var stObjConf = new StObjEngineConfiguration
            {
                RevertOrderingNames = helper.StObjRevertOrderingNames,
                TraceDependencySorterInput = helper.StObjTraceGraphOrdering,
                TraceDependencySorterOutput = helper.StObjTraceGraphOrdering,
                GeneratedAssemblyName = helper.GeneratedAssemblyName
            };
            var b = new BinPathConfiguration
            {
                CompileOption = CompileOption.Compile,
                Path = helper.BinFolder,
                GenerateSourceFiles = helper.StObjGenerateSourceFiles,
                ProjectPath = helper.TestProjectFolder
            };
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
