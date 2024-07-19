using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace CK.Core
{
    /// <summary>
    /// Abstract root object that will be dynamically generated to implement a <see cref="IStObjMap"/> and
    /// is able to manage via <see cref="StObjMapInfo"/> multiple maps and load the corresponding (dynamically generated) maps.
    /// </summary>
    public abstract partial class StObjContextRoot
    {
        /// <summary>
        /// Holds the name of the root class.
        /// </summary>
        public static readonly string RootContextTypeName = "GeneratedRootContext";

        /// <summary>
        /// Suffix of the companion signature file when present, contains the RunSignature of the StObjMap.
        /// </summary>
        public const string SuffixSignature = ".signature.txt";

        /// <summary>
        /// Holds the full name of the root class.
        /// </summary>
        public static readonly string RootContextTypeFullName = "CK.StObj." + RootContextTypeName;

        /// <summary>
        /// Holds the name of the construct method: StObjConstruct.
        /// </summary>
        public static readonly string ConstructMethodName = "StObjConstruct";

        /// <summary>
        /// Holds the name of 'Initialize' method: StObjInitialize.
        /// This must be a non virtual, typically private void method with parameters that must be (IActivityMonitor, IStObjMap).
        /// </summary>
        public static readonly string InitializeMethodName = "StObjInitialize";

        /// <summary>
        /// Holds the name 'RegisterStartupServices'.
        /// This must be a non virtual, typically private void method with parameters that must be (IActivityMonitor, ISimpleServiceContainer).
        /// </summary>
        public static readonly string RegisterStartupServicesMethodName = "RegisterStartupServices";

        /// <summary>
        /// Holds the name 'ConfigureServices'.
        /// This must be a non virtual, typically private void method with parameters that must start with an input <see cref="StObjContextRoot.ServiceRegister"/>.
        /// Following parameters can be a IActivityMonitor or any services previously registered in the ISimpleServiceContainer by
        /// any <see cref="RegisterStartupServicesMethodName"/>.
        /// </summary>
        public static readonly string ConfigureServicesMethodName = "ConfigureServices";

        /// <summary>
        /// Holds the name 'OnHostStartAsync'.
        /// This must be a non virtual and private Task or ValueTask returning method with parameters that can be any singleton or scoped services 
        /// (a dedicated scope is created for the call, scoped services won't pollute the application services).
        /// </summary>
        public static readonly string StartMethodNameAsync = "OnHostStartAsync";

        /// <summary>
        /// Holds the name 'OnHostStart'.
        /// This must be a non virtual, typically private void method with parameters that can be any singleton or scoped services 
        /// (a dedicated scope is created for the call, scoped services won't pollute the application services).
        /// </summary>
        public static readonly string StartMethodName = "OnHostStart";

        /// <summary>
        /// Holds the name 'OnHostStopAsync'. Same as the <see cref="StartMethodNameAsync"/>.
        /// </summary>
        public static readonly string StopMethodNameAsync = "OnHostStopAsync";

        /// <summary>
        /// Holds the name 'OnHostStop'. Same as the <see cref="StopMethodNameAsync"/>.
        /// </summary>
        public static readonly string StopMethodName = "OnHostStop";


        // We index the StObjMapInfo by the Assembly and by the Signature: assemblies are stable keys but
        // a new info with the same signature replaces the existing one.
        static readonly Dictionary<object, StObjMapInfo?> _alreadyHandled = new Dictionary<object, StObjMapInfo?>();
        static readonly List<StObjMapInfo> _availableMaps = new List<StObjMapInfo>();
        static ActivityMonitor? _contextMonitor;
        static int _allAssemblyCount;

        /// <summary>
        /// Tries to get a <see cref="StObjMapInfo"/> from an assembly.
        /// This never throws: errors are logged (a new monitor is automatically managed when <paramref name="monitor"/> is null),
        /// and null is returned.
        /// This method like all the methods that manipulates StObjMapInfo are thread/concurrency safe.
        /// </summary>
        /// <param name="a">Candidate assembly.</param>
        /// <param name="monitor">Optional monitor.</param>
        /// <returns>A <see cref="IStObjMap"/> that provides access to the objects graph.</returns>
        public static StObjMapInfo? GetMapInfo( Assembly a, IActivityMonitor? monitor = null )
        {
            Throw.CheckNotNullArgument( a );
            lock( _alreadyHandled )
            {
                return LockedGetMapInfo( a, ref monitor );
            }
        }

        static StObjMapInfo? LockedGetMapInfo( Assembly a, [AllowNull]ref IActivityMonitor monitor )
        {
            if( _alreadyHandled.TryGetValue( a, out var info ) )
            {
                return info;
            }
            LockedEnsureMonitor( ref monitor );
            var attr = a.GetCustomAttributesData().FirstOrDefault( m => m.AttributeType.Name == "SignatureAttribute" && m.AttributeType.Namespace == "CK.StObj" );
            if( attr != null )
            {
                using( monitor.OpenInfo( $"Analyzing '{a.FullName}' assembly." ) )
                {
                    info = StObjMapInfo.Create( monitor, a, attr );
                    if( info != null )
                    {
                        var sha1S = info.GeneratedSignature.ToString();
                        if( _alreadyHandled.TryGetValue( sha1S, out var exists ) )
                        {
                            Debug.Assert( exists != null );
                            monitor.Info( $"StObjMap found with the same signature as an already existing one. Keeping the previous one." );
                            info = exists;
                        }
                        else
                        {
                            _alreadyHandled.Add( sha1S, info );
                            _availableMaps.Add( info );
                        }
                    }
                }
                _alreadyHandled.Add( a, info );
            }
            return info;
        }

        /// <summary>
        /// Tries to get a <see cref="StObjMapInfo"/> from its signature among all loaded assemblies.
        /// This never throws: errors are logged (a new monitor is automatically managed when <paramref name="monitor"/> is null),
        /// and null is returned.
        /// This method like all the methods that manipulates StObjMapInfo are thread/concurrency safe.
        /// </summary>
        /// <param name="signature">Signature to find.</param>
        /// <param name="monitor">Optional monitor.</param>
        /// <returns>The StObjMapInfo if it exists.</returns>
        public static StObjMapInfo? GetMapInfo( SHA1Value signature, IActivityMonitor? monitor = null )
        {
            lock( _alreadyHandled )
            {
                LockedGetAvailableMapInfos( ref monitor );
                return _alreadyHandled.GetValueOrDefault( signature.ToString() );
            }
        }

        static void LockedEnsureMonitor( [AllowNull]ref IActivityMonitor monitor )
        {
            if( monitor == null )
            {
                monitor = (_contextMonitor ??= new ActivityMonitor( "CK.Core.StObjContextRoot" ));
            }
        }

        /// <summary>
        /// Gets all the <see cref="StObjMapInfo"/> available in all the loaded assemblies.
        /// This method like all the methods that manipulates StObjMapInfo are thread/concurrency safe.
        /// </summary>
        /// <returns>An array of all the available informations.</returns>
        public static StObjMapInfo[] GetAvailableMapInfos( IActivityMonitor? monitor = null )
        {
            lock( _alreadyHandled )
            {
                return LockedGetAvailableMapInfos( ref monitor ).ToArray();
            }
        }

        static List<StObjMapInfo> LockedGetAvailableMapInfos( [NotNullIfNotNull("monitor")] ref IActivityMonitor? monitor )
        {
            var all = AppDomain.CurrentDomain.GetAssemblies();
            if( all.Length != _allAssemblyCount )
            {
                // Don't know/trust the ordering: process them all.
                foreach( var a in all )
                {
                    LockedGetMapInfo( a, ref monitor );
                }
                _allAssemblyCount = all.Length;
            }
            return _availableMaps;
        }

        /// <summary>
        /// Gets the <see cref="IStObjMap"/> if no error prevents its instantiation from
        /// the <see cref="StObjMapInfo"/>.
        /// This never throws: errors are logged (a new monitor is automatically managed when <paramref name="monitor"/> is null),
        /// and null is returned.
        /// This method like all the methods that manipulates StObjMapInfo are thread/concurrency safe.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <param name="monitor">Optional monitor.</param>
        /// <returns>The loaded map or null on error.</returns>
        public static IStObjMap? GetStObjMap( StObjMapInfo info, IActivityMonitor? monitor = null )
        {
            Throw.CheckNotNullArgument( info );
            if( info.StObjMap != null || info.LoadError != null ) return info.StObjMap;
            lock( _alreadyHandled )
            {
                return LockedGetStObjMapFromInfo( info, ref monitor );
            }
        }

        static IStObjMap? LockedGetStObjMapFromInfo( StObjMapInfo info, [NotNullIfNotNull( "monitor" )] ref IActivityMonitor? monitor )
        {
            if( info.StObjMap != null || info.LoadError != null ) return info.StObjMap;
            LockedEnsureMonitor( ref monitor );
            using( monitor.OpenInfo( $"Instantiating StObjMap from {info}." ) )
            {
                try
                {
                    return info.StObjMap = (IStObjMap?)Activator.CreateInstance( info.StObjMapType, new object[] { monitor } );
                }
                catch( Exception ex )
                {
                    monitor.Error( ex );
                    info.LoadError = ex.Message;
                    return null;
                }
            }
        }

        /// <summary>
        /// Attempts to load a StObjMap from an assembly.
        /// </summary>
        /// <param name="a">Already generated assembly.</param>
        /// <param name="monitor">Optional monitor for loading operation.</param>
        /// <returns>A <see cref="IStObjMap"/> that provides access to the objects graph.</returns>
        public static IStObjMap? Load( Assembly a, IActivityMonitor? monitor = null )
        {
            Throw.CheckNotNullArgument( a );
            lock( _alreadyHandled )
            {
                var info = LockedGetMapInfo( a, ref monitor );
                if( info == null ) return null;
                var alc = AssemblyLoadContext.GetLoadContext( a );
                if( alc == null )
                {
                    monitor.Warn( $"Assembly '{a.FullName}' is not in any AssemblyLoadContext." );
                }
                else if( alc != AssemblyLoadContext.Default )
                {
                    monitor.Warn( $"Assembly '{a.FullName}' is loaded in non-default AssemblyLoadContext '{alc.Name}'." );
                }
                return LockedGetStObjMapFromInfo( info, ref monitor );
            }
        }

        /// <summary>
        /// Attempts to load a StObjMap with a provided signature from all available maps.
        /// </summary>
        /// <param name="signature">The signature.</param>
        /// <param name="monitor">Optional monitor to use.</param>
        /// <returns>A <see cref="IStObjMap"/> that provides access to the objects graph.</returns>
        public static IStObjMap? Load( SHA1Value signature, IActivityMonitor? monitor = null )
        {
            lock( _alreadyHandled )
            {
                LockedGetAvailableMapInfos( ref monitor );
                if( _alreadyHandled.TryGetValue( signature.ToString(), out var info ) )
                {
                    Debug.Assert( info != null );
                    return LockedGetStObjMapFromInfo( info, ref monitor );

                }
                return null;
            }
        }

        /// <summary>
        /// Attempts to load a StObjMap from an assembly name.
        /// <para>
        /// If a <see cref="SuffixSignature"/> file exists and contains a valid signature, the StObjMap is
        /// loaded from the <see cref="GetAvailableMapInfos(IActivityMonitor?)"/> if it exists.
        /// </para>
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <param name="monitor">Optional monitor to use.</param>
        /// <returns>A <see cref="IStObjMap"/> that provides access to the objects graph.</returns>
        public static IStObjMap? Load( string assemblyName, IActivityMonitor? monitor = null )
        {
            Throw.CheckNotNullOrEmptyArgument( assemblyName );
            Throw.CheckArgument( FileUtil.IndexOfInvalidFileNameChars( assemblyName ) < 0 );

            if( !assemblyName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase )
                && !assemblyName.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
            {
                assemblyName = assemblyName + ".dll";
            }
            string assemblyFullPath = Path.Combine( AppContext.BaseDirectory, assemblyName );
            var signaturePath = assemblyFullPath + SuffixSignature;
            if( File.Exists( signaturePath )
                && SHA1Value.TryParse( File.ReadAllText( signaturePath ), out var signature ) )
            {
                var map = Load( signature, monitor );
                if( map != null ) return map;
            }

            lock( _alreadyHandled )
            {
                LockedEnsureMonitor( ref monitor );
                using( monitor.OpenInfo( $"Loading StObj map from '{assemblyName}'." ) )
                {
                    try
                    {
                        // LoadFromAssemblyPath caches the assemblies by their path.
                        // No need to do it.
                        var a = AssemblyLoadContext.Default.LoadFromAssemblyPath( assemblyFullPath );
                        var info = LockedGetMapInfo( a, ref monitor );
                        if( info == null ) return null;
                        return LockedGetStObjMapFromInfo( info, ref monitor );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( ex );
                        return null;
                    }
                }
            }

        }
    }
}
