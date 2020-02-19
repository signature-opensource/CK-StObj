#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\StObj\Impl\MutableItem.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Reflection;
using System.Diagnostics;

namespace CK.Setup
{

    partial class MutableItem : IStObjResult, IStObjMutableItem, IDependentItemContainerTyped, IDependentItemContainerRef
    {
        class LeafData
        {
            public LeafData( MutableItem leaf, List<MutableAmbientProperty> ap, MutableInjectObject[] ac )
            {
                LeafSpecialization = leaf;
                AllAmbientProperties = ap;
                AllInjectObjects = ac;
            }

            /// <summary>
            /// Useless to store it at each level.
            /// </summary>
            public readonly MutableItem LeafSpecialization;
            
            /// <summary>
            /// Ambient Properties are shared by the inheritance chain (it is
            /// not null only at the specialization level).
            /// It is a List because we use it as a cache for propagation of ambient properties (in 
            /// EnsureCachedAmbientProperty): new properties issued from Container or Generalization are added 
            /// and cached into this list.
            /// </summary>
            public readonly List<MutableAmbientProperty> AllAmbientProperties;

            /// <summary>
            /// Like Ambient Properties above, Inject Objects are shared by the inheritance chain (it is
            /// not null only at the specialization level), but can use here an array instead of a dynamic list
            /// since there is no caching needed. Each MutableInjectSingleton here is bound to its InjectSingletonInfo
            /// in the RealObjectClassInfo.InjectSingletons.
            /// </summary>
            public readonly MutableInjectObject[] AllInjectObjects;

            // Direct properties are collected at leaf level and are allocated only if needed (by SetDirectPropertyValue).
            public Dictionary<PropertyInfo,object> DirectPropertiesToSet;

            /// <summary>
            /// Available only at the leaf level.
            /// </summary>
            public object StructuredObject;

            /// <summary>
            /// The ImplementableTypeInfo is not null only if the Type is abstract but
            /// a <see cref="ImplementableTypeInfo.StubType"/> has been successfuly created.
            /// </summary>
            public ImplementableTypeInfo ImplementableTypeInfo => LeafSpecialization.Type.ImplementableTypeInfo;

            /// <summary>
            /// Useless to store it at each level.
            /// </summary>
            public MutableItem RootGeneralization;

            public List<PropertySetter> PostBuildProperties;

            internal object CreateStructuredObject( IStObjRuntimeBuilder runtimeBuilder, Type typeIfNotImplementable )
            {
                Type toInstanciate = ImplementableTypeInfo != null
                                        ? ImplementableTypeInfo.StubType
                                        : typeIfNotImplementable;
                StructuredObject = runtimeBuilder.CreateInstance( toInstanciate );
                return StructuredObject;
            }
        }

        LeafData _leafData;

        // This is available at any level thanks to the ordering of ambient properties
        // and the ListAmbientProperty that exposes only the start of the list: only the 
        // properties that are available at the level appear in the list.
        // (This is the same for the injected real objects.)
        readonly IReadOnlyList<MutableAmbientProperty> _ambientPropertiesEx;
        readonly IReadOnlyList<MutableInjectObject> _ambientInjectObjectsEx;

        MutableReference _container;
        MutableReferenceList _requires;
        MutableReferenceList _requiredBy;
        MutableReferenceList _children;
        MutableReferenceList _groups;
        
        IReadOnlyList<MutableParameter> _constructParameterEx;
        /// <summary>
        /// This is the projection of <see cref="RealObjectClassInfo.BaseTypeInfo"/>'s <see cref="IStObjTypeRootParentInfo.StObjConstructCollector"/>.
        /// This is not null if this is the root of the specialization path (<see cref="Generalization"/> is null) and at least
        /// one of the base class has a StObjConstruct (with at least one parameter).
        /// </summary>
        IReadOnlyList<(MethodInfo, IReadOnlyList<MutableParameter>)> _constructParametersAbove;
        DependentItemKind _itemKind;
        List<StObjProperty> _stObjProperties;
        List<PropertySetter> _preConstruct;

        string _dFullName;
        MutableItem _dContainer;
        IReadOnlyList<MutableItem> _dRequires;
        IReadOnlyList<MutableItem> _dRequiredBy;
        IReadOnlyList<MutableItem> _dChildren;
        IReadOnlyList<MutableItem> _dGroups;

        /// <summary>
        /// Our container comes from the configuration of this item or is inherited (from generalization). 
        /// </summary>
        bool IsOwnContainer => _dContainer != null && _dContainer.ObjectType == _container.Type;

        /// <summary>
        /// The tracking mode for ambient properties is inherited and nothing prevents it to 
        /// change between levels (a Generalization can set AddPropertyHolderAsChildren and a Specialization 
        /// define PropertyHolderRequiredByThis, even if that seems pretty strange and that I can not imagine any
        /// clever use of such beast...). Anyway, technically speaking, it has to work this way.
        /// </summary>
        TrackAmbientPropertiesMode _trackAmbientPropertiesMode;

        // Ambient properties are per StObj.
        List<TrackedAmbientPropertyInfo> _trackedAmbientProperties;
        /// <summary>
        /// True if this or any Generalization has _trackAmbientPropertiesMode != None.
        /// </summary>
        bool _needsTrackedAmbientProperties;

        enum PrepareState : byte
        {
            None,
            RecursePreparing,
            PreparedDone,
            CachingAmbientProperty
        }
        PrepareState _prepareState;

        /// <summary>
        /// Only used for empty object pattern for markers.
        /// </summary>
        internal MutableItem()
        {
        }

        /// <summary>
        /// Called from Generalization to Specialization.
        /// </summary>
        internal MutableItem( RealObjectClassInfo type, MutableItem generalization, StObjObjectEngineMap engineMap )
        {
            EngineMap = engineMap;
            Type = type;
            Generalization = generalization;
            // These 2 lists can be initialized here (even if they can not work until InitializeBottomUp is called).
            _ambientPropertiesEx = new ListAmbientProperty( this );
            _ambientInjectObjectsEx = new ListInjectSingleton( this );
        }

        /// <summary>
        /// Second step of initialization called once a valid Type path has been found.
        /// </summary>
        /// <param name="specialization">The specialization. Null if this is the leaf.</param>
        /// <param name="implementableTypeInfo">
        /// A valid implementable type info if <paramref name="specialization"/> is null (ie. we are on a leaf)
        /// and Type is abstract.</param>
        internal void InitializeBottomUp( MutableItem specialization, ImplementableTypeInfo implementableTypeInfo )
        {
            if( specialization != null )
            {
                Debug.Assert( specialization.Generalization == this );
                Specialization = specialization;
                _leafData = specialization._leafData;
            }
            else
            {
                var ap = Type.AmbientProperties.Select( p => new MutableAmbientProperty( this, p ) ).ToList();
                var ac = new MutableInjectObject[Type.InjectObjects.Count];
                for( int i = ac.Length - 1; i >= 0; --i )
                {
                    ac[i] = new MutableInjectObject( this, Type.InjectObjects[i] );
                }
                _leafData = new LeafData( this, ap, ac );
            }
        }

        #region Configuration

        internal void ConfigureTopDown( IActivityMonitor monitor, MutableItem rootGeneralization )
        {
            Debug.Assert( _leafData.RootGeneralization == null || _leafData.RootGeneralization == rootGeneralization );
            Debug.Assert( (rootGeneralization == this) == (Generalization == null) );

            _leafData.RootGeneralization = rootGeneralization;
            ApplyTypeInformation( monitor );
            AnalyzeConstruct( monitor );
            ConfigureFromAttributes( monitor );
        }

        void ApplyTypeInformation( IActivityMonitor monitor )
        {
            Debug.Assert( _container == null, "Called only once right after object instanciation." );

            _container = new MutableReference( this, StObjMutableReferenceKind.Container );
            _container.Type = Type.Container;
            _itemKind = Type.ItemKind;

            if( Type.StObjProperties.Count > 0 ) _stObjProperties = Type.StObjProperties.Select( sp => new StObjProperty( sp ) ).ToList();

            // StObjTypeInfo already applied inheritance of TrackAmbientProperties attribute accross StObj levels.
            // But since TrackAmbientProperties is "mutable" (can be configured), we only know its actual value once PrepareDependentItem has done its job:
            // inheritance by StObjType onky gives the IStObjStructuralConfigurator a more precise information.
            _trackAmbientPropertiesMode = Type.TrackAmbientProperties;
            _requires = new MutableReferenceList( this, StObjMutableReferenceKind.Requires );
            if( Type.Requires != null )
            {
                _requires.AddRange( Type.Requires.Select( t => new MutableReference( this, StObjMutableReferenceKind.Requires ) { Type = t } ) );
            }
            _requiredBy = new MutableReferenceList( this, StObjMutableReferenceKind.RequiredBy );
            if( Type.RequiredBy != null )
            {
                _requiredBy.AddRange( Type.RequiredBy.Select( t => new MutableReference( this, StObjMutableReferenceKind.RequiredBy ) { Type = t } ) );
            }
            _children = new MutableReferenceList( this, StObjMutableReferenceKind.Child );
            if( Type.Children != null )
            {
                _children.AddRange( Type.Children.Select( t => new MutableReference( this, StObjMutableReferenceKind.RequiredBy ) { Type = t } ) );
            }
            _groups = new MutableReferenceList( this, StObjMutableReferenceKind.Group );
            if( Type.Groups != null )
            {
                _groups.AddRange( Type.Groups.Select( t => new MutableReference( this, StObjMutableReferenceKind.Group ) { Type = t } ) );
            }
        }

        void AnalyzeConstruct( IActivityMonitor monitor )
        {
            Debug.Assert( _constructParameterEx == null, "Called only once right after object instanciation..." );
            Debug.Assert( _container != null, "...and after ApplyTypeInformation." );

            var fromAbove = Type.BaseTypeInfo?.StObjConstructCollector;
            if( fromAbove != null )
            {
                _constructParametersAbove = fromAbove
                        .Select( c => (c.Item1,(IReadOnlyList<MutableParameter>)c.Item2.Select( p => new MutableParameter( this, p, false ) ).ToArray() ) )
                        .ToArray();
            }

            if( Type.StObjConstruct != null )
            {
                Debug.Assert( Type.ConstructParameters.Length > 0, "Parameterless StObjConstruct are skipped." );
                var parameters = new MutableParameter[Type.ConstructParameters.Length];
                for( int idx = 0; idx < parameters.Length; ++idx )
                {
                    ParameterInfo cp = Type.ConstructParameters[idx];
                    bool isContainer = idx == Type.ContainerConstructParameterIndex;
                    MutableParameter p = new MutableParameter( this, cp, isContainer );
                    if( isContainer )
                    {
                        // Sets the _container to be the parameter object.
                        _container = p;
                    }
                    parameters[idx] = p;
                }
                _constructParameterEx = parameters;
            }
            else
            {
                _constructParameterEx = Util.Array.Empty<MutableParameter>();
            }
        }

        void ConfigureFromAttributes( IActivityMonitor monitor )
        {
            foreach( var c in Attributes.GetAllCustomAttributes<IStObjStructuralConfigurator>() )
            {
                c.Configure( monitor, this );
            }
        }


        #endregion

        public StObjObjectEngineMap EngineMap { get; }

        /// <summary>
        /// Gets the StObjTypeInfo basic and immutable information.
        /// </summary>
        public RealObjectClassInfo Type { get; }

        /// <summary>
        /// The ImplementableTypeInfo is not null only if the Type is abstract but
        /// a <see cref="ImplementableTypeInfo.StubType"/> has been successfuly created.
        /// </summary>
        public ImplementableTypeInfo ImplementableTypeInfo => _leafData.ImplementableTypeInfo;

        /// <summary>
        /// Gets the generalization. 
        /// Null if this is the root of the specialization path.
        /// </summary>
        public MutableItem Generalization { get; }

        /// <summary>
        /// Gets the specialization. 
        /// Null if this is a leaf.
        /// </summary>
        public MutableItem Specialization { get; private set; }

        /// <summary>
        /// Gets the provider for attributes. Attributes that are marked with <see cref="IAttributeContextBound"/> are cached
        /// and can keep an internal state if needed.
        /// </summary>
        /// <remarks>
        /// All attributes related to ObjectType (either on the type itself or on any of its members) should be retrieved 
        /// thanks to this method otherwise stateful attributes will not work correctly.
        /// </remarks>
        public ICKCustomAttributeTypeMultiProvider Attributes => Type.Attributes;

        /// <summary>
        /// Never null.
        /// </summary>
        internal MutableItem LeafSpecialization => _leafData.LeafSpecialization; 

        internal MutableItem RootGeneralization => _leafData.RootGeneralization; 

        #region IStObjMutableItem is called during Configuration

        public DependentItemKindSpec ItemKind
        {
            get { return (DependentItemKindSpec)_itemKind; }
            set { _itemKind = (DependentItemKind)value; }
        }

        TrackAmbientPropertiesMode IStObjMutableItem.TrackAmbientProperties
        {
            get { return _trackAmbientPropertiesMode; }
            set { _trackAmbientPropertiesMode = value; }
        }

        IStObjMutableReference IStObjMutableItem.Container => _container; 

        IStObjMutableReferenceList IStObjMutableItem.Children => _children;

        IStObjMutableReferenceList IStObjMutableItem.Requires => _requires;

        IStObjMutableReferenceList IStObjMutableItem.RequiredBy => _requiredBy; 

        IStObjMutableReferenceList IStObjMutableItem.Groups => _groups;

        public IReadOnlyList<IStObjMutableParameter> ConstructParameters => _constructParameterEx;

        public IEnumerable<(MethodInfo,IReadOnlyList<IStObjMutableParameter>)> ConstructParametersAbove =>_constructParametersAbove?.Select( mp => (mp.Item1, (IReadOnlyList<IStObjMutableParameter>)mp.Item2) ); 

        IReadOnlyList<IStObjAmbientProperty> IStObjMutableItem.SpecializedAmbientProperties => _ambientPropertiesEx; 

        IReadOnlyList<IStObjMutableInjectObject> IStObjMutableItem.SpecializedInjectObjects => _ambientInjectObjectsEx;

        bool IStObjMutableItem.SetDirectPropertyValue( IActivityMonitor monitor, string propertyName, object value, string sourceDescription )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor", "Source:" + sourceDescription );
            if( String.IsNullOrEmpty( propertyName ) ) throw new ArgumentException( "Can not be null nor empty. Source:" + sourceDescription, "propertyName" );
            if( value == System.Type.Missing ) throw new ArgumentException( "Setting property to System.Type.Missing is not allowed. Source:" + sourceDescription, "value" );

            // Is it an Ambient property?
            // If yes, it is an error... 
            // We may consider that it is an error if the property is defined at this type level (or above), 
            // and a simple warning if the property is defined by a specialization (the developer may not be aware of it).
            // Note: since we check properties' type homogeneity in StObjTypeInfo, an Ambient/StObj/Direct property is always 
            // of the same "kind" regardless of its owner specialization depth.
            MutableAmbientProperty mp = _leafData.AllAmbientProperties.FirstOrDefault( a => a.Name == propertyName );
            if( mp != null )
            {
                monitor.Error( $"Unable to set direct property '{Type.Type.FullName}.{propertyName}' since it is defined as an Ambient property. Use SetAmbientPropertyValue to set it. (Source:{sourceDescription})" );
                return false;
            }

            // Direct property set.
            // Targets the specialization to honor property covariance.
            var leafType = _leafData.LeafSpecialization.Type.Type;
            PropertyInfo p = leafType.GetProperty( propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
            if( p != null && p.DeclaringType != leafType )
            {
                p = p.DeclaringType.GetProperty( propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
            }
            if( p == null || !p.CanWrite )
            {
                monitor.Error( $"Unable to set direct property '{Type.Type.FullName}.{propertyName}' structural value. It must exist and be writable (on type '{_leafData.LeafSpecialization.Type.Type.FullName}'). (Source:{sourceDescription})"  );
                return false;
            }
            if( _leafData.DirectPropertiesToSet == null ) _leafData.DirectPropertiesToSet = new Dictionary<PropertyInfo, object>();
            _leafData.DirectPropertiesToSet[p] = value;
            return true;
        }

        bool IStObjMutableItem.SetAmbientPropertyValue( IActivityMonitor monitor, string propertyName, object value, string sourceDescription )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor", "Source:" + sourceDescription );
            if( String.IsNullOrEmpty( propertyName ) ) throw new ArgumentException( "Can not be null nor empty. Source:" + sourceDescription, "propertyName" );
            if( value == System.Type.Missing ) throw new ArgumentException( "Setting property to System.Type.Missing is not allowed. Source:" + sourceDescription, "value" );

            // Is it an Ambient property?
            // If yes, set the value onto the property.
            MutableAmbientProperty mp = _leafData.AllAmbientProperties.FirstOrDefault( a => a.Name == propertyName );
            if( mp != null )
            {
                return mp.SetValue( Type.SpecializationDepth, monitor, value );
            }
            monitor.Error( $"Unable to set unexisting Ambient property '{Type.Type.FullName}.{propertyName}'. It must exist, be writable and marked with AmbientPropertyAttribute. (Source:{sourceDescription})" );
            return false;
        }

        bool IStObjMutableItem.SetAmbientPropertyConfiguration( IActivityMonitor monitor, string propertyName, Type type, StObjRequirementBehavior behavior, string sourceDescription )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor", "Source:" + sourceDescription );
            if( String.IsNullOrEmpty( propertyName ) ) throw new ArgumentException( "Can not be null nor empty. Source:" + sourceDescription, "propertyName" );

            MutableAmbientProperty mp = _leafData.AllAmbientProperties.FirstOrDefault( a => a.Name == propertyName );
            if( mp != null )
            {
                return mp.SetConfiguration( Type.SpecializationDepth, monitor, type, behavior );
            }
            monitor.Error( $"Unable to configure unexisting Ambient property '{Type.Type.FullName}.{propertyName}'. It must exist, be writable and marked with AmbientPropertyAttribute. (Source:{sourceDescription})" );
            return false;        
        }

        bool IStObjMutableItem.SetStObjPropertyValue( IActivityMonitor monitor, string propertyName, object value, string sourceDescription )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor", "Source:" + sourceDescription );
            if( String.IsNullOrEmpty( propertyName ) ) throw new ArgumentException( "Can not be null nor empty. Source:" + sourceDescription, "propertyName" );
            if( value == System.Type.Missing ) throw new ArgumentException( "Setting property to System.Type.Missing is not allowed. Source:" + sourceDescription, "value" );

            MutableAmbientProperty mp = _leafData.AllAmbientProperties.FirstOrDefault( a => a.Name == propertyName );
            if( mp != null )
            {
                monitor.Error( $"Unable to set StObj property '{Type.Type.FullName}.{propertyName}' since it is defined as an Ambient property. Use SetAmbientPropertyValue to set it. (Source:{sourceDescription})" );
                return false;
            }

            SetStObjProperty( propertyName, value );
            return true;
        }

        #endregion

        internal bool PrepareDependentItem( IActivityMonitor monitor, BuildValueCollector valueCollector )
        {
            if( _prepareState == PrepareState.PreparedDone ) return true;
            using( monitor.OpenTrace( $"Preparing '{ToString()}'." ) )
            {
                try
                {
                    bool result = true;
                    if( _prepareState == PrepareState.RecursePreparing )
                    {
                        monitor.Warn( "Cycle detected while preparing item." );
                        result = false;
                    }
                    else
                    {
                        _prepareState = PrepareState.RecursePreparing;
                        
                        ResolveDirectReferences( monitor );
                        if( _dContainer != null ) result &= _dContainer.PrepareDependentItem( monitor, valueCollector );
                        // Prepares Generalization and inherits from it as needed.
                        if( Generalization != null )
                        {
                            result &= Generalization.PrepareDependentItem( monitor, valueCollector );
                            if( _dContainer == null ) _dContainer = Generalization._dContainer;
                            if( _itemKind == DependentItemKind.Unknown ) _itemKind = Generalization._itemKind;
                            if( _trackAmbientPropertiesMode == TrackAmbientPropertiesMode.Unknown ) _trackAmbientPropertiesMode = Generalization._trackAmbientPropertiesMode;
                            // Sets it to true even if this level does not require it in order to follow the path in ResolvePreConstructAndPostBuildProperties:
                            // this captures the fact that this level or above needs to track the ambient properties.
                            _needsTrackedAmbientProperties = Generalization._needsTrackedAmbientProperties;
                        }
                        // Check configuration (avoiding warn for dynamically emitted types).
                        if( _itemKind == DependentItemKind.Unknown && !Type.Type.Assembly.IsDynamic )
                        {
                            monitor.Warn( $"Since ItemKind is not specified on this base class ('{ToString()}'), it defaults to SimpleItem. It should be explicitly set to either SimpleItem, Group or Container." );
                            _itemKind = DependentItemKind.Item;
                        }
                        if( _trackAmbientPropertiesMode == TrackAmbientPropertiesMode.Unknown ) _trackAmbientPropertiesMode = TrackAmbientPropertiesMode.None;
                        
                        // Allocates Ambient Properties tracking now that we know the final configuration for it.
                        Debug.Assert( _trackAmbientPropertiesMode != TrackAmbientPropertiesMode.Unknown );
                        if( _trackAmbientPropertiesMode != TrackAmbientPropertiesMode.None )
                        {
                            _trackedAmbientProperties = new List<TrackedAmbientPropertyInfo>();
                            _needsTrackedAmbientProperties = true;
                        }
                        // We can handle StObjProperties (check type coherency and propagate values) since the Container and 
                        // the Generalization have been prepared, StObj properties can safely be located and propagated to this StObj.
                        CheckStObjProperties( monitor, valueCollector );

                        // For AmbientProperties, this can not be done the same way: Ambient Properties are "projected to the leaf": they 
                        // have to be managed at the most specialized level: this is done in the next preparation step.
                    }
                    monitor.CloseGroup( $"ItemKind is {_itemKind}" );
                    return result;

                }
                finally
                {
                    _prepareState = PrepareState.PreparedDone;
                }
            }
        }

        bool ResolveDirectReferences( IActivityMonitor monitor )
        {
            Debug.Assert( _container != null && _constructParameterEx != null );
            bool result = true;
            _dFullName = Type.Type.FullName;
            _dContainer = _container.ResolveToStObj( monitor, EngineMap );
            // Requirement initialization.
            HashSet<MutableItem> req = new HashSet<MutableItem>();
            {
                // Requires are... Required (when not configured as optional by IStObjStructuralConfigurator).
                foreach( MutableItem dep in _requires.AsList.Select( r => r.ResolveToStObj( monitor, EngineMap ) ) )
                {
                    if( dep != null ) req.Add( dep );
                }
                // StObjConstruct parameters are Required... except:
                // - If they are one of our Container but this is handled
                //   at the DependencySorter level by using the SkipDependencyToContainer option.
                //   See the commented old code (to be kept) below for more detail on this option.
                // - If IStObjMutableParameter.SetParameterValue has been called by a IStObjStructuralConfigurator, then this 
                //   breaks the potential dependency.
                //
                IEnumerable<MutableParameter> allParams = _constructParametersAbove?.SelectMany( mp => mp.Item2 )
                                                            ?? Enumerable.Empty<MutableParameter>();
                allParams = allParams.Concat( _constructParameterEx );

                foreach( MutableParameter t in allParams )
                {
                    if( t.Value == System.Type.Missing )
                    {
                        MutableItem dep = t.ResolveToStObj( monitor, EngineMap );
                        if( dep != null ) req.Add( dep );
                    }
                }
            }
            // This will be updated after the Sort with clean Requirements (no Generalization nor Containers in it).
            _dRequires = req.ToArray();

            // RequiredBy initialization.
            if( _requiredBy.Count > 0 )
            {
                _dRequiredBy = _requiredBy.AsList.Select( r => r.ResolveToStObj( monitor, EngineMap ) ).Where( m => m != null ).ToArray();
            }
            // Children Initialization.
            if( _children.Count > 0 )
            {
                _dChildren = _children.AsList.Select( r => r.ResolveToStObj( monitor, EngineMap ) ).Where( m => m != null ).ToArray();
            }
            // Groups Initialization.
            if( _groups.Count > 0 )
            {
                _dGroups = _groups.AsList.Select( r => r.ResolveToStObj( monitor, EngineMap ) ).Where( m => m != null ).ToArray();
            }
            return result;
        }

        #region (Old fully commented PrepareDependentItem code to be kept for documentation - SkipDependencyToContainer option rationale).
        //internal void PrepareDependentItem( IActivityMonitor monitor, StObjCollectorResult result, StObjCollectorContextualResult contextResult )
        //{
        //    Debug.Assert( _container != null && _constructParameterEx != null );
        //    Debug.Assert( _context == contextResult.Context && result[_context] == contextResult, "We are called inside our typed context, this avoids the lookup result[Context] to obtain the owner's context (the default)." );

        //    // Container initialization.
        //    //
        //    // Since we want to remove all the containers of the object from its parameter requirements (see below), 
        //    // we can not rely on the DependencySorter to detect a cyclic chain of containers:
        //    // we use the list to collect the chain of containers and detect cycles.
        //    List<MutableItem> allContainers = null;
        //    ComputeFullNameAndResolveContainer( monitor, result, contextResult, ref allContainers );

        //    // Requirement initialization.
        //    HashSet<MutableItem> req = new HashSet<MutableItem>();
        //    {
        //        // Requires are... Required (when not configured as optional by IStObjStructuralConfigurator).
        //        foreach( MutableItem dep in _requires.Select( r => r.ResolveToStObj( monitor, result, contextResult ) ).Where( m => m != null ) )
        //        {
        //            req.Add( dep );
        //        }
        //        // Construct parameters are Required... except if they are one of our Container.
        //        if( _constructParameterEx.Count > 0 )
        //        {
        //            // We are here considering here that a Container parameter does NOT define a dependency to the whole container (with its content):
        //            //
        //            //      That seems strange: we may expect the container to be fully initialized when used as a parameter by a dependency Construct...
        //            //      The fact is that we are dealing with Objects that have a method Construct, that this Construct method is called on the head
        //            //      of the container (before any of its content) and that this method has no "thickness", no content in terms of dependencies: its
        //            //      execution fully initializes the StOj and we can use it.
        //            //      Construct method is a requirement on "Init", not on "InitContent".
        //            //      This is actually fully coherent with the way the setup works. An item of a package does not "require" its own package, it is 
        //            //      contained in its package and can require items in the package as it needs.
        //            // 
        //            foreach( MutableParameter t in _constructParameterEx )
        //            {
        //                // Do not consider the container as a requirement since a Container is
        //                // already a dependency (on the head's Container) and that a requirement on a container
        //                // targets the whole content of it (this would lead to a cycle in the dependency graph).
        //                MutableItem dep = t.ResolveToStObj( monitor, result, contextResult );
        //                if( dep != null && (allContainers == null || allContainers.Contains( dep ) == false) ) req.Add( dep );
        //            }
        //        }
        //    }
        //    _dRequires = req.ToReadOnlyList();

        //    // RequiredBy initialization.
        //    if( _requiredBy.Count > 0 )
        //    {
        //        _dRequiredBy = _requiredBy.Select( r => r.ResolveToStObj( monitor, result, contextResult ) ).Where( m => m != null ).ToReadOnlyList();
        //    }
        //}

        //void ComputeFullNameAndResolveContainer( IActivityMonitor monitor, StObjCollectorResult result, StObjCollectorContextualResult contextResult, ref List<MutableItem> prevContainers )
        //{
        //    if( _dFullName != null ) return;

        //    _dFullName = AmbientContractCollector.DisplayName( _context, _objectType.Type );
        //    _dContainer = _container.ResolveToStObj( monitor, result, contextResult );

        //    // Since we are obliged here to do in advance what the SetupOrderer will do (to remove dependencies to containers, see PrepareDependentItem above),
        //    // we must apply the "Container inheritance"...
            
        //    // TODO... Here or in DependencySorter... ?
        //    //    All this Container discovering stuff duplicates DependencySorter work...
        //    //    
        //    // => Answer: Done in the dependency sorter.

        //    if( _dContainer != null )
        //    {
        //        _dContainer._hasBeenReferencedAsAContainer = true;
        //        if( prevContainers == null ) prevContainers = new List<MutableItem>();
        //        else if( prevContainers.Contains( _dContainer ) )
        //        {
        //            monitor.Fatal().Send( "Recursive Container chain encountered: '{0}'.", String.Join( "', '", prevContainers.Select( m => m._dFullName ) ) );
        //            return;
        //        }
        //        prevContainers.Add( _dContainer );
        //        Type containerContext = _dContainer.Context;
        //        if( containerContext != contextResult.Context )
        //        {
        //            contextResult = result[containerContext];
        //            Debug.Assert( contextResult != null );
        //        }
        //        _dContainer.ComputeFullNameAndResolveContainer( monitor, result, contextResult, ref prevContainers );
        //    }
        //}
        #endregion

        /// <summary>
        /// Called by StObjCollector once the mutable items have been sorted.
        /// </summary>
        /// <param name="idx">The index in the whole ordered list of items.</param>
        /// <param name="rank">Rank in the depedency graph. This is used for Service association.</param>
        /// <param name="requiresFromSorter">Required items.</param>
        /// <param name="childrenFromSorter">Children items.</param>
        /// <param name="groupsFromSorter">Groups items.</param>
        internal void SetSorterData( int idx, int rank, IEnumerable<ISortedItem> requiresFromSorter, IEnumerable<ISortedItem> childrenFromSorter, IEnumerable<ISortedItem> groupsFromSorter )
        {
            Debug.Assert( IndexOrdered == 0 );
            IndexOrdered = idx;
            RankOrdered = rank;
            _dRequires = requiresFromSorter.Select( s => (MutableItem)s.Item ).ToArray();
            _dChildren = childrenFromSorter.Select( s => (MutableItem)s.Item ).ToArray();
            _dGroups = groupsFromSorter.Select( s => (MutableItem)s.Item ).ToArray();
            // requiredBy are useless.
            _dRequiredBy = null;
        }

        /// <summary>
        /// Gets the index of this IStObj. Available once ordering has been done.
        /// </summary>
        public int IndexOrdered { get; private set; }

        /// <summary>
        /// Gets the rank of this IStObj. Available once ordering has been done.
        /// </summary>
        public int RankOrdered { get; private set; }

        /// <summary>
        /// Overridden to return the Type full name.
        /// </summary>
        /// <returns>The type's full name.</returns>
        public override string ToString() => Type.Type.FullName;


        #region IDependentItem/Ref Members

        string IDependentItem.FullName => _dFullName; 

        IDependentItemRef IDependentItem.Generalization => Generalization; 

        IDependentItemContainerRef IDependentItem.Container => _dContainer; 

        IEnumerable<IDependentItemRef> IDependentItemGroup.Children
        {
            get
            {
                IEnumerable<IDependentItemRef> r = _dChildren;
                if( _trackAmbientPropertiesMode == TrackAmbientPropertiesMode.AddPropertyHolderAsChildren )
                {
                    Debug.Assert( _trackedAmbientProperties != null );
                    var t = _trackedAmbientProperties.Select( a => a.Owner );
                    r = r != null ? r.Concat( r ) : t;
                }
                return r;
            }
        }

        DependentItemKind IDependentItemContainerTyped.ItemKind => _itemKind; 

        IEnumerable<IDependentItemGroupRef> IDependentItem.Groups
        {
            get
            {
                IEnumerable<IDependentItemGroupRef> r = _dGroups;
                if( _trackAmbientPropertiesMode == TrackAmbientPropertiesMode.AddThisToPropertyHolderItems )
                {
                    Debug.Assert( _trackedAmbientProperties != null );
                    var t = _trackedAmbientProperties.Select( a => a.Owner );
                    r = r != null ? r.Concat( r ) : t;
                }
                return r;
            }
        }

        IEnumerable<IDependentItemRef> IDependentItem.Requires
        {
            get 
            {
                Debug.Assert( _dRequires != null, "Built from the HashSet in PrepareDependentItem." );
                IEnumerable<IDependentItemRef> r = _dRequires;
                if( _trackAmbientPropertiesMode == TrackAmbientPropertiesMode.PropertyHolderRequiresThis )
                {
                    Debug.Assert( _trackedAmbientProperties != null );
                    r = r.Concat( _trackedAmbientProperties.Select( a => a.Owner ) ); 
                }
                return r; 
            }
        }

        IEnumerable<IDependentItemRef> IDependentItem.RequiredBy
        {
            get 
            {
                IEnumerable<IDependentItemRef> r = _dRequiredBy;
                if( _trackAmbientPropertiesMode == TrackAmbientPropertiesMode.PropertyHolderRequiredByThis )
                {
                    Debug.Assert( _trackedAmbientProperties != null );
                    var t = _trackedAmbientProperties.Select( a => a.Owner );
                    r = r != null ? r.Concat( r ) : t;
                }
                return r;
            }
        }

        object IDependentItem.StartDependencySort( IActivityMonitor m ) =>  null;

        string IDependentItemRef.FullName => _dFullName; 

        bool IDependentItemRef.Optional => false; 
        #endregion

        #region IStObj Members

        public object InitialObject => _leafData.StructuredObject; 

        /// <summary>
        /// Gets the type of the structure object.
        /// </summary>
        public Type ObjectType => Type.Type; 

        IStObj IStObj.Generalization => Generalization; 

        IStObj IStObj.Specialization => Specialization; 

        IStObjResult IStObjResult.Generalization => Generalization; 

        IStObjResult IStObjResult.Specialization => Specialization; 

        IStObjResult IStObjResult.RootGeneralization => _leafData.RootGeneralization; 

        IStObjResult IStObjResult.LeafSpecialization => _leafData.LeafSpecialization; 

        IStObjResult IStObjResult.ConfiguredContainer => IsOwnContainer ? _dContainer : null; 

        IStObjResult IStObjResult.Container => _dContainer; 

        IReadOnlyList<IStObjResult> IStObjResult.Requires  => _dRequires; 

        IReadOnlyList<IStObjResult> IStObjResult.Children => _dChildren; 

        IReadOnlyList<IStObjResult> IStObjResult.Groups => _dGroups; 

        IReadOnlyList<IStObjTrackedAmbientPropertyInfo> IStObjResult.TrackedAmbientProperties => _trackedAmbientProperties;

        IStObjMap IStObj.StObjMap => EngineMap;

        object IStObjResult.GetStObjProperty( string propertyName )
        {
            StObjProperty p = GetStObjProperty( propertyName );
            return p != null ? p.Value : System.Type.Missing;
        }

        #endregion

    }
}
