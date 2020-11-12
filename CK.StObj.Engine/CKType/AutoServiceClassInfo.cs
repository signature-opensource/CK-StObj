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
    public class AutoServiceClassInfo : IStObjServiceFinalSimpleMapping
    {
        const CKTypeKind CKTypeKindAutoSingleton = CKTypeKind.IsAutoService | CKTypeKind.IsSingleton;
        const CKTypeKind CKTypeKindAutoScoped = CKTypeKind.IsAutoService | CKTypeKind.IsScoped;

        HashSet<AutoServiceClassInfo>? _ctorParmetersClosure;
        // Memorizes the EnsureCtorBinding call state.
        bool? _ctorBinding;

        /// <summary>
        /// Constructor parameter info: either a <see cref="AutoServiceClassInfo"/>,
        /// <see cref="AutoServiceInterfaceInfo"/>, a enumeration of one of them or a
        /// regular (no IAutoService) parameter.
        /// </summary>
        public class CtorParameter
        {
            /// <summary>
            /// The parameter info.
            /// This is never null.
            /// </summary>
            public readonly ParameterInfo ParameterInfo;

            /// <summary>
            /// Not null if this parameter is a service class (ie. a IAutoService implementation).
            /// </summary>
            public readonly AutoServiceClassInfo? ServiceClass;

            /// <summary>
            /// Not null if this parameter is a service interface (a <see cref="IAutoService"/>).
            /// </summary>
            public readonly AutoServiceInterfaceInfo? ServiceInterface;

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
            /// for IEnumerable&lt;X&gt;, this is typeof(X) (either <see cref="ServiceClass"/> or <see cref="ServiceInterface"/>).
            /// Otherwise, it is simply the parameter type: this is never null.
            /// </summary>
            public Type ParameterType { get; }

            /// <summary>
            /// Gets whether this is an enumerable of <see cref="ServiceClass"/> or <see cref="ServiceInterface"/>.
            /// When true, <see cref="ServiceClass"/> xor <see cref="ServiceInterface"/> is not null.
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

            internal CtorParameter(
                ParameterInfo p,
                AutoServiceClassInfo? cS,
                AutoServiceInterfaceInfo? iS,
                bool isEnumerable )
            {
                Debug.Assert( (cS != null) ^ (iS != null) );
                ParameterInfo = p;
                ParameterType = cS?.ClassType ?? iS!.Type;
                IsEnumerable = isEnumerable;
                ServiceClass = cS;
                ServiceInterface = iS;
            }

            internal CtorParameter( ParameterInfo p )
            {
                ParameterInfo = p;
                ParameterType = p.ParameterType;
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

        internal AutoServiceClassInfo(
            IActivityMonitor m,
            IServiceProvider serviceProvider,
            AutoServiceClassInfo? parent,
            Type t,
            bool isExcluded,
            RealObjectClassInfo? objectInfo )
        {
            Debug.Assert( objectInfo == null || objectInfo.ServiceClass == null, "If we are the the asociated Service, we must be the only one." );
            if( objectInfo != null )
            {
                TypeInfo = objectInfo;
                objectInfo.ServiceClass = this;
            }
            else
            {
                TypeInfo = new CKTypeInfo( m, parent?.TypeInfo, t, serviceProvider, isExcluded, this );
            }
            Debug.Assert( parent == null || ReferenceEquals( TypeInfo.Generalization, parent.TypeInfo ), $"Gen={TypeInfo.Generalization}/Par={parent?.TypeInfo}" );

            if( parent != null ) SpecializationDepth = parent.SpecializationDepth + 1;

            // Used only when this service is eventually a simple one.
            SimpleMappingListIndex = -1;
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
        /// <see cref="AutoServiceKind.IsFrontService"/> or <see cref="AutoServiceKind.IsFrontProcessService"/>
        /// are propagated to any service that depend on this one (transitively), unless <see cref="AutoServiceKind.IsMarshallable"/> is set.
        /// </summary>
        public AutoServiceKind? FinalTypeKind { get; private set; }

        /// <summary>
        /// Gets the multiple interfaces that are marked with <see cref="CKTypeKind.IsMultipleService"/>
        /// and that must be mapped to this <see cref="FinalType"/>.
        /// </summary>
        public IReadOnlyCollection<Type> MultipleMappings => TypeInfo.MultipleMappingTypes;

        /// <summary>
        /// Gets the unique types that that must be mapped to this <see cref="FinalType"/> and only to this one.
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
        /// This is not null only if <see cref="IsIncluded"/> is true (ie. this class is not excluded
        /// and is on a concrete path) and may be empty if there is no service interface (the
        /// implementation itself is marked with any <see cref="IScopedAutoService"/> marker).
        /// </summary>
        public IReadOnlyList<AutoServiceInterfaceInfo>? Interfaces { get; private set; }

        /// <summary>
        /// Gets the container type to which this service is associated.
        /// This can be null (service is considered to reside in the final package) or
        /// if an error occured.
        /// For Service Chaining Resolution to be available (either to depend on or be used by others),
        /// services must be associated to one container.
        /// </summary>
        public Type? ContainerType { get; }

        /// <summary>
        /// Gets the StObj container.
        /// </summary>
        public IStObjResult? Container => ContainerItem;

        internal MutableItem? ContainerItem { get; private set; }

        /// <summary>
        /// Gets the constructor. This may be null if any error occurred or
        /// if this service is implemented by an Real object.
        /// </summary>
        public ConstructorInfo? ConstructorInfo { get; private set; }

        /// <summary>
        /// Gets all the constructor parameters' <see cref="ParameterInfo"/> wrapped in <see cref="CtorParameter"/>.
        /// This is empty (even for service implemented by Real object) as soon as the EnsureCtorBinding internal method has been called.
        /// </summary>
        public IReadOnlyList<CtorParameter>? ConstructorParameters { get; private set; }

        /// <summary>
        /// Gets the types that must be marshalled for this Auto service to be marshallable.
        /// This is null until the EnsureCtorBinding internal method has been called.
        /// This is empty (if this service is not marshallable), it contains this <see cref="ClassType"/>
        /// (if it is the one that must have a <see cref="StObj.Model.IMarshaller{T}"/> available), or is a set of one or more types
        /// that must have a marshaller.
        /// </summary>
        public IReadOnlyCollection<Type>? MarshallableTypes { get; private set; }

        IReadOnlyCollection<Type> IStObjServiceClassDescriptor.MarshallableTypes => MarshallableTypes!;

        /// <summary>
        /// Gets the types that must be marshalled for this Auto service to be marshallable inside the same process.
        /// This is null until the EnsureCtorBinding internal method has been called.
        /// This is empty if this service is not marshallable or if it doesn't need to be: only services marked
        /// with <see cref="AutoServiceKind.IsFrontService"/> are concerned since, by design, services that are
        /// not front services at all or are <see cref="AutoServiceKind.IsFrontProcessService"/> don't need to be
        /// marshalled inside the same process.
        /// </summary>
        public IReadOnlyCollection<Type>? MarshallableInProcessTypes { get; private set; }

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
        internal bool InitializePath(
                        IActivityMonitor monitor,
                        CKTypeCollector collector,
                        AutoServiceClassInfo? generalization,
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
                isConcretePath |= c.InitializePath( monitor, collector, this, tempAssembly, lastConcretes, ref abstractTails );
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
                // An unused Auto Service interface (ie. that has no implementation in the context)
                // is like any other interface.
                // Note that if this is a Real Object, multiple mappings are already handled by the real object.
                Interfaces = collector.RegisterServiceInterfaces( TypeInfo.Interfaces, IsRealObject ? (Action<Type>?)null : TypeInfo.AddMultipleMapping ).ToArray();
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
                    if( child.ContainerType != null )
                    {
                        if( (child.ContainerItem = engineMap.ToHighestImpl( child.ContainerType )) == null )
                        {
                            monitor.Error( $"Unable to resolve container '{child.ContainerType.FullName}' for service '{child.ClassType.FullName}' to a StObj." );
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


        bool IStObjFinalClass.IsScoped => (FinalTypeKind!.Value & AutoServiceKind.IsScoped) != 0;

        AutoServiceKind IStObjServiceClassDescriptor.AutoServiceKind => FinalTypeKind!.Value;

        internal AutoServiceKind ComputeFinalTypeKind( IActivityMonitor m, CKTypeKindDetector typeKindDetector, ref bool success )
        {
            Debug.Assert( !TypeInfo.IsSpecialized, "This is called only on leaf, most specialized, class." );
            if( !FinalTypeKind.HasValue )
            {
                var initial = typeKindDetector.GetKind( m, ClassType ).ToAutoServiceKind();
                var final = initial;
                using( m.OpenTrace( $"Computing {ClassType}'s final type based on {ConstructorParameters!.Count} parameter(s). Initially '{initial}'." ) )
                {
                    const AutoServiceKind FrontTypeMask = AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService;
                    const AutoServiceKind IsFrontMashallableMask = AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable;
                    HashSet<Type>? allMarshallableTypes = null;
                    HashSet<Type>? frontMarshallableTypes = null;
                    // If this service is not marshallable then all its parameters that are Front services must be marshallable
                    // so that this service can be "normally" created as long as its required dependencies have been marshalled.
                    // Lets's be optimistic: all parameters that are Front(Process) services (if any) will be marshallable, so this one
                    // can be used "on the other side" as if it was itself marshallable.
                    bool isAutomaticallyMarshallable = true;

                    // If this service is marshallable, it "handles" any of the FrontEnd/Process front service
                    // on which it relies.
                    // If we are on a Front service (the worst case) and this Service is IsMarshallable at its level, 
                    // we don't need to process the parameters since we have nothing to learn...
                    bool isFrontMarshallable = (final & IsFrontMashallableMask) == IsFrontMashallableMask;

                    // This may be exposed?
                    bool requiresInstantiator = false;

                    foreach( var p in ConstructorParameters )
                    {
                        AutoServiceClassInfo? pC = null;
                        AutoServiceKind kP;
                        if( p.IsAutoService )
                        {
                            Debug.Assert( p.FinalServiceClass != null );
                            kP = (pC = p.FinalServiceClass).ComputeFinalTypeKind( m, typeKindDetector, ref success );
                        }
                        else
                        {
                            kP = typeKindDetector.GetKind( m, p.ParameterType ).ToAutoServiceKind();
                            if( (kP & (AutoServiceKind.IsSingleton | AutoServiceKind.IsScoped)) == 0 )
                            {
                                if( p.ParameterType.IsValueType || p.ParameterType == typeof(string) )
                                {
                                    if( !p.ParameterInfo.HasDefaultValue )
                                    {
                                        m.Warn( $"Parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' is a {(p.ParameterType.IsValueType ? "value type" : "string" )} without default value. This prevents automatic instantiation." );
                                        requiresInstantiator = true;
                                    }
                                    // Value type parameter with default value. Skip it.
                                    continue;
                                }
                                else
                                {
                                    m.Info( $"External type '{p.ParameterType}': unknown lifetime is considered Scoped. Type set to {kP | AutoServiceKind.IsScoped}." );
                                    var update = typeKindDetector.RestrictToScoped( m, p.ParameterType );
                                    if( update.HasValue ) kP = update.Value.ToAutoServiceKind();
                                    else success = false;
                                }
                            }
                        }
                        // Handling lifetime.
                        Debug.Assert( (kP & (AutoServiceKind.IsSingleton | AutoServiceKind.IsScoped)) != 0 );
                        if( (kP & AutoServiceKind.IsScoped) != 0 )
                        {
                            if( (final & AutoServiceKind.IsSingleton) != 0 )
                            {
                                m.Error( $"Lifetime error: Type '{ClassType}' is marked as IsSingleton but parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' in constructor is Scoped." );
                                success = false;
                            }
                            else if( (final & AutoServiceKind.IsScoped) == 0 )
                            {
                                m.Info( $"Type '{ClassType}' must be Scoped since parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' in constructor is Scoped." );
                                final |= AutoServiceKind.IsScoped;
                            }
                        }
                        // Handling Front aspects.
                        if( isFrontMarshallable ) continue;
                        // If the parameter is not a front service, we skip it: we don't care of a IsMarshallable only type,
                        // as long as the parameter is not "front", we can safely ignore it. 
                        if( (kP & FrontTypeMask) == 0 ) continue;

                        var newFinal = final | (kP & FrontTypeMask);
                        if( newFinal != final )
                        {
                            // Upgrades from None, Process to Front...
                            m.Trace( $"Type '{ClassType}' must be {newFinal & FrontTypeMask}, because of (at least) constructor's parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}'." );
                            final = newFinal;
                            // We don't have to worry about the IsFrontService that implies the IsScoped flag since this is already handled
                            // by the lifetime code above: if the current parameter is a FrontService then it is also a Scoped and any conflict
                            // with this being a singleton would have been handled.
                            // We can update the flag that avoids useless processing.
                            isFrontMarshallable = (final & IsFrontMashallableMask) == IsFrontMashallableMask;
                            if( isFrontMarshallable ) continue;
                        }
                        // If this Service is marshallable at its level OR it is already known to be NOT automatically marshallable,
                        // we don't have to worry anymore about the parameters marshalling.
                        if( (final&AutoServiceKind.IsMarshallable) != 0 || !isAutomaticallyMarshallable ) continue;

                        if( (kP & AutoServiceKind.IsMarshallable) == 0 )
                        {
                            m.Warn( $"Type '{ClassType}' is not marked as marshallable and the constructor's parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' that is a Front service is not marshallable: it cannot be marked as marshallable." );
                            isAutomaticallyMarshallable = false;
                        }
                        else
                        {
                            if( allMarshallableTypes == null ) allMarshallableTypes = new HashSet<Type>();
                            if( pC != null )
                            {
                                allMarshallableTypes.AddRange( pC.MarshallableTypes );
                            }
                            else
                            {
                                allMarshallableTypes.Add( p.ParameterInfo.ParameterType );
                            }
                            if( (kP & AutoServiceKind.IsFrontService) != 0 )
                            {
                                if( frontMarshallableTypes == null ) frontMarshallableTypes = new HashSet<Type>();
                                if( pC != null )
                                {
                                    frontMarshallableTypes.AddRange( pC.MarshallableInProcessTypes );
                                }
                                else
                                {
                                    frontMarshallableTypes.Add( p.ParameterInfo.ParameterType );
                                }
                            }
                        }
                    }
                    // Conclude about lifetime.
                    if( (final & (AutoServiceKind.IsScoped|AutoServiceKind.IsSingleton)) == 0 )
                    {
                        m.Info( $"Nothing prevents the class '{ClassType}' to be a Singleton: this is the most efficient choice." );
                        final |= AutoServiceKind.IsSingleton;
                    }
                    // Warn about instantiation function.
                    if( requiresInstantiator )
                    {
                        m.Warn( $"'{ClassType}' requires a manual instantiation function." );
                    }
                    // Conclude about Front aspect.
                    if( isFrontMarshallable )
                    {
                        MarshallableTypes = MarshallableInProcessTypes = new[] { ClassType };
                    }
                    else
                    {
                        if( isAutomaticallyMarshallable && allMarshallableTypes != null )
                        {
                            Debug.Assert( allMarshallableTypes.Count > 0 );
                            MarshallableTypes = allMarshallableTypes;
                            final |= AutoServiceKind.IsMarshallable;
                            if( frontMarshallableTypes != null )
                            {
                                MarshallableInProcessTypes = frontMarshallableTypes;
                            }
                            else MarshallableInProcessTypes = Type.EmptyTypes;
                        }
                        else
                        {
                            // This service is not a Front service OR it is not automatically marshallable.
                            // We have nothing special to do: the set of Marshallable types is empty (this is not an error)
                            // and this FinalTypeKind will be 'None' or a EndPoint/Process service but without the IsMarshallable bit.
                            MarshallableTypes = MarshallableInProcessTypes = Type.EmptyTypes;
                        }
                    }
                    if( final != initial ) m.CloseGroup( $"Final: {final}" );
                    FinalTypeKind = final;
                }
            }
            return FinalTypeKind.Value;
        }

        ///// <summary>
        ///// Ensures that the final lifetime and front kind are computed: <see cref="MustBeScopedLifetime"/> and <see cref="FinalTypeKind"/>
        ///// will not be null once called.
        ///// Returns the MustBeScopedLifetime (true if this Service implementation must be scoped and false for singleton) and service kind.
        ///// </summary>
        ///// <param name="m">The monitor to use.</param>
        ///// <param name="typeKindDetector">The type detector (used to check singleton life times and promote mere IAutoService to singletons).</param>
        ///// <param name="success">Success reference token.</param>
        ///// <returns>First is True for scoped, false for singleton. Second is the front service.</returns>
        //[Obsolete]
        //internal (bool Scoped, AutoServiceKind Kind) GetFinalMustBeScopedAndFrontKind( IActivityMonitor m, CKTypeKindDetector typeKindDetector, ref bool success )
        //{
        //    Debug.Assert( !TypeInfo.IsSpecialized, "This is called only on leaf, most specialized, class." );
        //    if( !MustBeScopedLifetime.HasValue )
        //    {
        //        foreach( var p in ConstructorAutoServiceParameters )
        //        {
        //            var c = p.ServiceClass?.MostSpecialized ?? p.ServiceInterface?.FinalResolved;
        //            if( c != null )
        //            {
        //                bool scoped = c.GetFinalMustBeScopedAndFrontKind( m, typeKindDetector, ref success ).Scoped;
        //                if( !MustBeScopedLifetime.HasValue && scoped )
        //                {
        //                    if( (TypeKind & CKTypeKind.IsSingleton) == CKTypeKind.IsSingleton )
        //                    {
        //                        m.Error( $"Lifetime error: Type '{ClassType}' is {nameof( ISingletonAutoService )} but parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' in constructor is Scoped." );
        //                        success = false;
        //                    }
        //                    if( !MustBeScopedLifetime.HasValue )
        //                    {
        //                        m.Info( $"Type '{ClassType}' must be Scoped since parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' in constructor is Scoped." );
        //                    }
        //                    MustBeScopedLifetime = true;
        //                }
        //            }
        //        }
        //        if( !MustBeScopedLifetime.HasValue )
        //        {
        //            if( _requiredParametersToBeSingletons != null )
        //            {
        //                Debug.Assert( (TypeKind&CKTypeKind.LifetimeMask) == 0, "Lifetime is not specified." );
        //                foreach( var external in _requiredParametersToBeSingletons )
        //                {
        //                    if( !typeKindDetector.IsSingleton( external.ParameterType ) )
        //                    {
        //                        m.Info( $"Type '{ClassType.Name}' must be Scoped since parameter '{external.Name}' of type '{external.ParameterType.Name}' in constructor is not a Singleton." );
        //                        MustBeScopedLifetime = true;
        //                        break;
        //                    }
        //                }
        //            }
        //            if( !MustBeScopedLifetime.HasValue )
        //            {
        //                MustBeScopedLifetime = false;
        //                if( (TypeKind&CKTypeKind.IsSingleton) == 0 )
        //                {
        //                    m.Info( $"Nothing prevents the class '{ClassType}' to be a Singleton: this is the most efficient choice." );
        //                    var updated = typeKindDetector.PromoteToSingleton( m, ClassType );
        //                    if( updated == null ) success = false; 
        //                    else TypeKind = updated.Value;
        //                }
        //            }
        //        }
        //    }
        //    if( !FinalTypeKind.HasValue )
        //    {
        //        const AutoServiceKind FrontTypeMask = AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService;
        //        const AutoServiceKind FrontMask = FrontTypeMask | AutoServiceKind.IsMarshallable;

        //        AutoServiceKind frontKind = typeKindDetector.GetKind( m, ClassType ).ToAutoServiceKind() & FrontMask;

        //        // If this service is marshallable, it "handles" any of the FrontEnd/Process front service
        //        // on which it relies.
        //        // However a check must be done: a simple Process Front service cannot rely on a EndPoint Front service!
        //        //
        //        // If this service is not marshallable then all its parameters that are Front services must be marshallable
        //        // so that this service can be "normally" created as long as its required dependencies have been marshalled.
        //        //
        //        bool frontSuccess = true;

        //        if( (frontKind & (AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable)) == (AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable) )
        //        {
        //            // If we are on a Front service (the worst case) and this Service is IsMarshallable at its level, 
        //            // we don't need to process the parameters since we have nothing to learn...
        //            MarshallableTypes = MarshallableInProcessTypes = new[] { ClassType };
        //        }
        //        else
        //        {
        //            HashSet<Type> allMarshallableTypes = null;
        //            HashSet<Type> frontMarshallableTypes = null;
        //            // Lets's be optimistic: all parameters that are Front(Process) services (if any) will be mashallable, so this one
        //            // can be used "on the other side" as if it was itself marshallable.
        //            bool isAutomaticallyMarshallable = true;
        //            // We have to analyze the parameters.
        //            foreach( var p in AllConstructorParameters )
        //            {
        //                AutoServiceKind parameterKind;
        //                AutoServiceClassInfo c = p.ServiceClass?.MostSpecialized ?? p.ServiceInterface?.FinalResolved;
        //                if( c != null )
        //                {
        //                    parameterKind = c.GetFinalMustBeScopedAndFrontKind( m, typeKindDetector, ref success ).Kind;
        //                    if( !success ) frontSuccess = false;
        //                }
        //                else
        //                {
        //                    parameterKind = typeKindDetector.GetKind( m, p.ParameterType ).ToAutoServiceKind();
        //                }
        //                // If the parameter is not a front service, we skip it.
        //                if( (parameterKind & FrontTypeMask) == 0 ) continue;

        //                var newFrontKind = frontKind | (parameterKind & FrontTypeMask);
        //                if( newFrontKind != frontKind )
        //                {
        //                    m.Trace( $"Type '{ClassType}' must be {newFrontKind & FrontTypeMask}, because of (at least) constructor's parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}'." );
        //                    frontKind = newFrontKind;
        //                }
        //                // If this Service is marshallable, we don't have to worry about the parameters marshalling:
        //                // they only impact the IsFront(Process)Service flag.
        //                if( (frontKind & AutoServiceKind.IsMarshallable) != 0 || !isAutomaticallyMarshallable )
        //                {
        //                    // If we are on a FrontService, there is nothing more to achieve here: this is the worst case.
        //                    if( (frontKind & AutoServiceKind.IsFrontService) != 0 ) break;
        //                }
        //                else
        //                {
        //                    if( (parameterKind & AutoServiceKind.IsMarshallable) == 0 )
        //                    {
        //                        m.Warn( $"Type '{ClassType}' is not marked as marshallable and the constructor's parameter '{p.Name}' of type '{p.ParameterInfo.ParameterType.Name}' that is a Front service is not marshallable: type '{ClassType}' cannot be marked as marshallable." );
        //                        isAutomaticallyMarshallable = false;
        //                    }
        //                    else
        //                    {
        //                        if( allMarshallableTypes == null ) allMarshallableTypes = new HashSet<Type>();
        //                        if( c != null )
        //                        {
        //                            allMarshallableTypes.AddRange( c.MarshallableTypes );
        //                        }
        //                        else
        //                        {
        //                            allMarshallableTypes.Add( p.ParameterInfo.ParameterType );
        //                        }
        //                        if( (parameterKind & AutoServiceKind.IsFrontService) != 0 )
        //                        {
        //                            if( frontMarshallableTypes == null ) frontMarshallableTypes = new HashSet<Type>();
        //                            if( c != null )
        //                            {
        //                                frontMarshallableTypes.AddRange( c.MarshallableInProcessTypes );
        //                            }
        //                            else
        //                            {
        //                                frontMarshallableTypes.Add( p.ParameterInfo.ParameterType );
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            if( frontSuccess )
        //            {
        //                if( isAutomaticallyMarshallable && allMarshallableTypes != null )
        //                {
        //                    Debug.Assert( allMarshallableTypes.Count > 0 );
        //                    MarshallableTypes = allMarshallableTypes;
        //                    frontKind |= AutoServiceKind.IsMarshallable;
        //                    if( frontMarshallableTypes != null )
        //                    {
        //                        MarshallableInProcessTypes = frontMarshallableTypes;
        //                    }
        //                    else MarshallableInProcessTypes = Type.EmptyTypes;
        //                }
        //                else
        //                {
        //                    // This service is not a Front service OR it is not automatically marshallable.
        //                    // We have nothing special to do: the set of Marshallable types is empty (this is not an error)
        //                    // and this FinalFrontServiceKind will be 'None' or a EndPoint/Process service but without the IsMarshallable bit.
        //                    MarshallableTypes = MarshallableInProcessTypes = Type.EmptyTypes;
        //                }
        //            }
        //        }

        //        if( MustBeScopedLifetime.Value ) frontKind |= AutoServiceKind.IsScoped;
        //        else frontKind |= AutoServiceKind.IsSingleton;

        //        if( frontSuccess )
        //        {
        //            if( (frontKind&AutoServiceKind.IsFrontProcessService) == 0 )
        //            {
        //                m.Debug( $"'{ClassType}' is not a front service." );
        //            }
        //            else
        //            {
        //                m.Trace( $"'{ClassType}' is a front service ({frontKind})." );
        //            }
        //        }
        //        FinalTypeKind = frontKind;
        //    }
        //    return (MustBeScopedLifetime.Value, FinalTypeKind.Value);
        //}

        /// <summary>
        /// This is called on the Service leaf and recursively on the Generalization.
        /// </summary>
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

        IEnumerable<AutoServiceClassInfo> GetReplacedTargetsFromReplaceServiceAttribute( IActivityMonitor m, CKTypeCollector collector )
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
                        m.Warn( $"[ReplaceAutoService] on type '{ClassType}': the assembly qualified name '{s}' cannot be resolved. It is ignored." );
                        continue;
                    }
                }
                else
                {
                    replaced = p.Value as Type;
                    if( replaced == null )
                    {
                        m.Warn( $"[ReplaceAutoService] on type '{ClassType}': the parameter '{p.Value}' is not a Type. It is ignored." );
                        continue;
                    }
                }
                var target = collector.FindServiceClassInfo( replaced );
                if( target == null )
                {
                    m.Warn( $"[ReplaceAutoService({replaced.Name})] on type '{ClassType}': the Type to replace is not an Auto Service class implementation. It is ignored." );
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
            bool success = false;
            var ctors = ClassType.GetConstructors();
            if( ctors.Length > 1 ) m.Error( $"Multiple public constructors found for '{ClassType.FullName}'. Only one must exist." );
            else
            {
                success = Generalization?.EnsureCtorBinding( m, collector ) ?? true;
                if( ctors.Length == 0 )
                {
                    // There is no public constructor: if the class is abstract we cannot conclude anything since the
                    // generated implementation can do whaterver it wants to satisfy any of its base constructor.
                    if( ClassType.IsAbstract )
                    {
                        // We can only consider that this type has no constructor parameters.
                        ConstructorParameters = Array.Empty<CtorParameter>();
                    }
                    else
                    {
                        // The class is not abstract and has no public constructor. We can't do anything with it.
                        success = false;
                        m.Error( $"No public constructor found for '{ClassType.FullName}' and no default constructor exist since at least one non-public constructor exists." );
                    }
                }
                else 
                {
                    var parameters = ctors[0].GetParameters();
                    var allCtorParameters = new CtorParameter[parameters.Length];
                    foreach( var p in parameters )
                    {
                        var param = CreateCtorParameter( m, collector, p );
                        success &= param.Success;
                        CtorParameter ctorParameter;
                        if( param.Class != null || param.Interface != null )
                        {
                            ctorParameter = new CtorParameter( p, param.Class, param.Interface, param.IsEnumerable );
                        }
                        else ctorParameter = new CtorParameter( p );
                        allCtorParameters[p.Position] = ctorParameter;

                        // Temporary: Enumeration is not implemented yet.
                    if( success && param.IsEnumerable /*&& (param.Lifetime & CKTypeKind.IsMultipleService) == 0*/ )
                        {
                            m.Error( $"IEnumerable<T> or IReadOnlyList<T> where T is marked with IScopedAutoService or ISingletonAutoService is not supported yet: '{ClassType.FullName}' constructor cannot be handled." );
                            success = false;
                        }
                    }
                    ConstructorParameters = allCtorParameters;
                    ConstructorInfo = ctors[0];
                }
            }
            _ctorBinding = success;
            return success;
        }

         readonly ref struct CtorParameterData
         {
            public readonly bool Success;
            public readonly AutoServiceClassInfo? Class;
            public readonly AutoServiceInterfaceInfo? Interface;
            public readonly bool IsEnumerable;
            public readonly CKTypeKind Lifetime;

            public CtorParameterData( bool success, AutoServiceClassInfo? c, AutoServiceInterfaceInfo? i, bool isEnumerable, CKTypeKind lt )
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
                    var genLifetime = collector.CKTypeKindDetector.GetKind( m, tGen );
                    if( genLifetime != CKTypeKind.None )
                    {
                        return new CtorParameterData( true, null, null, false, genLifetime );
                    }
                }
            }
            var lifetime = collector.CKTypeKindDetector.GetKind( m, tParam );
            var conflictMsg = lifetime.GetCombinationError( tParam.IsClass );
            if( conflictMsg == null )
            {
                // Direct check here of the fact that a [IsMultiple] interface MUST NOT be a direct parameter. 
                bool isMultipleService = (lifetime & CKTypeKind.IsMultipleService) != 0;
                if( isMultipleService && !isEnumerable )
                {
                    conflictMsg = $"Cannot depend on one instance of this type since it is marked as a [IsMultiple] service (IEnumerable<{tParam.Name}> should be used).";
                }
            }
            if( conflictMsg == null )
            {
                // If the parameter type is not marked with a I(Scoped/Singleton)AutoService, we don't
                // look for a AutoServiceClassInfo or a AutoServiceInterfaceInfo: we are done.
                if( (lifetime & CKTypeKind.IsAutoService) == 0 )
                {
                    return new CtorParameterData( true, null, null, isEnumerable, lifetime );
                }
            }
            Debug.Assert( p.Member.DeclaringType != null );
            if( conflictMsg != null )
            {
                m.Error( $"Type '{tParam.FullName}' for parameter '{p.Name}' in '{p.Member.DeclaringType.FullName}' constructor: {conflictMsg}" );
                return new CtorParameterData( false, null, null, false, lifetime );
            }

            Debug.Assert( conflictMsg == null && (lifetime & CKTypeKind.IsAutoService) != 0 );
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
        /// Awful property that avoids yet another data structure to implement the IStObjServiceFinalManualMapping
        /// and to track the final registration.
        /// Used at the very end of the process when the final StObjObjectEngineMap is built.
        /// </summary>
        internal int SimpleMappingListIndex;

        int IStObjServiceFinalSimpleMapping.SimpleMappingIndex => SimpleMappingListIndex;

        /// <summary>
        /// Overridden to return the <see cref="CKTypeInfo.ToString()"/>.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => TypeInfo.ToString();

    }
}
