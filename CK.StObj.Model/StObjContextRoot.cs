using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Core
{
    /// <summary>
    /// Abstract root object that will be dynamically generated to implement a <see cref="IStObjMap"/> and
    /// is able to load concrete (dynamically generated) maps thanks to static <see cref="Load(Assembly, IStObjRuntimeBuilder, IActivityMonitor)"/>.
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
        /// This must be a non virtual, typically private void method with parameters that must contain at least an interface named "IServiceCollection".
        /// Other parameters can be a IActivityMonitor or any services previously registered in the ISimpleServiceContainer by
        /// any <see cref="RegisterStartupServicesMethodName"/>.
        /// </summary>
        public static readonly string ConfigureServicesMethodName = "ConfigureServices";

        static IStObjRuntimeBuilder _stObjBuilder;

        /// <summary>
        /// Default <see cref="IStObjRuntimeBuilder"/> that will be used.
        /// Never null: defaults to <see cref="BasicStObjRuntimeBuilder"/>.
        /// </summary>
        public static IStObjRuntimeBuilder DefaultStObjRuntimeBuilder
        {
            get => _stObjBuilder ?? BasicStObjRuntimeBuilder;
            set => _stObjBuilder = value;
        }

        /// <summary>
        /// Default and trivial implementation of <see cref="IStObjRuntimeBuilder"/> where <see cref="IStObjRuntimeBuilder.CreateInstance"/> implementation 
        /// uses <see cref="Activator.CreateInstance(Type)"/> to call the public default constructor of the type.
        /// </summary>
        public readonly static IStObjRuntimeBuilder BasicStObjRuntimeBuilder = new SimpleStObjRuntimeBuilder();

        class SimpleStObjRuntimeBuilder : IStObjRuntimeBuilder
        {
            public object CreateInstance( Type finalType )
            {
                return Activator.CreateInstance( finalType );
            }
        }

        static readonly HashSet<Assembly> _alreadyLoaded = new HashSet<Assembly>();

        /// <summary>
        /// Loads a previously generated assembly.
        /// </summary>
        /// <param name="a">Already generated assembly.</param>
        /// <param name="runtimeBuilder">Runtime builder to use. When null, <see cref="DefaultStObjRuntimeBuilder"/> is used.</param>
        /// <param name="monitor">Optional monitor for loading operation.</param>
        /// <returns>A <see cref="IStObjMap"/> that provides access to the objects graph.</returns>
        public static IStObjMap Load( Assembly a, IStObjRuntimeBuilder runtimeBuilder = null, IActivityMonitor monitor = null )
        {
            if( a == null ) throw new ArgumentNullException( nameof( a ) );
            IActivityMonitor m = monitor ?? new ActivityMonitor( "CK.Core.StObjContextRoot.Load" );
            bool loaded;
            lock( _alreadyLoaded )
            {
                loaded = _alreadyLoaded.Contains( a );
                if( !loaded ) _alreadyLoaded.Add( a );
            }
            using( m.OpenInfo( loaded ? $"'{a.FullName}' is already loaded." : $"Loading dynamic '{a.FullName}'." ) )
            {
                try
                {
                    Type t = a.GetType( RootContextTypeFullName, true, false );
                    return (IStObjMap)Activator.CreateInstance( t, new object[] { m, runtimeBuilder ?? DefaultStObjRuntimeBuilder } );
                }
                catch( Exception ex )
                {
                    m.Error( "Unable to instanciate StObjMap.", ex );
                    return null;
                }
                finally
                {
                    m.CloseGroup();
                    if( monitor == null ) m.MonitorEnd();
                }
            }
        }

    }
}
