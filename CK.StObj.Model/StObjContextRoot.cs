using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// Holds the name of 'Construct' method: StObjConstruct.
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
        /// This must be a non virtual, typically private void method with parameters that must start with an input (in) <see cref="StObjContextRoot.ServiceRegister"/>.
        /// Following parameters can be a IActivityMonitor or any services previously registered in the ISimpleServiceContainer by
        /// any <see cref="RegisterStartupServicesMethodName"/>.
        /// </summary>
        public static readonly string ConfigureServicesMethodName = "ConfigureServices";

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
                using( monitor.OpenInfo( $"Analysing '{a.FullName}' assembly." ) )
                {
                    info = StObjMapInfo.Create( monitor, a, attr );
                    if( info != null )
                    {
                        if( _alreadyHandled.TryGetValue( info.GeneratedSignature, out var exists ) )
                        {
                            Debug.Assert( exists != null );
                            monitor.Info( $"StObjMap found replaces the one from '{exists.AssemblyName}' that has the same signature." );
                            _alreadyHandled[info.GeneratedSignature] = info;
                            _availableMaps.Remove( exists );
                        }
                        else
                        {
                            _alreadyHandled.Add( info.GeneratedSignature, info );
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
                return _alreadyHandled.GetValueOrDefault( signature );
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

        static List<StObjMapInfo> LockedGetAvailableMapInfos( ref IActivityMonitor? monitor )
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
                return LockedGetStObjMap( info, ref monitor );
            }
        }

        static IStObjMap? LockedGetStObjMap( StObjMapInfo info, ref IActivityMonitor? monitor )
        {
            if( info.StObjMap != null || info.LoadError != null ) return info.StObjMap;
            monitor = LockedEnsureMonitor( monitor );
            using( monitor.OpenInfo( $"Instantiating StObjMap from {info}." ) )
            {
                try
                {
                    return info.StObjMap = (IStObjMap)Activator.CreateInstance( info.StObjMapType, new object[] { monitor } );
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
            lock( _alreadyHandled )
            {
                var info = LockedGetMapInfo( a, ref monitor );
                if( info == null ) return null;
                return LockedGetStObjMap( info, ref monitor );

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
                if( _alreadyHandled.TryGetValue( signature, out var info ) )
                {
                    Debug.Assert( info != null );
                    return LockedGetStObjMap( info, ref monitor );

                }
                return null;
            }
        }
    }
}
