using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

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
            if( a == null ) throw new ArgumentNullException( nameof( a ) );
            lock( _alreadyHandled )
            {
                return LockedGetMapInfo( a, ref monitor );
            }
        }

        static StObjMapInfo? LockedGetMapInfo( Assembly a, ref IActivityMonitor? monitor )
        {
            if( _alreadyHandled.TryGetValue( a, out var info ) )
            {
                return info;
            }
            monitor = LockedEnsureMonitor( monitor );
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
                            monitor.Info( $"StObjMap found replaces the one from '{exists.AssemblyName}' that has the same signature." );
                            _alreadyHandled[sha1S] = info;
                            _availableMaps.Remove( exists );
                        }
                        else
                        {
                            _alreadyHandled.Add( sha1S, info );
                        }
                        _availableMaps.Add( info );
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

        static IActivityMonitor LockedEnsureMonitor( IActivityMonitor? monitor )
        {
            if( monitor == null )
            {
                monitor = (_contextMonitor ??= new ActivityMonitor( "CK.Core.StObjContextRoot" ));
            }
            return monitor;
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
            if( info == null ) throw new ArgumentNullException( nameof( info ) );
            if( info.StObjMap != null || info.LoadError != null ) return info.StObjMap;
            lock( _alreadyHandled )
            {
                return LockedGetStObjMapFromInfo( info, ref monitor );
            }
        }

        static IStObjMap? LockedGetStObjMapFromInfo( StObjMapInfo info, [NotNullIfNotNull( "monitor" )] ref IActivityMonitor? monitor )
        {
            if( info.StObjMap != null || info.LoadError != null ) return info.StObjMap;
            monitor = LockedEnsureMonitor( monitor );
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
            if( a == null ) throw new ArgumentNullException( nameof( a ) );
            lock( _alreadyHandled )
            {
                var info = LockedGetMapInfo( a, ref monitor );
                if( info == null ) return null;
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
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <param name="monitor">Optional monitor to use.</param>
        /// <returns>A <see cref="IStObjMap"/> that provides access to the objects graph.</returns>
        public static IStObjMap? Load( string assemblyName, IActivityMonitor? monitor = null )
        {
            Throw.CheckNotNullOrEmptyArgument( assemblyName );

            // We could support here that if a / or \ appear in the name, then its a path and then we could use Assembly.LoadFile.
            if( FileUtil.IndexOfInvalidFileNameChars( assemblyName ) >= 0 ) Throw.ArgumentException( $"Invalid characters in '{assemblyName}'.", nameof( assemblyName ) );

            string assemblyNameWithExtension; 
            if( assemblyName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) || assemblyName.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) )
            {
                assemblyNameWithExtension = assemblyName;
                assemblyName = assemblyName.Substring( 0, assemblyName.Length - 4 );
            }
            else
            {
                assemblyNameWithExtension = assemblyName + ".dll";
            }
            string assemblyFullPath = System.IO.Path.Combine( AppContext.BaseDirectory, assemblyNameWithExtension );
            string assemblyFullPathSig = assemblyFullPath + Setup.StObjEngineConfiguration.ExistsSignatureFileExtension;

            lock( _alreadyHandled )
            {
                monitor = LockedEnsureMonitor( monitor );
                using( monitor.OpenInfo( $"Loading StObj map from '{assemblyName}'." ) )
                {
                    try
                    {
                        StObjMapInfo? info;
                        if( System.IO.File.Exists( assemblyFullPathSig ) )
                        {
                            var sig = System.IO.File.ReadAllText( assemblyFullPathSig );
                            LockedGetAvailableMapInfos( ref monitor );
                            info = _alreadyHandled.GetValueOrDefault( sig );
                            if( info != null )
                            {
                                monitor.CloseGroup( $"Found existing map from signature file {assemblyNameWithExtension}{Setup.StObjEngineConfiguration.ExistsSignatureFileExtension}: {info}." );
                                return LockedGetStObjMapFromInfo( info, ref monitor );
                            }
                            monitor.Warn( $"Unable to find an existing map based on the Signature file '{assemblyNameWithExtension}{Setup.StObjEngineConfiguration.ExistsSignatureFileExtension}' ({sig}). Trying to load the assembly." );
                        }
                        else
                        {
                            monitor.Warn( $"No Signature file '{assemblyNameWithExtension}{Setup.StObjEngineConfiguration.ExistsSignatureFileExtension}' found. Trying to load the assembly." );
                        }
                        var a = Assembly.LoadFile( assemblyFullPath );
                        info = LockedGetMapInfo( a, ref monitor );
                        if( info == null ) return null;
                        return LockedGetStObjMapFromInfo( info, ref monitor );
                    }
                    catch( Exception ex )
                    {
                        Debug.Assert( monitor != null, "Not detected by nullable analysis..." );
                        monitor.Error( ex );
                        return null;
                    }
                }
            }

        }
    }
}
