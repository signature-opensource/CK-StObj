using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Represents a service class/implementation.
    /// </summary>
    public sealed class AutoServiceClassInfo : IStObjServiceFinalSimpleMapping
    {
        HashSet<AutoServiceClassInfo>? _ctorParmetersClosure;
        AutoServiceKind? _serviceKind;
        // Memorizes the EnsureCtorBinding call state.
        bool? _ctorBinding;

        /// <summary>
        /// Constructor parameter info: either a <see cref="AutoServiceClassInfo"/>,
        /// <see cref="AutoServiceInterfaceInfo"/>, a enumeration of one of them or a
        /// regular (no IAutoService) parameter.
        /// </summary>
        public sealed class CtorParameter
        {
            /// <summary>
            /// The parameter info.
            /// This is never null.
            /// </summary>
            public ParameterInfo ParameterInfo { get; }

            /// <summary>
            /// Not null if this parameter is a service class (ie. a IAutoService implementation).
            /// </summary>
            public AutoServiceClassInfo? ServiceClass { get; }

            /// <summary>
            /// Not null if this parameter is a service interface (a <see cref="IAutoService"/>).
            /// </summary>
            public AutoServiceInterfaceInfo? ServiceInterface { get; }

            /// <summary>
            /// Gets whether this parameter is an <see cref="IAutoService"/>.
            /// </summary>
            public bool IsAutoService => ServiceClass != null || ServiceInterface != null;

            /// <summary>
            /// Gets the final ServiceClassInfo: either the <see cref="AutoServiceClassInfo.MostSpecialized"/> or
            /// the <see cref="AutoServiceInterfaceInfo.FinalResolved"/> from <see cref="ServiceClass"/> or <see cref="ServiceInterface"/>.
            /// Null if <see cref="IsAutoService"/> is false.
            /// Must be called once the mapping has been fully resolved at the interface level.
            /// </summary>
            public AutoServiceClassInfo? FinalServiceClass => ServiceClass?.MostSpecialized ?? ServiceInterface?.FinalResolved;

            /// <summary>
            /// Gets the (unwrapped) Type of this parameter.
            /// When <see cref="IsEnumerable"/> is true, this is the type of the enumerated object:
            /// for IEnumerable&lt;X&gt;, this is typeof(X) where X is a a <see cref="IsMultipleAttribute"/> interface,
            /// or a value type.
            /// Otherwise, it is simply the parameter type: this is never null.
            /// </summary>
            public Type ParameterType { get; }

            /// <summary>
            /// Gets whether this is an enumerable of a type that cannot be a class, a <see cref="ServiceClass"/>
            /// or a <see cref="ServiceInterface"/> (they are both null if this is true).
            /// <para>
            /// Only IEnumerable of value types xor [IsMultiple] interfaces are accepted.
            /// </para>
            /// </summary>
            public bool IsEnumerable { get; }

            /// <summary>
            /// Gets the zero-based position of the parameter in the parameter list.
            /// </summary>
            public int Position => ParameterInfo.Position;

            /// <summary>
            /// Gets the name of the parameter.
            /// </summary>
            public string Name => ParameterInfo.Name!;

            internal CtorParameter( ParameterInfo p,
                                    AutoServiceClassInfo? cS,
                                    AutoServiceInterfaceInfo? iS )
            {
                Debug.Assert( (cS != null) ^ (iS != null) );
                ParameterInfo = p;
                ParameterType = cS?.ClassType ?? iS!.Type;
                ServiceClass = cS;
                ServiceInterface = iS;
            }

            internal CtorParameter( ParameterInfo p, bool isEnumerable, Type parameterType )
            {
                ParameterInfo = p;
                IsEnumerable = isEnumerable;
                ParameterType = parameterType;
                Debug.Assert( IsAutoService == false );
            }

            /// <summary>
            /// Overridden to return a readable string.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString()
            {
                var typeName = ParameterInfo.Member.DeclaringType!.Name;
                return $"{typeName}( {ParameterInfo.ParameterType.Name} {ParameterInfo.Name} {(ParameterInfo.HasDefaultValue ? "= null" : "")})";
            }
        }

        internal AutoServiceClassInfo( IActivityMonitor m,
                                       IServiceProvider serviceProvider,
                                       AutoServiceClassInfo? parent,
                                       Type t,
                                       bool isExcluded,
                                       RealObjectClassInfo? realObjectInfo,
                                       Action<Type> alsoRegister )
        {
            Debug.Assert( realObjectInfo == null || realObjectInfo.ServiceClass == null, "If we are the associated Service, we must be the only one." );
            if( realObjectInfo != null )
            {
                Debug.Assert( realObjectInfo.Type == t );
                TypeInfo = realObjectInfo;
                realObjectInfo.ServiceClass = this;
            }
            else
            {
                TypeInfo = new CKTypeInfo( m, parent?.TypeInfo, t, serviceProvider, isExcluded, this, alsoRegister );
            }
            Debug.Assert( parent == null || ReferenceEquals( TypeInfo.Generalization, parent.TypeInfo ), $"Gen={TypeInfo.Generalization}/Par={parent?.TypeInfo}" );

            if( parent != null ) SpecializationDepth = parent.SpecializationDepth + 1;
            MappingListIndex = -1;
        }

        /// <summary>
        /// Gets the <see cref="CKTypeInfo"/> that can be an autonomous one (specific to this service), or an
        /// existing RealObjectClassInfo if this service is implemented by a Real object (such service don't
        /// need to have a public constructor).
        /// </summary>
        public CKTypeInfo TypeInfo { get; }

        /// <summary>
        /// Get the <see cref="CKTypeInfo.Type"/>.
        /// </summary>
        public Type ClassType => TypeInfo.Type;

        /// <summary>
        /// Gets whether this service implementation is also a Real Object.
        /// </summary>
        public bool IsRealObject => TypeInfo is RealObjectClassInfo;

        /// <summary>
        /// Gets the final service kind.
        /// <see cref="AutoServiceKind.IsSingleton"/> and <see cref="AutoServiceKind.IsScoped"/> are propagated using the lifetime rules.
        /// </summary>
        public AutoServiceKind? FinalTypeKind  => _serviceKind;

        /// <summary>
        /// Gets the multiple interfaces that are marked with <see cref="CKTypeKind.IsMultipleService"/>
        /// and that must be mapped to this <see cref="FinalType"/>.
        /// </summary>
        public IReadOnlyCollection<Type> MultipleMappings => TypeInfo.MultipleMappingTypes;

        /// <summary>
        /// Gets the unique types that must be mapped to this <see cref="FinalType"/> and only to this one.
        /// </summary>
        public IReadOnlyCollection<Type> UniqueMappings => TypeInfo.UniqueMappingTypes;

        /// <summary>
        /// Gets the generalization of this Service class, it is null if no base class exists.
        /// This property is valid even if this type is excluded (however this AutoServiceClassInfo does not
        /// appear in generalization's <see cref="Specializations"/>).
        /// </summary>
        public AutoServiceClassInfo? Generalization => TypeInfo?.Generalization?.ServiceClass;

        /// <summary>
        /// Gets the different specialized <see cref="AutoServiceClassInfo"/> that are not excluded.
        /// </summary>
        /// <returns>An enumerable of <see cref="AutoServiceClassInfo"/> that specialize this one.</returns>
        public IEnumerable<AutoServiceClassInfo> Specializations => TypeInfo.Specializations.Select( s => s.ServiceClass! );

        /// <summary>
        /// Gets all the <see cref="Specializations"/> recursively.
        /// </summary>
        public IEnumerable<AutoServiceClassInfo> AllSpecializations
        {
            get
            {
                foreach( var s in Specializations )
                {
                    yield return s;
                    foreach( var c in s.AllSpecializations ) yield return c;
                }
            }
        }

        /// <summary>
        /// Gets the most specialized concrete (or abstract but auto implementable) implementation.
        /// This is available only once <see cref="CKTypeCollector.GetResult"/> has been called.
        /// As long as <see cref="AutoServiceCollectorResult.HasFatalError"/> is false, this is never null
        /// since it can be this instance itself.
        /// </summary>
        public AutoServiceClassInfo? MostSpecialized { get; private set; }

        /// <summary>
        /// Gets the supported service interfaces.
        /// This is not null only if <see cref="IsIncluded"/> is true (i.e. this class is not excluded
        /// and is on a concrete path) and may be empty if there is no service interface (the
        /// implementation itself is marked with any <see cref="IScopedAutoService"/> marker).
        /// </summary>
        public IReadOnlyList<AutoServiceInterfaceInfo>? Interfaces { get; private set; }

        /// <summary>
        /// Gets the StObj container.
        /// </summary>
        public IStObjResult? Container => ContainerItem;

        internal MutableItem? ContainerItem { get; private set; }

        /// <summary>
        /// Gets all the constructor parameters' <see cref="ParameterInfo"/> wrapped in <see cref="CtorParameter"/>.
        /// This is not null (even for service implemented by Real object) as soon as the EnsureCtorBinding internal method has been called.
        /// </summary>
        public IReadOnlyList<CtorParameter>? ConstructorParameters { get; private set; }

        /// <summary>
        /// Gets the <see cref="ImplementableTypeInfo"/> if this <see cref="CKTypeInfo.Type"/>
        /// is abstract, null otherwise.
        /// </summary>
        public ImplementableTypeInfo? ImplementableTypeInfo => TypeInfo.ImplementableTypeInfo;

        /// <summary>
        /// Gets the final type that must be used: it is <see cref="ImplementableTypeInfo.StubType"/>
        /// if this type is abstract otherwise it is the associated concrete <see cref="CKTypeInfo.Type"/>.
        /// </summary>
        public Type FinalType => TypeInfo.ImplementableTypeInfo?.StubType ?? TypeInfo.Type;

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

        /// <summary>
        /// This mimics the <see cref="RealObjectClassInfo.CreateMutableItemsPath"/> method
        /// to reproduce the exact same Type handling between Services and StObj (ignoring abstract tails
        /// for instance).
        /// This is simpler here since there is no split in type info (no MutableItem layer).
        /// </summary>
        internal bool InitializePath( IActivityMonitor monitor,
                                      CKTypeCollector collector,
                                      IDynamicAssembly tempAssembly,
                                      List<AutoServiceClassInfo> lastConcretes,
                                      ref List<Type>? abstractTails )
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
                isConcretePath |= c.InitializePath( monitor, collector, tempAssembly, lastConcretes, ref abstractTails );
            }
            if( !isConcretePath )
            {
                if( ClassType.IsAbstract
                    && TypeInfo.InitializeImplementableTypeInfo( monitor, tempAssembly ) == null )
                {
                    if( abstractTails == null ) abstractTails = new List<Type>();
                    abstractTails.Add( ClassType );
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
                // An unused Auto Service interface (i.e. that has no implementation in the context)
                // is like any other interface.
                // Note that if this is a Real Object, multiple mappings are already handled by the real object.

                Interfaces = collector.RegisterServiceInterfaces( monitor,
                                                                    TypeInfo.Interfaces,
                                                                    IsRealObject
                                                                    ? null
                                                                    : !TypeInfo.IsSpecialized
                                                                        ? TypeInfo.AddMultipleMapping
                                                                        : null ).ToArray();
            }
            return isConcretePath;
        }

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
        /// Sets one of the leaves of this class to be the most specialized one from this
        /// instance potentially up to the leaf.
        /// At least one assignment (the one of this instance) is necessarily done.
        /// Trailing path may have already been resolved to this or to another specialization:
        /// classes that are already assigned are skipped.
        /// This must obviously be called bottom-up the inheritance chain.
        /// </summary>
        internal bool SetMostSpecialized( IActivityMonitor monitor,
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
            AutoServiceClassInfo? child = mostSpecialized;
            do
            {
                Debug.Assert( child != null );
                if( child.MostSpecialized == null )
                {
                    // Child's most specialized class has not been assigned yet: its generalization
                    // has not been assigned yet.
                    Debug.Assert( child.Generalization?.MostSpecialized == null );
                    child.MostSpecialized = mostSpecialized;
#if DEBUG
                    atLeastOneAssignment = true;
#endif
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

        bool IStObjFinalClass.IsScoped => (FinalTypeKind!.Value & AutoServiceKind.IsScoped) != 0;

        AutoServiceKind IStObjServiceClassDescriptor.AutoServiceKind => FinalTypeKind!.Value;

        internal AutoServiceKind ComputeFinalTypeKind( IActivityMonitor m, IAutoServiceKindComputeFacade kindComputeFacade, Stack<AutoServiceClassInfo> path, ref bool success )
        {
            Debug.Assert( !TypeInfo.IsSpecialized, "This is called only on leaf, most specialized, class." );
            if( !_serviceKind.HasValue )
            {
                var initial = kindComputeFacade.KindDetector.GetValidKind( m, ClassType ).ToAutoServiceKind();
                var final = initial;
                Debug.Assert( ConstructorParameters != null );
                using( m.OpenTrace( $"Computing {ClassType}'s final type based on {ConstructorParameters.Count} parameter(s). Initially '{initial}'." ) )
                {
                    if( path.Contains( this ) )
                    {
                        m.Error( $"Service class dependency cycle detected: '{path.Select( c => c.ClassType.Name ).Concatenate( "' -> '" )}'." );
                        success = false;
                    }
                    else
                    {
                        path.Push( this );
                        Debug.Assert( ConstructorParameters != null );
                        foreach( var p in ConstructorParameters )
                        {
                            AutoServiceClassInfo? pC = null;
                            AutoServiceKind kP;
                            string paramTypeName;
                            CKTypeCollector.MultipleImpl? multiple;
                            if( p.IsEnumerable && (multiple = kindComputeFacade.GetMultipleInterfaceDescriptor( p.ParameterType )) != null )
                            {
                                kP = multiple.ComputeFinalTypeKind( m, kindComputeFacade, ref success );
                                paramTypeName = $"IEnumerable<{p.ParameterType.Name}>";
                            }
                            else if( p.IsAutoService )
                            {
                                Throw.DebugAssert( "A [IsMultiple] interface cancels its IAutoService trait (if any).", !p.IsEnumerable );
                                pC = p.FinalServiceClass;
                                Debug.Assert( pC != null );
                                kP = pC.ComputeFinalTypeKind( m, kindComputeFacade, path, ref success );
                                paramTypeName = p.ParameterType.Name;
                            }
                            else
                            {
                                kP = kindComputeFacade.KindDetector.GetValidKind( m, p.ParameterType ).ToAutoServiceKind();
                                paramTypeName = p.ParameterType.Name;
                                if( (kP & (AutoServiceKind.IsSingleton | AutoServiceKind.IsScoped)) == 0 )
                                {
                                    if( p.ParameterType.IsValueType || p.ParameterType == typeof( string ) )
                                    {
                                        if( !p.ParameterInfo.HasDefaultValue )
                                        {
                                            m.Warn( $"Parameter '{p.Name}' of type '{paramTypeName}' is a {(p.ParameterType.IsValueType ? "value type" : "string")} without default value. This requires an explicit registration in the DI container." );
                                        }
                                        // Value type parameter with default value. Skip it.
                                        continue;
                                    }
                                    else
                                    {
                                        m.Warn( $"Parameter '{p.Name}' is an external type '{paramTypeName}': unknown lifetime is considered Scoped. Type set to {kP | AutoServiceKind.IsScoped}." );
                                        var update = kindComputeFacade.KindDetector.RestrictToScoped( m, p.ParameterType );
                                        if( update.HasValue ) kP = update.Value.ToAutoServiceKind();
                                        else success = false;
                                    }
                                }
                            }
                            if( success )
                            {
                                // Handling lifetime.
                                Debug.Assert( (kP & (AutoServiceKind.IsSingleton | AutoServiceKind.IsScoped)) != 0 );
                                if( (kP & AutoServiceKind.IsScoped) != 0 )
                                {
                                    if( (final & AutoServiceKind.IsSingleton) != 0 )
                                    {
                                        m.Error( $"Lifetime error: Type '{ClassType}' is marked as IsSingleton but parameter '{p.Name}' of type '{paramTypeName}' in constructor is Scoped." );
                                        success = false;
                                    }
                                    else if( (final & AutoServiceKind.IsScoped) == 0 )
                                    {
                                        m.Info( $"Type '{ClassType}' must be Scoped since parameter '{p.Name}' of type '{paramTypeName}' in constructor is Scoped." );
                                        final |= AutoServiceKind.IsScoped;
                                    }
                                }
                            }
                        }
                        path.Pop();

                        if( success )
                        {
                            // Conclude about lifetime.
                            if( (final & (AutoServiceKind.IsScoped | AutoServiceKind.IsSingleton)) == 0 )
                            {
                                m.Info( $"Nothing prevents the class '{ClassType}' to be a Singleton: this is the most efficient choice." );
                                final |= AutoServiceKind.IsSingleton;
                            }
                        }
                    }
                    if( final != initial ) m.CloseGroup( $"Final: {final}" );
                    _serviceKind = final;
                }
            }
            return _serviceKind.Value;
        }

        /// <summary>
        /// This is called on the Service leaf and recursively on the Generalization.
        /// </summary>
        internal HashSet<AutoServiceClassInfo> GetCtorParametersClassClosure( IActivityMonitor m,
                                                                              CKTypeCollector collector,
                                                                              ref bool initializationError )
        {
            if( _ctorParmetersClosure == null )
            {
                // Parameters of base classes are by design added to parameters of this instance.
                // This ensure the "Inheritance Constructor Parameters rule", even if parameters are
                // not exposed from the inherited constructor (and base parameters are directly new'ed).
                _ctorParmetersClosure = new HashSet<AutoServiceClassInfo>();

                bool AddCoveredParameters( IEnumerable<AutoServiceClassInfo> classes )
                {
                    bool initError = false;
                    foreach( var cS in classes )
                    {
                        AutoServiceClassInfo? c = cS;
                        do { _ctorParmetersClosure.Add( c ); } while( (c = c.Generalization) != null );
                        var cParams = cS.GetCtorParametersClassClosure( m, collector, ref initError );
                        _ctorParmetersClosure.UnionWith( cParams );
                    }
                    return initError;
                }

                if( IsRealObject )
                {
                    // This what EnsureCtorBinding would have done.
                    ConstructorParameters = Array.Empty<CtorParameter>();
                    _ctorBinding = true;
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
                        Debug.Assert( ConstructorParameters != null );
                        initializationError |= AddCoveredParameters( ConstructorParameters.Select( p => p.ServiceClass )
                                                                       .Where( p => p != null )
                                                                       .Select( p => p! )
                                                                       .Concat( replacedTargets ) );
                    }
                }

                // Checks here for (stupid) cyclic class dependencies.
                if( _ctorParmetersClosure.Contains( this ) )
                {
                    m.Error( $"Cyclic constructor dependency detected: {ClassType} eventually depends on itself." );
                    initializationError = true;
                }
            }
            return _ctorParmetersClosure;
        }

        IEnumerable<AutoServiceClassInfo> GetReplacedTargetsFromReplaceServiceAttribute( IActivityMonitor monitor, CKTypeCollector collector )
        {
            foreach( var p in ClassType.GetCustomAttributesData()
                                  .Where( a => a.AttributeType.Name == nameof( ReplaceAutoServiceAttribute ) )
                                  .SelectMany( a => a.ConstructorArguments ) )
            {
                Type? replaced;
                if( p.Value is string s )
                {
                    replaced = SimpleTypeFinder.WeakResolver( s, false );
                    if( replaced == null )
                    {
                        monitor.Warn( $"[ReplaceAutoService] on type '{ClassType}': the assembly qualified name '{s}' cannot be resolved. It is ignored." );
                        continue;
                    }
                }
                else
                {
                    replaced = p.Value as Type;
                    if( replaced == null )
                    {
                        monitor.Warn( $"[ReplaceAutoService] on type '{ClassType}': the parameter '{p.Value}' is not a Type. It is ignored." );
                        continue;
                    }
                }
                var target = collector.FindServiceClassInfo( monitor, replaced );
                if( target == null )
                {
                    monitor.Warn( $"[ReplaceAutoService({replaced.Name})] on type '{ClassType}': the Type to replace is not an Auto Service class implementation. It is ignored." );
                }
                else
                {
                    yield return target;
                }
            }
        }

        // This is called by GetCtorParametersClassClosure and recursively by this method.
        // The initial call is on the service leaf.
        internal bool EnsureCtorBinding( IActivityMonitor m, CKTypeCollector collector )
        {
            Debug.Assert( IsIncluded && !IsRealObject );
            if( _ctorBinding.HasValue ) return _ctorBinding.Value;
            bool success = Generalization?.EnsureCtorBinding( m, collector ) ?? true;
            if( !AnalyzeSingleConstructor( m, collector ) ) success = false;
            _ctorBinding = success;
            return success;
        }

        bool AnalyzeSingleConstructor( IActivityMonitor m, CKTypeCollector collector )
        {
            bool success = true;
            var ctors = ClassType.GetConstructors();
            if( ctors.Length > 1 )
            {
                if( !collector.KindDetector.AmbientServices.Contains( ClassType ) )
                {
                    m.Error( $"Multiple public constructors found for '{ClassType:C}'. Only one must exist. Consider using factory methods that relay to protected constructors for explicit initialization." );
                    success = false;
                }
                else
                {
                    // Ambient services are not automatically resolved but explicitly resolved with a factory function (or found in the AmbientServiceHub
                    // or set to their default value provided by their IEndpointUbiquitousServiceDefault singleton companion).
                    // They can perfectly have multiple constructors (they are selected/used by the explicit factory methods).
                    // When a single constructor is found, we process it like a regular auto service.
                    // Here there are multiple constructors: we ignore them since unifying their parameters don't make a lot of sense.
                    // And by ignoring them here, we apply (caller code) the ReplaceAutoService attribute (if any... and this would be surprising).
                    m.Trace( $"{ctors.Length} public constructors found for ubiquitous endpoint service '{ClassType:C}'. " +
                             $"Skipping constructor parameters analysis (endpoint services are resolved by the endpoint configuration)." );
                    ConstructorParameters = Array.Empty<CtorParameter>();
                }
            }
            else
            {
                if( ctors.Length == 0 )
                {
                    // There is no public constructor: if the class is abstract we cannot conclude anything since the
                    // generated implementation can do whatever it wants to satisfy any of its base constructor.
                    if( ClassType.IsAbstract )
                    {
                        // We can only consider that this type has no constructor parameters.
                        ConstructorParameters = Array.Empty<CtorParameter>();
                    }
                    else
                    {
                        // The class is not abstract and has no public constructor.
                        // Same as above (multiple constructor case): this is allowed for endpoint services since
                        // they are "manually" build by the endpoint code.
                        if( !collector.KindDetector.AmbientServices.Contains( ClassType ) )
                        {
                            // We can't do anything with it.
                            success = false;
                            m.Error( $"No public constructor found for '{ClassType.FullName}' and no default constructor exist (since at least one non-public constructor exists)." );
                        }
                        else
                        {
                            m.Trace( $"No public constructors found for ubiquitous endpoint service '{ClassType:C}'. " +
                                $"Skipping constructor parameters analysis (endpoint services are resolved by the endpoint configuration)." );
                            ConstructorParameters = Array.Empty<CtorParameter>();
                        }
                    }
                }
                else
                {
                    var parameters = ctors[0].GetParameters();
                    var allCtorParameters = new CtorParameter[parameters.Length];
                    foreach( var p in parameters )
                    {
                        var param = CreateCtorParameterData( m, collector, p );
                        success &= param.Success;
                        CtorParameter ctorParameter;
                        if( param.Class != null || param.Interface != null )
                        {
                            Debug.Assert( !param.IsEnumerable, "A [IsMultiple] interface cancels its IAutoService trait (if any)." );
                            ctorParameter = new CtorParameter( p, param.Class, param.Interface );
                        }
                        else ctorParameter = new CtorParameter( p, param.IsEnumerable, param.ParameterType );
                        allCtorParameters[p.Position] = ctorParameter;
                    }
                    ConstructorParameters = allCtorParameters;
                }
            }

            return success;
        }

        readonly ref struct CtorParameterData
         {
            public readonly bool Success;
            public readonly AutoServiceClassInfo? Class;
            public readonly AutoServiceInterfaceInfo? Interface;
            public readonly bool IsEnumerable;
            public readonly CKTypeKind Kind;
            public readonly Type ParameterType;

            public CtorParameterData( bool success, AutoServiceClassInfo? c, AutoServiceInterfaceInfo? i, bool isEnumerable, CKTypeKind lt, Type parameterType )
            {
                Success = success;
                Class = c;
                Interface = i;
                IsEnumerable = isEnumerable;
                Kind = lt;
                ParameterType = parameterType;
            }
        }

        CtorParameterData CreateCtorParameterData( IActivityMonitor monitor,
                                                   CKTypeCollector collector,
                                                   ParameterInfo p )
        {
            var tParam = p.ParameterType;
            bool isEnumerable = false;
            if( tParam.IsGenericType )
            {
                var tGen = tParam.GetGenericTypeDefinition();
                if( tGen == typeof( IEnumerable<> ) )
                {
                    isEnumerable = true;
                    tParam = tParam.GetGenericArguments()[0];
                }
                else 
                {
                    var genKind = collector.KindDetector.GetValidKind( monitor, tGen );
                    if( genKind != CKTypeKind.None )
                    {
                        return new CtorParameterData( true, null, null, false, genKind, tParam );
                    }
                }
            }
            var kind = collector.KindDetector.GetRawKind( monitor, tParam );
            bool isMultipleService = (kind & CKTypeKind.IsMultipleService) != 0;

            var conflictMsg = (kind & CKTypeKind.HasError) != 0 ? kind.GetCombinationError( tParam.IsClass ) : null;
            if( conflictMsg == null )
            {
                // Direct check here of the fact that a [IsMultiple] interface MUST NOT be a direct, single, parameter. 
                if( !isEnumerable && isMultipleService )
                {
                    conflictMsg = $"Cannot depend on one instance of this interface since it is marked as a [IsMultiple] service (IEnumerable<{tParam.Name}> should be used).";
                }
                else if( isEnumerable && !isMultipleService )
                {
                    conflictMsg = $"IEnumerable<T> requires that T is a [IsMultiple] interface.";
                    if( tParam.IsClass )
                    {
                        conflictMsg += " In no way can T be a class.";
                    }
                    else if( tParam.IsInterface )
                    {
                        conflictMsg += $" '{tParam.Name}' must be either a IAutoService marked with [IsMultiple] attribute or be externally declared as a AutoTypeKind.IsMultipleService.";
                    }
                    else
                    {
                        // IEnumerable<ValueType> or other...
                        // This may work depending on the runtime container configuration.
                        // A Warn will be emitted by ComputeFinalTypeKind.
                        conflictMsg = null;
                    }
                }
            }
            if( conflictMsg != null )
            {
                monitor.Error( $"Type '{tParam.FullName}' for parameter '{p.Name}' in '{p.Member.DeclaringType!:C}' constructor: {conflictMsg}" );
                return new CtorParameterData( false, null, null, false, kind, tParam );
            }
            // If the parameter type is not marked with a I(Scoped/Singleton)AutoService, we don't
            // look for a AutoServiceClassInfo or a AutoServiceInterfaceInfo: we are done.
            // Note that it can be externally qualified with a AutoServiceKind but it is not meant to be analyzed like a true IAutoService.
            if( (kind & CKTypeKind.IsAutoService) == 0 )
            {
                return new CtorParameterData( true, null, null, isEnumerable, kind, tParam );
            }
            Debug.Assert( conflictMsg == null && (kind & CKTypeKind.IsAutoService) != 0 );
            if( tParam.IsClass )
            {
                var sClass = collector.FindServiceClassInfo( monitor, tParam );
                if( sClass == null )
                {
                    monitor.Error( $"Unable to resolve '{tParam.FullName}' service type for parameter '{p.Name}' in '{p.Member.DeclaringType!:C}' constructor." );
                    return new CtorParameterData( false, null, null, isEnumerable, kind, tParam );
                }
                if( !sClass.IsIncluded )
                {
                    var reason = sClass.TypeInfo.IsExcluded
                                    ? "excluded from registration"
                                    : "abstract (and cannot be concretized)";
                    var prefix = $"Service type '{tParam}' is {reason}. Parameter '{p.Name}' in '{p.Member.DeclaringType.ToCSharpName()}' constructor ";
                    if( !p.HasDefaultValue )
                    {
                        monitor.Error( prefix + "can not be resolved." );
                        return new CtorParameterData( false, null, null, isEnumerable, kind, tParam );
                    }
                    monitor.Info( prefix + "will use its default value." );
                    sClass = null;
                }
                else if( TypeInfo.IsAssignableFrom( sClass.TypeInfo ) )
                {
                    monitor.Error( $"Parameter '{p.Name}' in '{p.Member.DeclaringType!:C}' constructor cannot be this class or one of its specializations." );
                    return new CtorParameterData( false, null, null, isEnumerable, kind, tParam );
                }
                else if( sClass.TypeInfo.IsAssignableFrom( TypeInfo ) )
                {
                    monitor.Error( $"Parameter '{p.Name}' in '{p.Member.DeclaringType!:C}' constructor cannot be one of its base class." );
                    return new CtorParameterData( false, null, null, isEnumerable, kind, tParam );
                }
                return new CtorParameterData( true, sClass, null, isEnumerable, kind, tParam );
            }
            return new CtorParameterData( true, null, collector.FindServiceInterfaceInfo( monitor, tParam ), isEnumerable, kind, tParam );
        }

        /// <summary>
        /// Awful property that avoids yet another data structure to implement the IStObjServiceFinalManualMapping
        /// and to track the final registration.
        /// Used at the very end of the process when the final StObjObjectEngineMap is built.
        /// </summary>
        internal int MappingListIndex;

        int IStObjServiceFinalSimpleMapping.MappingIndex => MappingListIndex;

        /// <summary>
        /// Overridden to return the <see cref="CKTypeInfo.ToString()"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => TypeInfo.ToString();

    }
}
