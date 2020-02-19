using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Represents a service class/implementation.
    /// </summary>
    public class AutoServiceClassInfo : IStObjServiceClassDescriptor
    {
        HashSet<AutoServiceClassInfo> _ctorParmetersClosure;
        // Memorizes the EnsureCtorBinding call state.
        bool? _ctorBinding;
        // When not null, this contains the constructor parameters that must be singletons
        // for this service to be a singleton.
        List<ParameterInfo> _requiredParametersToBeSingletons;

        /// <summary>
        /// Constructor parameter info: either a <see cref="AutoServiceClassInfo"/>,
        /// <see cref="AutoServiceInterfaceInfo"/> or a enumeration of one of them.
        /// </summary>
        public class CtorParameter
        {
            /// <summary>
            /// The parameter info.
            /// </summary>
            public readonly ParameterInfo ParameterInfo;

            /// <summary>
            /// Not null if this parameter is a service class (ie. an implementation).
            /// </summary>
            public readonly AutoServiceClassInfo ServiceClass;

            /// <summary>
            /// Not null if this parameter is a service interface.
            /// </summary>
            public readonly AutoServiceInterfaceInfo ServiceInterface;

            /// <summary>
            /// Currently unused.
            /// </summary>
            public readonly AutoServiceClassInfo EnumeratedServiceClass;

            /// <summary>
            /// Currently unused.
            /// </summary>
            public readonly AutoServiceInterfaceInfo EnumeratedServiceInterface;

            /// <summary>
            /// Gets the (unwrapped) Type of this parameter.
            /// When <see cref="IsEnumerated"/> is true, this is the type of the enumerated object:
            /// for IReadOnlyList&lt;X&gt;, this is typeof(X).
            /// </summary>
            Type ParameterType { get; }

            /// <summary>
            /// Gets whether this is an enumerable of class or interface.
            /// </summary>
            public bool IsEnumerated { get; }

            /// <summary>
            /// Gets the zero-based position of the parameter in the parameter list.
            /// </summary>
            public int Position => ParameterInfo.Position;

            /// <summary>
            /// Gets the name of the parameter.
            /// </summary>
            public string Name => ParameterInfo.Name;

            internal CtorParameter(
                ParameterInfo p,
                AutoServiceClassInfo cS,
                AutoServiceInterfaceInfo iS,
                bool isEnumerable )
            {
                Debug.Assert( (cS != null) ^ (iS != null) );
                ParameterInfo = p;
                ParameterType = cS?.Type ?? iS.Type;
                if( isEnumerable )
                {
                    IsEnumerated = true;
                    EnumeratedServiceClass = cS;
                    EnumeratedServiceInterface = iS;
                }
                else
                {
                    ServiceClass = cS;
                    ServiceInterface = iS;
                }
            }

            /// <summary>
            /// Overridden to return a readable string.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString()
            {
                var typeName = ParameterInfo.Member.DeclaringType.Name;
                return $"{typeName}( {ParameterInfo.ParameterType.Name} {ParameterInfo.Name} {(ParameterInfo.HasDefaultValue ? "= null" : "")})";
            }
        }

        internal AutoServiceClassInfo(
            IActivityMonitor m,
            IServiceProvider serviceProvider,
            AutoServiceClassInfo parent,
            Type t,
            CKTypeCollector collector,
            bool isExcluded,
            CKTypeKind lifetime,
            RealObjectClassInfo objectInfo )
        {
            Debug.Assert( objectInfo == null || objectInfo.ServiceClass == null, "If we are the the asociated Service, we must be the only one." );

            if( objectInfo != null )
            {
                TypeInfo = objectInfo;
                objectInfo.ServiceClass = this;
            }
            else TypeInfo = new CKTypeInfo( m, parent?.TypeInfo, t, serviceProvider, isExcluded, this );

            Debug.Assert( parent == null || ReferenceEquals( TypeInfo.Generalization, parent.TypeInfo ), $"Gen={TypeInfo.Generalization}/Par={parent?.TypeInfo}" );
            Debug.Assert( (lifetime == (CKTypeKind.RealObject | CKTypeKind.AutoSingleton)) == TypeInfo is RealObjectClassInfo );


            // Forgets the RealObject flag.
            if( lifetime == (CKTypeKind.RealObject|CKTypeKind.AutoSingleton) )
            {
                lifetime = CKTypeKind.AutoSingleton;
                // See below.
                MustBeScopedLifetime = false;
            }
            Debug.Assert( lifetime == CKTypeKind.IsAutoService
                          || lifetime == CKTypeKind.AutoSingleton
                          || lifetime == CKTypeKind.AutoScoped );

            DeclaredLifetime = lifetime;
            // Let MustBeScopedLifetime be null for singleton here. Singleton impact is handled later
            // since it may have an impact on its ctor parameter type.
            // We have shortcut this process above for RealObject (since there is no ctor).
            if( lifetime == CKTypeKind.AutoScoped ) MustBeScopedLifetime = true;
            if( parent != null ) SpecializationDepth = parent.SpecializationDepth + 1;

            //if( IsExcluded ) return;
            //
            // AutoServiceAttribute is currently not used. This is to associate a service
            // to a StObj package and may be useful for Service Unification support.
            //var aC = t.GetCustomAttribute<AutoServiceAttribute>();
            //if( aC == null )
            //{
            //    m.Warn( $"Missing {nameof( AutoServiceAttribute )} on '{t.FullName}'." );
            //}
            //else
            //{
            //    ContainerType = aC.Container;
            //    if( ContainerType == null )
            //    {
            //        m.Info( $"{nameof( AutoServiceAttribute )} on '{t.FullName}' indicates no container." );
            //    }
            //}
        }

        /// <summary>
        /// Gets the <see cref="CKTypeInfo"/> that can be an autonomous one (specific to this service), or an
        /// existing RealObjectClassInfo if this service is implemented by a Real object (such service don't
        /// have to have a public constructor).
        /// </summary>
        public CKTypeInfo TypeInfo { get; }

        /// <summary>
        /// Get the <see cref="CKTypeInfo.Type"/>.
        /// </summary>
        public Type Type => TypeInfo.Type;

        /// <summary>
        /// Gets whether this service implementation is also a Real Object.
        /// </summary>
        public bool IsRealObject => TypeInfo is RealObjectClassInfo;

        /// <summary>
        /// Gets this Service class life time.
        /// This reflects the <see cref="IAutoService"/> or <see cref="ISingletonAutoService"/>
        /// vs. <see cref="IScopedAutoService"/> interface marker.
        /// This can never be <see cref="CKTypeKindExtension.IsNoneOrInvalid(CKTypeKind)"/> since
        /// in such cases, the AutoServiceClassInfo is not instanciated.
        /// </summary>
        public CKTypeKind DeclaredLifetime { get; }

        /// <summary>
        /// Gets whether this class must be <see cref="CKTypeKind.IsScoped"/> because of its dependencies.
        /// If its <see cref="DeclaredLifetime"/> is <see cref="CKTypeKind.IsSingleton"/> an error is detected
        /// either at the very beginning of the process based on the static parameter type information or at the
        /// end of the process when class and interface mappings are about to be resolved.
        /// </summary>
        public bool? MustBeScopedLifetime { get; private set; }

        /// <summary>
        /// Gets the generalization of this Service class, it is be null if no base class exists.
        /// This property is valid even if this type is excluded (however this AutoServiceClassInfo does not
        /// appear in generalization's <see cref="Specializations"/>).
        /// </summary>
        public AutoServiceClassInfo Generalization => TypeInfo?.Generalization?.ServiceClass;

        /// <summary>
        /// Gets the different specialized <see cref="AutoServiceClassInfo"/> that are not excluded.
        /// </summary>
        /// <returns>An enumerable of <see cref="AutoServiceClassInfo"/> that specialize this one.</returns>
        public IEnumerable<AutoServiceClassInfo> Specializations => TypeInfo.Specializations.Select( s => s.ServiceClass );

        /// <summary>
        /// Gets the most specialized concrete (or abstract but auto implementable) implementation.
        /// This is available only once <see cref="CKTypeCollector.GetResult"/> has been called.
        /// As long as <see cref="AutoServiceCollectorResult.HasFatalError"/> is false, this is never null
        /// since it can be this instance itself.
        /// </summary>
        public AutoServiceClassInfo MostSpecialized { get; private set; }

        /// <summary>
        /// Gets the supported service interfaces.
        /// This is not null only if <see cref="IsIncluded"/> is true (ie. this class is not excluded
        /// and is on a concrete path) and may be empty if there is no service interface (the
        /// implementation itself is marked with any <see cref="IScopedAutoService"/> marker).
        /// </summary>
        public IReadOnlyList<AutoServiceInterfaceInfo> Interfaces { get; private set; }

        /// <summary>
        /// Gets the container type to which this service is associated.
        /// This can be null (service is considered to reside in the final package) or
        /// if an error occured.
        /// For Service Chaining Resolution to be available (either to depend on or be used by others),
        /// services must be associated to one container.
        /// </summary>
        public Type ContainerType { get; }

        /// <summary>
        /// Gets the StObj container.
        /// </summary>
        public IStObjResult Container => ContainerItem;

        internal MutableItem ContainerItem { get; private set; }

        /// <summary>
        /// Gets the constructor. This may be null if any error occurred or
        /// if this service is implemented by an Real object.
        /// </summary>
        public ConstructorInfo ConstructorInfo { get; private set; }

        /// <summary>
        /// Gets the constructor parameters that we need to consider.
        /// Parameters that are not <see cref="IAutoService"/> do not appear here.
        /// This is empty even for service implemented by Real object as soon as <see cref="EnsureCtorBinding(IActivityMonitor, CKTypeCollector)"/>
        /// has been called.
        /// </summary>
        public IReadOnlyList<CtorParameter> ConstructorParameters { get; private set; }

        /// <summary>
        /// Gets the <see cref="ImplementableTypeInfo"/> if this <see cref="CKTypeInfo.Type"/>
        /// is abstract, null otherwise.
        /// </summary>
        public ImplementableTypeInfo ImplementableTypeInfo => TypeInfo.ImplementableTypeInfo;

        /// <summary>
        /// Gets the final type that must be used: it is <see cref="ImplementableTypeInfo.StubType"/>
        /// if this type is abstract otherwise it is the associated concrete <see cref="CKTypeInfo.Type"/>.
        /// </summary>
        public Type FinalType => TypeInfo.ImplementableTypeInfo?.StubType ?? Type;

        /// <summary>
        /// Gets the specialization depth from the first top AutoServiceClassInfo.
        /// This is not the same as <see cref="RealObjectClassInfo.SpecializationDepth"/> that
        /// is relative to <see cref="Object"/> type.
        /// </summary>
        public int SpecializationDepth { get; }

        /// <summary>
        /// Gets whether this class is on a concrete path: it is not excluded and is not abstract
        /// or has at least one concrete specialization.
        /// Only included classes eventually participate to the setup process.
        /// </summary>
        public bool IsIncluded => Interfaces != null;

        internal void FinalizeMostSpecializedAndCollectSubGraphs( List<AutoServiceClassInfo> subGraphCollector )
        {
            Debug.Assert( IsIncluded );
            if( MostSpecialized == null ) MostSpecialized = this;
            foreach( var s in Specializations )
            {
                if( s.MostSpecialized != MostSpecialized ) subGraphCollector.Add( s );
                s.FinalizeMostSpecializedAndCollectSubGraphs( subGraphCollector );
            }
        }

        /// <summary>
        /// This mimics the <see cref="RealObjectClassInfo.CreateMutableItemsPath"/> method
        /// to reproduce the exact same Type handling between Services and StObj (ignoring abstract tails
        /// for instance).
        /// This is simpler here since there is no split in type info (no MutableItem layer).
        /// </summary>
        internal bool InitializePath(
                        IActivityMonitor monitor,
                        CKTypeCollector collector,
                        AutoServiceClassInfo generalization,
                        IDynamicAssembly tempAssembly,
                        List<AutoServiceClassInfo> lastConcretes,
                        ref List<Type> abstractTails )
        {
            Debug.Assert( tempAssembly != null );
            Debug.Assert( !TypeInfo.IsExcluded );
            Debug.Assert( Interfaces == null );
            // Don't try to reuse the potential RealObjectInfo here: even if the TypeInfo is
            // a RealObject, let the regular code be executed (any abstract Specializations
            // have already been removed anyway) so we'll correctly initialize the Interfaces for
            // all the chain.
            bool isConcretePath = false;
            foreach( AutoServiceClassInfo c in Specializations )
            {
                Debug.Assert( !c.TypeInfo.IsExcluded );
                isConcretePath |= c.InitializePath( monitor, collector, this, tempAssembly, lastConcretes, ref abstractTails );
            }
            if( !isConcretePath )
            {
                if( Type.IsAbstract
                    && TypeInfo.InitializeImplementableTypeInfo( monitor, tempAssembly ) == null )
                {
                    if( abstractTails == null ) abstractTails = new List<Type>();
                    abstractTails.Add( Type );
                    TypeInfo.Generalization?.RemoveSpecialization( TypeInfo );
                }
                else
                {
                    isConcretePath = true;
                    lastConcretes.Add( this );
                }
            }
            if( isConcretePath )
            {
                // Only if this class IsIncluded: assigns the set of interfaces.
                // This way only interfaces that are actually used are registered in the collector.
                // An unused Auto Service interface (ie. that has no implementation in the context)
                // is like any other interface.
                Interfaces = collector.RegisterServiceInterfaces( Type.GetInterfaces() ).ToArray();
            }
            return isConcretePath;
        }

        /// <summary>
        /// Sets one of the leaves of this class to be the most specialized one from this
        /// instance potentially up to the leaf (and handles container binding at the same time).
        /// At least one assignment (the one of this instance) is necessarily done.
        /// Trailing path may have already been resolved to this or to another specialization:
        /// classes that are already assigned are skipped.
        /// This must obviously be called bottom-up the inheritance chain.
        /// </summary>
        internal bool SetMostSpecialized(
            IActivityMonitor monitor,
            StObjObjectEngineMap engineMap,
            AutoServiceClassInfo mostSpecialized )
        {
            Debug.Assert( IsIncluded );
            Debug.Assert( MostSpecialized == null );
            Debug.Assert( mostSpecialized != null && mostSpecialized.IsIncluded );
            Debug.Assert( !mostSpecialized.TypeInfo.IsSpecialized );

            bool success = true;
#if DEBUG
            bool atLeastOneAssignment = false;
#endif
            var child = mostSpecialized;
            do
            {
                if( child.MostSpecialized == null )
                {
                    // Child's most specialized class has not been assigned yet: its generalization
                    // has not been assigned yet.
                    Debug.Assert( child.Generalization?.MostSpecialized == null );
                    child.MostSpecialized = mostSpecialized;
#if DEBUG
                    atLeastOneAssignment = true;
#endif
                    if( child.ContainerType != null )
                    {
                        if( (child.ContainerItem = engineMap.ToHighestImpl( child.ContainerType )) == null )
                        {
                            monitor.Error( $"Unable to resolve container '{child.ContainerType.FullName}' for service '{child.Type.FullName}' to a StObj." );
                            success = false;
                        }
                    }
                }
            }
            while( (child = child.Generalization) != Generalization );
#if DEBUG
            Debug.Assert( atLeastOneAssignment );
#endif
            return success;
        }

        /// <summary>
        /// Gets the parameters closure (including "Inheritance Constructor Parameters rule" and
        /// external intermediate classes).
        /// </summary>
        public HashSet<AutoServiceClassInfo> ComputedCtorParametersClassClosure
        {
            get
            {
                Debug.Assert( _ctorParmetersClosure != null && _ctorBinding == true );
                return _ctorParmetersClosure;
            }
        }

        Type IStObjServiceClassDescriptor.ClassType => FinalType;

        bool IStObjServiceClassDescriptor.IsScoped => MustBeScopedLifetime.Value;

        /// <summary>
        /// Ensures that the final lifetime is computed: <see cref="MustBeScopedLifetime"/> will not be null
        /// once called.
        /// Returns the MustBeScopedLifetime (true if this Service implementation must be scoped and false for singleton).
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="typeKindDetector">The type detector (used to check singleton life times and promote mere IAutoService to singletons).</param>
        /// <param name="success">Success reference token.</param>
        /// <returns>True for scoped, false for singleton.</returns>
        internal bool GetFinalMustBeScopedLifetime( IActivityMonitor m, CKTypeKindDetector typeKindDetector, ref bool success )
        {
            if( !MustBeScopedLifetime.HasValue )
            {
                Debug.Assert( (DeclaredLifetime & CKTypeKind.IsAutoService) != 0 );
                foreach( var p in ConstructorParameters )
                {
                    var c = p.ServiceClass?.MostSpecialized ?? p.ServiceInterface?.FinalResolved;
                    if( c != null )
                    {
                        if( c.GetFinalMustBeScopedLifetime( m, typeKindDetector, ref success ) )
                        {
                            if( DeclaredLifetime == CKTypeKind.AutoSingleton )
                            {
                                m.Error( $"Lifetime error: Type '{Type}' is {nameof( ISingletonAutoService )} but parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' in constructor is Scoped." );
                                success = false;
                            }
                            if( !MustBeScopedLifetime.HasValue )
                            {
                                m.Info( $"Type '{Type}' must be Scoped since parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' in constructor is Scoped." );
                            }
                            MustBeScopedLifetime = true;
                        }
                    }
                }
                if( !MustBeScopedLifetime.HasValue )
                {
                    if( _requiredParametersToBeSingletons != null )
                    {
                        Debug.Assert( DeclaredLifetime == CKTypeKind.IsAutoService );
                        foreach( var external in _requiredParametersToBeSingletons )
                        {
                            if( !typeKindDetector.IsSingleton( external.ParameterType ) )
                            {
                                m.Info( $"Type '{Type.Name}' must be Scoped since parameter '{external.Name}' of type '{external.ParameterType.Name}' in constructor is not a Singleton." );
                                MustBeScopedLifetime = true;
                                break;
                            }
                        }
                    }
                    if( !MustBeScopedLifetime.HasValue )
                    {
                        MustBeScopedLifetime = false;
                        if( DeclaredLifetime != CKTypeKind.AutoSingleton )
                        {
                            m.Info( $"Nothing prevents the class '{Type}' to be a Singleton: this is the most efficient choice." );
                            success &= typeKindDetector.PromoteToSingleton( m, Type ) != null;
                        }
                    }
                }
            }
            return MustBeScopedLifetime.Value;
        }

        internal HashSet<AutoServiceClassInfo> GetCtorParametersClassClosure(
            IActivityMonitor m,
            CKTypeCollector collector,
            ref bool initializationError )
        {
            if( _ctorParmetersClosure == null )
            {
                // Parameters of base classes are by design added to parameters of this instance.
                // This ensure the "Inheritance Constructor Parameters rule", even if parameters are
                // not exposed from the inherited constructor (and base parameters are direclty new'ed).
                _ctorParmetersClosure = new HashSet<AutoServiceClassInfo>();

                bool AddCoveredParameters( IEnumerable<AutoServiceClassInfo> classes )
                {
                    bool initError = false;
                    foreach( var cS in classes )
                    {
                        AutoServiceClassInfo c = cS;
                        do { _ctorParmetersClosure.Add( c ); } while( (c = c.Generalization) != null );
                        var cParams = cS.GetCtorParametersClassClosure( m, collector, ref initError );
                        _ctorParmetersClosure.UnionWith( cParams );
                    }
                    return initError;
                }

                if( IsRealObject )
                {
                    // Calls EnsureCtorBinding (even if it is useless) for coherency: it is up
                    // to this function to handle the IsRealObject case.
                    initializationError |= !EnsureCtorBinding( m, collector );
                    // Handles the ReplaceAutoServiceAttribute that must be used by RealObject service implementation.
                    if( !initializationError )
                    {
                        var replacedTargets = GetReplacedTargetsFromReplaceServiceAttribute( m, collector );
                        initializationError |= AddCoveredParameters( replacedTargets );
                    }
                }
                else
                {
                    if( Generalization != null )
                    {
                        _ctorParmetersClosure.AddRange( Generalization.GetCtorParametersClassClosure( m, collector, ref initializationError ) );
                    }
                    if( !(initializationError |= !EnsureCtorBinding( m, collector )) )
                    {
                        var replacedTargets = GetReplacedTargetsFromReplaceServiceAttribute( m, collector );
                        initializationError |= AddCoveredParameters( ConstructorParameters.Select( p => p.ServiceClass )
                                                                       .Where( p => p != null )
                                                                       .Concat( replacedTargets ) );
                    }
                }

                // Checks here for (stupid) cyclic class dependencies.
                if( _ctorParmetersClosure.Contains( this ) )
                {
                    m.Error( $"Cyclic constructor dependency detected: {Type} eventually depends on itself." );
                    initializationError = true;
                }
            }
            return _ctorParmetersClosure;
        }

        IEnumerable<AutoServiceClassInfo> GetReplacedTargetsFromReplaceServiceAttribute( IActivityMonitor m, CKTypeCollector collector )
        {
            foreach( var p in Type.GetCustomAttributesData()
                                  .Where( a => a.AttributeType.Name == nameof( ReplaceAutoServiceAttribute ) )
                                  .SelectMany( a => a.ConstructorArguments ) )
            {
                Type replaced;
                if( p.Value is string s )
                {
                    replaced = SimpleTypeFinder.WeakResolver( s, false );
                    if( replaced == null )
                    {
                        m.Warn( $"[ReplaceAutoService] on type '{Type}': the assembly qualified name '{s}' cannot be resolved. It is ignored." );
                        continue;
                    }
                }
                else
                {
                    replaced = p.Value as Type;
                    if( replaced == null )
                    {
                        m.Warn( $"[ReplaceAutoService] on type '{Type}': the parameter '{p.Value}' is not a Type. It is ignored." );
                        continue;
                    }
                }
                var target = collector.FindServiceClassInfo( replaced );
                if( target == null )
                {
                    m.Warn( $"[ReplaceAutoService({replaced.Name})] on type '{Type}': the Type to replace is not an Auto Service class implementation. It is ignored." );
                }
                else
                {
                    yield return target;
                }
            }
        }

        internal bool EnsureCtorBinding( IActivityMonitor m, CKTypeCollector collector )
        {
            Debug.Assert( IsIncluded );
            if( _ctorBinding.HasValue ) return _ctorBinding.Value;
            if( IsRealObject )
            {
                ConstructorParameters = Array.Empty<CtorParameter>();
                _ctorBinding = true;
                return true;
            }
            bool success = false;
            var ctors = Type.GetConstructors();
            if( ctors.Length == 0 ) m.Error( $"No public constructor found for '{Type.FullName}'." );
            else if( ctors.Length > 1 ) m.Error( $"Multiple public constructors found for '{Type.FullName}'. Only one must exist." );
            else
            {
                success = Generalization?.EnsureCtorBinding( m, collector ) ?? true;
                var parameters = ctors[0].GetParameters();
                var mParameters = new List<CtorParameter>();
                foreach( var p in parameters )
                {
                    var param = CreateCtorParameter( m, collector, p );
                    success &= param.Success;
                    if( param.Class != null || param.Interface != null )
                    {
                        mParameters.Add( new CtorParameter( p, param.Class, param.Interface, param.IsEnumerable ) );
                    }
                    // We check here the Singleton to Scoped dependency error at the Type level.
                    // This must be done here since CtorParameters are not created for types that are external (those
                    // are considered as Scoped) or for ambient interfaces that have no implementation classes.
                    // If the parameter knwn to be singleton, we have nothing to do.
                    if( param.Lifetime == CKTypeKind.None || (param.Lifetime & CKTypeKind.IsScoped) != 0 )
                    {
                        // Note: if this DeclaredLifetime is AutoScopedd nothing is done here: as a
                        //       scoped service there is nothing to say about its constructor parameters' lifetime.
                        if( DeclaredLifetime == CKTypeKind.AutoSingleton )
                        {
                            if( param.Lifetime == CKTypeKind.None )
                            {
                                m.Warn( $"Type '{p.Member.DeclaringType}' is marked with {nameof( ISingletonAutoService )}. Parameter '{p.Name}' of type '{p.ParameterType.Name}' that has no associated lifetime will be considered as a Singleton." );
                                if( collector.AmbientKindDetector.DefineAsSingletonReference( m, p.ParameterType ) == null )
                                {
                                    success = false;
                                }
                            }
                            else
                            {
                                MustBeScopedLifetime = true;
                                string paramReason;
                                if( param.Lifetime == CKTypeKind.AutoScoped )
                                {
                                    paramReason = $"is marked with {nameof( IScopedAutoService )}";
                                }
                                else
                                {
                                    Debug.Assert( param.Lifetime == CKTypeKind.IsScoped );
                                    paramReason = $"is registered as an external scoped service";
                                }
                                m.Error( $"Lifetime error: Type '{p.Member.DeclaringType}' is marked with {nameof( ISingletonAutoService )}  but parameter '{p.Name}' of type '{p.ParameterType.Name}' {paramReason}." );
                                success = false;
                            }
                        }
                        else if( DeclaredLifetime == CKTypeKind.IsAutoService )
                        {
                            if( (param.Lifetime & CKTypeKind.IsScoped) != 0 )
                            {
                                m.Info( $"{nameof( IAutoService )} '{p.Member.DeclaringType}' is Scoped because of parameter '{p.Name}' of type '{p.ParameterType.Name}'." );
                                MustBeScopedLifetime = true;
                            }
                            else
                            {
                                Debug.Assert( param.Lifetime == CKTypeKind.None );
                                if( _requiredParametersToBeSingletons == null ) _requiredParametersToBeSingletons = new List<ParameterInfo>();
                                _requiredParametersToBeSingletons.Add( p );
                            }
                        }
                    }
                    // Temporary: Enumeration is not implemented yet.
                    if( success && param.IsEnumerable )
                    {
                        m.Error( $"IEnumerable<T> or IReadOnlyList<T> where T is marked with IScopedAutoService or ISingletonAutoService is not supported yet: '{Type.FullName}' constructor cannot be handled." );
                        success = false;
                    }
                }
                ConstructorParameters = mParameters;
                ConstructorInfo = ctors[0];
            }
            _ctorBinding = success;
            return success;
        }

        readonly struct CtorParameterData
        {
            public readonly bool Success;
            public readonly AutoServiceClassInfo Class;
            public readonly AutoServiceInterfaceInfo Interface;
            public readonly bool IsEnumerable;
            public readonly CKTypeKind Lifetime;

            public CtorParameterData( bool success, AutoServiceClassInfo c, AutoServiceInterfaceInfo i, bool isEnumerable, CKTypeKind lt )
            {
                Success = success;
                Class = c;
                Interface = i;
                IsEnumerable = isEnumerable;
                Lifetime = lt;
            }
        }

        CtorParameterData CreateCtorParameter(
            IActivityMonitor m,
            CKTypeCollector collector,
            ParameterInfo p )
        {
            var tParam = p.ParameterType;
            bool isEnumerable = false;
            if( tParam.IsGenericType )
            {
                var tGen = tParam.GetGenericTypeDefinition();
                if( tGen == typeof( IEnumerable<> )
                    || tGen == typeof( IReadOnlyCollection<> )
                    || tGen == typeof( IReadOnlyList<> ) )
                {
                    isEnumerable = true;
                    tParam = tParam.GetGenericArguments()[0];
                }
                else 
                {
                    var genLifetime = collector.AmbientKindDetector.GetKind( m, tGen );
                    if( genLifetime != CKTypeKind.None )
                    {
                        return new CtorParameterData( true, null, null, false, genLifetime );
                    }
                }
            }
            // We only consider I(Scoped/Singleton)AutoService marked type parameters.
            var lifetime = collector.AmbientKindDetector.GetKind( m, tParam );
            if( (lifetime&CKTypeKind.IsAutoService) == 0 )
            {
                return new CtorParameterData( true, null, null, false, lifetime );
            }
            var conflictMsg = lifetime.GetCKTypeKindCombinationError( tParam.IsClass ); 
            if( conflictMsg != null )
            {
                m.Error( $"Type '{tParam.FullName}' for parameter '{p.Name}' in '{p.Member.DeclaringType.FullName}' constructor: {conflictMsg}" );
                return new CtorParameterData( false, null, null, false, lifetime );
            }

            if( tParam.IsClass )
            {
                var sClass = collector.FindServiceClassInfo( tParam );
                if( sClass == null )
                {
                    m.Error( $"Unable to resolve '{tParam.FullName}' service type for parameter '{p.Name}' in '{p.Member.DeclaringType.FullName}' constructor." );
                    return new CtorParameterData( false, null, null, isEnumerable, lifetime );
                }
                if( !sClass.IsIncluded )
                {
                    var reason = sClass.TypeInfo.IsExcluded
                                    ? "excluded from registration"
                                    : "abstract (and can not be concretized)";
                    var prefix = $"Service type '{tParam}' is {reason}. Parameter '{p.Name}' in '{p.Member.DeclaringType.FullName}' constructor ";
                    if( !p.HasDefaultValue )
                    {
                        m.Error( prefix + "can not be resolved." );
                        return new CtorParameterData( false, null, null, isEnumerable, lifetime );
                    }
                    m.Info( prefix + "will use its default value." );
                    sClass = null;
                }
                else if( TypeInfo.IsAssignableFrom( sClass.TypeInfo ) )
                {
                    var prefix = $"Parameter '{p.Name}' in '{p.Member.DeclaringType.FullName}' constructor ";
                    m.Error( prefix + "cannot be this class or one of its specializations." );
                    return new CtorParameterData( false, null, null, isEnumerable, lifetime );
                }
                else if( sClass.TypeInfo.IsAssignableFrom( TypeInfo ) )
                {
                    var prefix = $"Parameter '{p.Name}' in '{p.Member.DeclaringType.FullName}' constructor ";
                    m.Error( prefix + "cannot be one of its base class." );
                    return new CtorParameterData( false, null, null, isEnumerable, lifetime );
                }
                return new CtorParameterData( true, sClass, null, isEnumerable, lifetime );
            }
            return new CtorParameterData( true, null, collector.FindServiceInterfaceInfo( tParam ), isEnumerable, lifetime );
        }


        /// <summary>
        /// Overridden to return the <see cref="CKTypeInfo.ToString()"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => TypeInfo.ToString();

    }
}
