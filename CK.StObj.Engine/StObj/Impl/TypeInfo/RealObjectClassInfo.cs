using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Reflection;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Specialized <see cref="CKTypeInfo"/> for <see cref="IRealObject"/> classes.
    /// </summary>
    internal class RealObjectClassInfo : CKTypeInfo, IStObjTypeInfoFromParent
    {
        Type[] _ambientInterfaces;
        Type[] _thisAmbientInterfaces;

        class TypeInfoForBaseClasses : IStObjTypeInfoFromParent, IStObjTypeRootParentInfo
        {
            public IReadOnlyList<AmbientPropertyInfo> AmbientProperties { get; private set; }
            public IReadOnlyList<InjectObjectInfo> InjectObjects { get; private set; }
            public IReadOnlyList<StObjPropertyInfo> StObjProperties { get; private set; }
            public int SpecializationDepth { get; private set; }
            public Type Container { get; private set; }
            public DependentItemKind ItemKind { get; private set; }
            public TrackAmbientPropertiesMode TrackAmbientProperties { get; private set; }
            // The following properties are specific to pure (non stObj) base types: IStObjTypeRootParentInfo
            public IReadOnlyList<(MethodInfo, ParameterInfo[])> StObjConstructCollector { get; private set; }
            public IReadOnlyList<MethodInfo> StObjInitializeCollector { get; private set; }
            public IReadOnlyList<MethodInfo> RegisterStartupServicesCollector { get; private set; }
            public IReadOnlyList<ParameterInfo[]> ConfigureServicesCollector { get; private set; }


            static object _lock = new object();
            static Dictionary<Type, TypeInfoForBaseClasses> _cache;

            static public TypeInfoForBaseClasses GetFor( IActivityMonitor monitor, Type t, CKTypeKindDetector ambientTypeKind )
            {
                TypeInfoForBaseClasses result = null;
                // Poor lock: we don't care here. Really.
                lock( _lock )
                {
                    if( _cache == null ) _cache = new Dictionary<Type, TypeInfoForBaseClasses>();
                    else _cache.TryGetValue( t, out result );
                    if( result == null )
                    {
                        result = new TypeInfoForBaseClasses();
                        if( t == typeof( object ) )
                        {
                            result.AmbientProperties = Util.Array.Empty<AmbientPropertyInfo>();
                            result.InjectObjects = Util.Array.Empty<InjectObjectInfo>();
                            result.StObjProperties = Util.Array.Empty<StObjPropertyInfo>();
                        }
                        else
                        {
                            // At least below object :-).
                            result.SpecializationDepth = 1;
                            // For ItemKind & TrackAmbientProperties, walks up the inheritance chain and combines the StObjAttribute.
                            // We compute the SpecializationDepth: once we know it, we can inject it the Ambient Properties discovery.
                            var a = StObjAttributesReader.GetStObjAttributeForExactType( t, monitor );
                            if( a != null )
                            {
                                result.Container = a.Container;
                                result.ItemKind = (DependentItemKind)a.ItemKind;
                                result.TrackAmbientProperties = a.TrackAmbientProperties;
                            }
                            List<(MethodInfo, ParameterInfo[])> stObjConstructCollector = null;
                            List<MethodInfo> stObjInitializeCollector = null;
                            List<MethodInfo> registerStartupServicesCollector = null;
                            List<ParameterInfo[]> configureServicesCollector = null;
                            CollectMethods( monitor, t, ref stObjConstructCollector, ref stObjInitializeCollector, ref registerStartupServicesCollector, ref configureServicesCollector );
                            Type tAbove = t.BaseType;
                            while( tAbove != typeof( object ) )
                            {
                                result.SpecializationDepth = result.SpecializationDepth + 1;
                                var aAbove = StObjAttributesReader.GetStObjAttributeForExactType( tAbove, monitor );
                                if( aAbove != null )
                                {
                                    if( result.Container == null ) result.Container = aAbove.Container;
                                    if( result.ItemKind == DependentItemKind.Unknown ) result.ItemKind = (DependentItemKind)aAbove.ItemKind;
                                    if( result.TrackAmbientProperties == TrackAmbientPropertiesMode.Unknown ) result.TrackAmbientProperties = aAbove.TrackAmbientProperties;
                                }
                                CollectMethods( monitor, tAbove, ref stObjConstructCollector, ref stObjInitializeCollector, ref registerStartupServicesCollector, ref configureServicesCollector );
                                tAbove = tAbove.BaseType;
                            }
                            if( stObjConstructCollector != null )
                            {
                                stObjConstructCollector.Reverse();
                                result.StObjConstructCollector = stObjConstructCollector;
                            }
                            if( stObjInitializeCollector != null )
                            {
                                stObjInitializeCollector.Reverse();
                                result.StObjInitializeCollector = stObjInitializeCollector;
                            }
                            if( registerStartupServicesCollector != null )
                            {
                                registerStartupServicesCollector.Reverse();
                                result.RegisterStartupServicesCollector = registerStartupServicesCollector;
                            }
                            if( configureServicesCollector != null )
                            {
                                configureServicesCollector.Reverse();
                                result.ConfigureServicesCollector = configureServicesCollector;
                            }
                            // Ambient, InjectObjects & StObj Properties (uses a recursive function).
                            List<StObjPropertyInfo> stObjProperties = new List<StObjPropertyInfo>();
                            IReadOnlyList<AmbientPropertyInfo> propList;
                            IReadOnlyList<InjectObjectInfo> injectList;
                            CreateAllAmbientPropertyList( monitor, t, result.SpecializationDepth, ambientTypeKind, stObjProperties, out propList, out injectList );
                            Debug.Assert( propList != null && injectList != null );
                            result.AmbientProperties = propList;
                            result.InjectObjects = injectList;
                            result.StObjProperties = stObjProperties;
                        }
                        _cache.Add( t, result );
                    }
                }
                return result;
            }

            static void CollectMethods( IActivityMonitor monitor, Type tAbove, ref List<(MethodInfo, ParameterInfo[])> stObjConstructCollector, ref List<MethodInfo> stObjInitializeCollector, ref List<MethodInfo> registerStartupServicesCollector, ref List<ParameterInfo[]> configureServicesCollector )
            {
                var stObjConstruct = ReadStObjConstruct( monitor, tAbove );
                if( stObjConstruct.Item2 != null )
                {
                    if( stObjConstruct.Item2.Any( p => p.GetCustomAttribute<ContainerAttribute>() != null ) )
                    {
                        monitor.Error( $"'{tAbove.FullName}.{StObjContextRoot.ConstructMethodName}' method cannot have a parameter marked with [Container] attribute." );
                    }
                    else
                    {
                        if( stObjConstructCollector == null ) stObjConstructCollector = new List<(MethodInfo, ParameterInfo[])>();
                        stObjConstructCollector.Add( stObjConstruct );
                    }
                }
                var stObjInitialize = ReadStObjInitialize( monitor, tAbove );
                if( stObjInitialize != null )
                {
                    if( stObjInitializeCollector == null ) stObjInitializeCollector = new List<MethodInfo>();
                    stObjInitializeCollector.Add( stObjInitialize );
                }
                var registerStartupServices = ReadRegisterStartupServices( monitor, tAbove );
                if( registerStartupServices != null )
                {
                    if( registerStartupServicesCollector == null ) registerStartupServicesCollector = new List<MethodInfo>();
                    registerStartupServicesCollector.Add( registerStartupServices );
                }
                var configureServices = ReadConfigureServices( monitor, tAbove );
                if( configureServices != null )
                {
                    if( configureServicesCollector == null ) configureServicesCollector = new List<ParameterInfo[]>();
                    configureServicesCollector.Add( configureServices );
                }
            }

            /// <summary>
            /// Recursive function to collect/merge Ambient Properties, InjectObject and StObj Properties on base (non IRealObject) types.
            /// </summary>
            static void CreateAllAmbientPropertyList(
                IActivityMonitor monitor,
                Type type,
                int specializationLevel,
                CKTypeKindDetector ambientTypeKind,
                List<StObjPropertyInfo> stObjProperties,
                out IReadOnlyList<AmbientPropertyInfo> apListResult,
                out IReadOnlyList<InjectObjectInfo> acListResult )
            {
                if( type == typeof( object ) )
                {
                    apListResult = Util.Array.Empty<AmbientPropertyInfo>();
                    acListResult = Util.Array.Empty<InjectObjectInfo>();
                }
                else
                {
                    IList<AmbientPropertyInfo> apCollector;
                    IList<InjectObjectInfo> acCollector;
                    AmbientPropertyOrInjectObjectInfo.CreateAmbientPropertyListForExactType( monitor, type, specializationLevel, ambientTypeKind, stObjProperties, out apCollector, out acCollector );

                    CreateAllAmbientPropertyList( monitor, type.BaseType, specializationLevel - 1, ambientTypeKind, stObjProperties, out apListResult, out acListResult );

                    apListResult = AmbientPropertyOrInjectObjectInfo.MergeWithAboveProperties( monitor, apListResult, apCollector );
                    acListResult = AmbientPropertyOrInjectObjectInfo.MergeWithAboveProperties( monitor, acListResult, acCollector );
                }
            }
        }

        internal RealObjectClassInfo( IActivityMonitor monitor, RealObjectClassInfo parent, Type t, IServiceProvider provider, CKTypeKindDetector ambientTypeKind, bool isExcluded )
            : base( monitor, parent, t, provider, isExcluded, null )
        {
            Debug.Assert( parent == Generalization );
            if( IsExcluded ) return;

            IStObjTypeInfoFromParent infoFromParent = Generalization;
            if( infoFromParent == null )
            {
                var b = TypeInfoForBaseClasses.GetFor( monitor, t.BaseType, ambientTypeKind );
                BaseTypeInfo = b;
                infoFromParent = b;
            }
            SpecializationDepth = infoFromParent.SpecializationDepth + 1;

            // StObj properties are initialized with inherited (non Real Object ones).
            List<StObjPropertyInfo> stObjProperties = new List<StObjPropertyInfo>();
            if( Generalization == null ) stObjProperties.AddRange( infoFromParent.StObjProperties );
            // StObj properties are then read from StObjPropertyAttribute on class
            foreach( StObjPropertyAttribute p in t.GetCustomAttributes( typeof( StObjPropertyAttribute ), Generalization == null ) )
            {
                if( String.IsNullOrWhiteSpace( p.PropertyName ) )
                {
                    monitor.Error( $"Unnamed or whitespace StObj property on '{t.FullName}'. Attribute must be configured with a valid PropertyName." );
                }
                else if( p.PropertyType == null )
                {
                    monitor.Error( $"StObj property named '{p.PropertyName}' for '{t.FullName}' has no PropertyType defined. It should be typeof(object) to explicitly express that any type is accepted." );
                }
                else if( stObjProperties.Find( sP => sP.Name == p.PropertyName ) != null )
                {
                    monitor.Error( $"StObj property named '{p.PropertyName}' for '{t.FullName}' is defined more than once. It should be declared only once." );
                }
                else
                {
                    stObjProperties.Add( new StObjPropertyInfo( t, p.ResolutionSource, p.PropertyName, p.PropertyType, null ) );
                }
            }
            // Ambient properties for the exact Type (can be null). 
            // In the same time, StObjPropertyAttribute that are associated to actual properties are collected into stObjProperties.
            IList<AmbientPropertyInfo> apCollector;
            IList<InjectObjectInfo> acCollector;
            AmbientPropertyInfo.CreateAmbientPropertyListForExactType( monitor, Type, SpecializationDepth, ambientTypeKind, stObjProperties, out apCollector, out acCollector );
            // For type that have no Generalization: we must handle [AmbientProperty], [InjectObject] and [StObjProperty] on base classes (we may not have CKTypeInfo object 
            // since they are not necessarily IRealObject, we use infoFromParent abstraction).
            AmbientProperties = AmbientPropertyInfo.MergeWithAboveProperties( monitor, infoFromParent.AmbientProperties, apCollector );
            InjectObjects = AmbientPropertyInfo.MergeWithAboveProperties( monitor, infoFromParent.InjectObjects, acCollector );
            StObjProperties = stObjProperties;
            Debug.Assert( InjectObjects != null && AmbientProperties != null && StObjProperties != null );

            // Simple detection of name clashing: I prefer to keep it simple and check property kind coherency here instead of injecting 
            // the detection inside CreateAmbientPropertyListForExactType and MergeWithAboveProperties with a multi-type property collector. 
            // Code is complicated enough and it should be not really less efficient to use the dictionary below once all properties
            // have been resolved...
            {
                var names = new Dictionary<string, INamedPropertyInfo>();
                foreach( var newP in AmbientProperties.Cast<INamedPropertyInfo>().Concat( InjectObjects ).Concat( StObjProperties ) )
                {
                    INamedPropertyInfo exists;
                    if( names.TryGetValue( newP.Name, out exists ) )
                    {
                        monitor.Error( $"{newP.Kind} property '{newP.DeclaringType.FullName}.{newP.Name}' is declared as a '{exists.Kind}' property by '{exists.DeclaringType.FullName}'. Property names must be distinct." );
                    }
                    else names.Add( newP.Name, newP );
                }
            }

            #region IStObjAttribute (ItemKind, Container & Type requirements).
            // There is no Container inheritance at this level.
            var a = StObjAttributesReader.GetStObjAttributeForExactType( t, monitor );
            if( a != null )
            {
                Container = a.Container;
                ItemKind = (DependentItemKind)a.ItemKind;
                TrackAmbientProperties = a.TrackAmbientProperties;
                RequiredBy = a.RequiredBy;
                Requires = a.Requires;
                Children = a.Children;
                Groups = a.Groups;
            }
            // We inherit only from non Real Object base classes, not from Generalization if it exists.
            // This is to let the inheritance of these 3 properties take dynamic configuration (IStObjStructuralConfigurator) 
            // changes into account: inheritance will take place after configuration so that a change on a base class
            // will be inherited if not explicitly defined at the class level.
            if( Generalization == null )
            {
                if( Container == null ) Container = infoFromParent.Container;
                if( ItemKind == DependentItemKind.Unknown ) ItemKind = infoFromParent.ItemKind;
                if( TrackAmbientProperties == TrackAmbientPropertiesMode.Unknown ) TrackAmbientProperties = infoFromParent.TrackAmbientProperties;
            }
            // Requires, Children, Groups and RequiredBy are directly handled by MutableItem (they are wrapped in MutableReference 
            // so that IStObjStructuralConfigurator objects can alter them).
            #endregion

            #region StObjConstruct method & parameters (handles single [Container] parameter attribute).
            (StObjConstruct, ConstructParameters) = ReadStObjConstruct( monitor, t );
            if( ConstructParameters != null )
            {
                ContainerConstructParameterIndex = -1;
                for( int i = 0; i < ConstructParameters.Length; ++i )
                {
                    var p = ConstructParameters[i];

                    // Is it marked with ContainerAttribute?
                    bool isContainerParameter = p.GetCustomAttribute<ContainerAttribute>() != null;
                    if( isContainerParameter )
                    {
                        if( ContainerConstructParameterIndex >= 0 )
                        {
                            monitor.Error( $"'{t.FullName}.{StObjContextRoot.ConstructMethodName}' method has more than one parameter marked with [Container] attribute." );
                        }
                        else
                        {
                            // The Parameter is the Container.
                            if( Container != null && Container != p.ParameterType )
                            {
                                monitor.Error( $"'{t.FullName}.{StObjContextRoot.ConstructMethodName}' method parameter '{p.Name}' defines the Container as '{p.ParameterType.FullName}' but an attribute on the class declares the Container as '{Container.FullName}'." );
                            }
                            ContainerConstructParameterIndex = i;
                            Container = p.ParameterType;
                        }
                    }
                }
            }
            #endregion

            // StObjInitialize method checks: (non virtual) void StObjInitialize( IActivityMonitor, IStObjObjectMap )
            StObjInitialize = ReadStObjInitialize( monitor, t );

            // RegisterStartupServices method checks: (non virtual) void RegisterStartupServices( IActivityMonitor, SimpleServiceContainer )
            RegisterStartupServices = ReadRegisterStartupServices( monitor, t );

            // ConfigureServices method checks: (non virtual) void ConfigureServices( [in] StObjContextRoot.ServiceRegister, ... )
            ConfigureServicesParameters = ReadConfigureServices( monitor, t );
        }

        #region Read StObjConstruct, StObjInitialize, RegisterStartupServices and ConfigureServices methods.

        /// <summary>
        /// Checks that StObjConstruct method if it exists is non virtual: void StObjConstruct( ... ).
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type.</param>
        /// <returns>The method if it exists.</returns>
        static (MethodInfo, ParameterInfo[]) ReadStObjConstruct( IActivityMonitor monitor, Type t )
        {
            var stObjConstruct = t.GetMethod( StObjContextRoot.ConstructMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            // From Construct to StObjConstruct...
            if( stObjConstruct == null )
            {
                stObjConstruct = t.GetMethod( "Construct", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                if( stObjConstruct != null )
                {
                    monitor.Error( $"Deprecated: Method '{t.FullName}.Construct' must be named '{StObjContextRoot.ConstructMethodName}' instead." );
                }
            }
            if( stObjConstruct != null )
            {
                if( stObjConstruct.IsVirtual )
                {
                    monitor.Error( $"Method '{t.FullName}.{StObjContextRoot.ConstructMethodName}' must NOT be virtual." );
                }
                else
                {
                    var p = stObjConstruct.GetParameters();
                    if( p.Length == 0 )
                    {
                        monitor.Warn( $"Method '{t.FullName}.{StObjContextRoot.ConstructMethodName}' has no parameters. It will be ignored." );
                    }
                    else return (stObjConstruct, p);
                }
            }
            return (null, null);
        }

        /// <summary>
        /// Checks that StObjInitialize method if it exists is non virtual: void StObjInitialize( IActivityMonitor, IStObjObjectMap ).
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type.</param>
        /// <returns>The method if it exists.</returns>
        static MethodInfo ReadStObjInitialize( IActivityMonitor monitor, Type t )
        {
            var stObjInitialize = t.GetMethod( StObjContextRoot.InitializeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            if( stObjInitialize != null )
            {
                if( stObjInitialize.IsVirtual )
                {
                    monitor.Error( $"'{t.FullName}.{StObjContextRoot.InitializeMethodName}' method must NOT be virtual." );
                }
                else
                {
                    var parameters = stObjInitialize.GetParameters();
                    if( parameters.Length != 2
                        || parameters[0].ParameterType != typeof( IActivityMonitor )
                        || parameters[1].ParameterType != typeof( IStObjObjectMap ) )
                    {
                        monitor.Error( $"'{t.FullName}.{StObjContextRoot.InitializeMethodName}' method parameters must be (IActivityMonitor, IStObjObjectMap)." );
                        if( parameters.Length >= 2 && parameters[1].ParameterType == typeof( IStObjMap ) )
                        {
                            monitor.Error( $"Before v12, this was IStObjMap but at StObjInitialize time, Services are not available: the IStObjObjectMap gives access to only Real Objects." );
                        }
                    }
                }
            }
            return stObjInitialize;
        }

        /// <summary>
        /// Checks that RegisterStartupServices method if it exists is non virtual: void RegisterStartupServices( IActivityMonitor, SimpleServiceContainer ).
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type.</param>
        /// <returns>The method if it exists.</returns>
        static MethodInfo ReadRegisterStartupServices( IActivityMonitor monitor, Type t )
        {
            var registerStartupServices = t.GetMethod( StObjContextRoot.RegisterStartupServicesMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            if( registerStartupServices != null )
            {
                if( registerStartupServices.IsVirtual )
                {
                    monitor.Error( $"'{t.FullName}.{StObjContextRoot.RegisterStartupServicesMethodName}' method must NOT be virtual." );
                }
                else
                {
                    var parameters = registerStartupServices.GetParameters();
                    if( parameters.Length != 2
                        || parameters[0].ParameterType != typeof( IActivityMonitor )
                        || parameters[1].ParameterType != typeof( SimpleServiceContainer ) )
                    {
                        monitor.Error( $"'{t.FullName}.{StObjContextRoot.InitializeMethodName}' method parameters must be (IActivityMonitor, SimpleServiceContainer)." );
                    }
                }
            }
            return registerStartupServices;
        }

        /// <summary>
        /// Checks that ConfigureServices method if it exists is non virtual: void ConfigureServices( [in] StObjContextRoot.ServiceRegister, ... ).
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="t">The type.</param>
        /// <returns>The method's parameters or null.</returns>
        static ParameterInfo[] ReadConfigureServices( IActivityMonitor monitor, Type t )
        {
            var configureServices = t.GetMethod( StObjContextRoot.ConfigureServicesMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            if( configureServices != null )
            {
                if( configureServices.IsVirtual )
                {
                    monitor.Error( $"'{t.FullName}.{StObjContextRoot.ConfigureServicesMethodName}' method must NOT be virtual." );
                }
                else
                {
                    var parameters = configureServices.GetParameters();
                    if( parameters.Length == 0
                        || (parameters[0].ParameterType != typeof( StObjContextRoot.ServiceRegister )
                            && !(parameters[0].ParameterType.IsByRef
                                 && parameters[0].ParameterType.GetElementType() == typeof( StObjContextRoot.ServiceRegister ))) )
                    {
                        monitor.Error( $"'{t.FullName}.{StObjContextRoot.ConfigureServicesMethodName}': first parameter must be a StObjContextRoot.ServiceRegister." );
                    }
                    else
                    {
                        return parameters;
                    }
                }
            }
            return null;
        }

        #endregion

        /// <summary>
        /// Gets the information above the root if this is the root (null otherwise): this
        /// property is not null if <see cref="Generalization"/> is null and vice versa.
        /// </summary>
        public IStObjTypeRootParentInfo BaseTypeInfo { get; }

        public new RealObjectClassInfo Generalization => (RealObjectClassInfo)base.Generalization;

        public IReadOnlyList<AmbientPropertyInfo> AmbientProperties { get; private set; }

        public IReadOnlyList<InjectObjectInfo> InjectObjects { get; private set; }

        public IReadOnlyList<StObjPropertyInfo> StObjProperties { get; private set; }

        public Type Container { get; private set; }

        /// <summary>
        /// Gets the specialization depth from root object type (Object's depth being 0).
        /// </summary>
        public int SpecializationDepth { get; private set; }

        public DependentItemKind ItemKind { get; private set; }

        public TrackAmbientPropertiesMode TrackAmbientProperties { get; private set; }

        public readonly Type[] Requires;

        public readonly Type[] RequiredBy;

        public readonly Type[] Children;

        public readonly Type[] Groups;

        public readonly MethodInfo StObjConstruct;

        public readonly ParameterInfo[] ConstructParameters;

        public readonly int ContainerConstructParameterIndex;

        public readonly MethodInfo StObjInitialize;

        public IEnumerable<MethodInfo> AllStObjInitialize
        {
            get
            {
                var initializers = BaseTypeInfo?.StObjInitializeCollector ?? Enumerable.Empty<MethodInfo>();
                return StObjInitialize == null ? initializers : initializers.Append( StObjInitialize );
            }
        }

        public readonly MethodInfo RegisterStartupServices;

        public IEnumerable<MethodInfo> AllRegisterStartupServices
        {
            get
            {
                var registers = BaseTypeInfo?.RegisterStartupServicesCollector ?? Enumerable.Empty<MethodInfo>();
                return RegisterStartupServices == null ? registers : registers.Append( RegisterStartupServices );
            }
        }

        /// <summary>
        /// ConfigureService parameters. The first parameter is a StObjContextRoot.ServiceRegister.
        /// When null no ConfigureService method exists.
        /// </summary>
        public readonly ParameterInfo[] ConfigureServicesParameters;

        public IEnumerable<ParameterInfo[]> AllConfigureServicesParameters
        {
            get
            {
                var conf = BaseTypeInfo?.ConfigureServicesCollector ?? Enumerable.Empty<ParameterInfo[]>();
                return ConfigureServicesParameters == null ? conf : conf.Append( ConfigureServicesParameters );
            }
        }

        Type[] EnsureAllAmbientInterfaces( IActivityMonitor m, CKTypeKindDetector d )
        {
            return _ambientInterfaces
                ?? (_ambientInterfaces = Type.GetInterfaces().Where( t => (d.GetKind( m, t ) & CKTypeKind.RealObject) == CKTypeKind.RealObject ).ToArray());
        }

        internal Type[] EnsureThisAmbientInterfaces( IActivityMonitor m, CKTypeKindDetector d )
        {
            return _thisAmbientInterfaces ?? (_thisAmbientInterfaces = Generalization != null
                                                        ? EnsureAllAmbientInterfaces( m, d ).Except( Generalization.EnsureAllAmbientInterfaces( m, d ) ).ToArray()
                                                        : EnsureAllAmbientInterfaces( m, d ));
        }


        internal bool CreateMutableItemsPath(
            IActivityMonitor monitor,
            IServiceProvider services,
            StObjObjectEngineMap engineMap,
            MutableItem generalization,
            IDynamicAssembly tempAssembly,
            List<(MutableItem, ImplementableTypeInfo)> lastConcretes,
            List<Type> abstractTails )
        {
            Debug.Assert( tempAssembly != null );
            var item = new MutableItem( this, generalization, engineMap );
            bool concreteBelow = false;
            foreach( RealObjectClassInfo c in Specializations )
            {
                Debug.Assert( !c.IsExcluded );
                concreteBelow |= c.CreateMutableItemsPath( monitor, services, engineMap, item, tempAssembly, lastConcretes, abstractTails );
            }
            if( !concreteBelow )
            {
                ImplementableTypeInfo autoImplementor = null;
                if( Type.IsAbstract
                    && (autoImplementor = InitializeImplementableTypeInfo( monitor, tempAssembly )) == null )
                {
                    abstractTails.Add( Type );
                    Generalization?.RemoveSpecialization( this );
                }
                else
                {
                    lastConcretes.Add( (item, autoImplementor) );
                    concreteBelow = true;
                }
            }
            return concreteBelow;
        }

    }
}
