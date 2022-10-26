using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Encapsulate type information for a Real Object or Auto Service class.
    /// Offers persistent access to attributes that support <see cref="IAttributeContextBound"/> interface.
    /// Attributes must be retrieved thanks to <see cref="Attributes"/>.
    /// This type information are built top-down (from generalization to most specialized type).
    /// <para>
    /// A CKTypeInfo can be either a <see cref="RealObjectClassInfo"/> (RealObjectClassInfo inherits from CKTypeInfo) or
    /// an independent one (this is a concrete class) that is associated to a <see cref="AutoServiceClassInfo"/> (<see cref="AutoServiceClassInfo.TypeInfo"/>). 
    /// </para>
    /// </summary>
    public class CKTypeInfo
    {
        readonly TypeAttributesCache? _attributes;
        readonly Type[] _interfacesCache;
        CKTypeInfo? _nextSibling;
        CKTypeInfo? _firstChild;
        int _specializationCount;
        bool _initializeImplementableTypeInfo;
        List<Type>? _uniqueMappings;
        List<Type>? _multipleMappings;

        /// <summary>
        /// Initializes a new <see cref="CKTypeInfo"/> from a base one (its <see cref="Generalization"/>) if it exists and a type.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="t">Type itself. Can not be null.</param>
        /// <param name="parent">Parent CKTypeInfo (Generalization). Null if the base type is not a IAutoService or IRealObject type.</param>
        /// <param name="services">Available services that will be used for delegated attribute constructor injection.</param>
        /// <param name="isExcluded">True to actually exclude this type from the registration.</param>
        /// <param name="serviceClass">Service class is mandatory if this is an independent Type info.</param>
        internal CKTypeInfo( IActivityMonitor monitor, CKTypeInfo? parent, Type t, IServiceProvider services, bool isExcluded, AutoServiceClassInfo? serviceClass )
        {
            Debug.Assert( (serviceClass == null) == (this is RealObjectClassInfo) );
            ServiceClass = serviceClass;
            Generalization = parent;
            Type = t;
            _interfacesCache = System.Type.EmptyTypes;
            if( (parent?.IsExcluded ?? false) )
            {
                monitor.Warn( $"Type {t.FullName} is excluded since its parent is excluded." );
                IsExcluded = true;
            }
            else if( IsExcluded = isExcluded )
            {
                monitor.Info( $"Type {t.FullName} is excluded." );
            }
            else
            {
                _attributes = new TypeAttributesCache( monitor, t, services, parent == null );
                _interfacesCache = t.GetInterfaces();
                if( parent != null )
                {
                    _nextSibling = parent._firstChild;
                    parent._firstChild = this;
                    ++parent._specializationCount;
                }
            }
        }

        /// <summary>
        /// Gets the unique mappings to this type that MUST be a leaf:
        /// an <see cref="InvalidOperationException"/> is thrown if <see cref="IsSpecialized"/> is true.
        /// </summary>
        public IReadOnlyCollection<Type> UniqueMappingTypes
        {
            get
            {
                if( IsSpecialized ) Throw.InvalidOperationException( $"Must be called on the most specialized type." );
                return (IReadOnlyCollection<Type>?)_uniqueMappings ?? Type.EmptyTypes;
            }
        }

        /// <summary>
        /// Gets the unique mappings to this type that MUST be a leaf:
        /// an <see cref="InvalidOperationException"/> is thrown if <see cref="IsSpecialized"/> is true.
        /// </summary>
        public IReadOnlyCollection<Type> MultipleMappingTypes
        {
            get
            {
                if( IsSpecialized ) throw new InvalidOperationException( $"Must be called on the most specialized type." );
                return (IReadOnlyCollection<Type>?)_multipleMappings ?? Type.EmptyTypes;
            }
        }

        /// <summary>
        /// Gets the service class information for this type if there is one.
        /// If this <see cref="CKTypeInfo"/> is an independent one, then this is necessarily not null.
        /// If this is a <see cref="RealObjectClassInfo"/> this can be null if the Real object doesn't
        /// support any IAutoService interfaces.
        /// </summary>
        public AutoServiceClassInfo? ServiceClass { get; internal set; }

        /// <summary>
        /// Gets the Type that is decorated.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets all the interfaces supported by this <see cref="Type"/> (the array is cached once for all).
        /// </summary>
        public IReadOnlyList<Type> Interfaces => _interfacesCache;

        /// <summary>
        /// Gets whether this Type is excluded from registration.
        /// </summary>
        public bool IsExcluded { get; }

        /// <summary>
        /// Gets the generalization of this <see cref="Type"/>, it is null if no base <see cref="IAutoService"/>
        /// or <see cref="IRealObject"/> exists.
        /// This property is valid even if this type is excluded (however this CKTypeInfo does not
        /// appear in generalization's <see cref="Specializations"/>).
        /// </summary>
        public CKTypeInfo? Generalization { get; }

        /// <summary>
        /// Gets the <see cref="ImplementableTypeInfo"/> if this <see cref="Type"/>
        /// is abstract, null otherwise.
        /// </summary>
        public ImplementableTypeInfo? ImplementableTypeInfo { get; private set; }

        /// <summary>
        /// Gets whether this Type (that is abstract) must actually be considered as an abstract type or not.
        /// An abstract class may be considered as concrete if there is a way to concretize an instance. 
        /// This must be called only for abstract types and if <paramref name="assembly"/> is not null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="assembly">The dynamic assembly to use for generated types.</param>
        /// <returns>Concrete Type builder or null.</returns>
        internal protected ImplementableTypeInfo? InitializeImplementableTypeInfo( IActivityMonitor monitor, IDynamicAssembly assembly )
        {
            Debug.Assert( Type.IsAbstract && assembly != null && !IsExcluded );

            if( _initializeImplementableTypeInfo ) return ImplementableTypeInfo;
            _initializeImplementableTypeInfo = true;

            var combined = new List<ICKCustomAttributeProvider>();
            CKTypeInfo? p = this;
            do
            {
                Debug.Assert( p.Attributes != null );
                combined.Add( p.Attributes );
                p = p.Generalization;
            }
            while( p != null );

            var autoImpl = ImplementableTypeInfo.CreateImplementableTypeInfo( monitor, Type, new CustomAttributeProviderComposite( combined ) );
            if( autoImpl != null && autoImpl.CreateStubType( monitor, assembly ) != null )
            {
                return ImplementableTypeInfo = autoImpl;
            }
            return null;
        }

        /// <summary>
        /// Gets the provider for attributes. Attributes that are marked with <see cref="IAttributeContextBound"/> are cached
        /// and can keep an internal state if needed.
        /// This is null if <see cref="IsExcluded"/> is true.
        /// </summary>
        /// <remarks>
        /// All attributes related to <see cref="Type"/> (either on the type itself or on any of its members) should be retrieved 
        /// thanks to this property otherwise stateful attributes will not work correctly.
        /// </remarks>
        public ITypeAttributesCache? Attributes => _attributes;

        /// <summary>
        /// Gets whether this type has at least one <see cref="Specializations"/>
        /// (only non excluded specializations are considered).
        /// </summary>
        public bool IsSpecialized => _firstChild != null;

        /// <summary>
        /// Gets the number of <see cref="Specializations"/>.
        /// (only non excluded specializations are considered).
        /// </summary>
        public int SpecializationsCount => _specializationCount;

        /// <summary>
        /// Gets the different specialized <see cref="CKTypeInfo"/> that are not excluded.
        /// </summary>
        /// <returns>An enumerable of <see cref="CKTypeInfo"/> that specialize this one.</returns>
        public IEnumerable<CKTypeInfo> Specializations
        {
            get
            {
                var c = _firstChild;
                while( c != null )
                {
                    yield return c;
                    c = c._nextSibling;
                }
            }
        }

        internal bool IsAssignableFrom( CKTypeInfo child )
        {
            Debug.Assert( child != null );
            CKTypeInfo? c = child;
            do
            {
                if( c == this ) return true;
            }
            while( (c = c.Generalization) != null );
            return false;
        }

        internal void RemoveSpecialization( CKTypeInfo child )
        {
            Debug.Assert( child.Generalization == this );
            if( _firstChild == child )
            {
                _firstChild = child._nextSibling;
                --_specializationCount;
            }
            else
            {
                var c = _firstChild;
                while( c != null && c._nextSibling != child ) c = c._nextSibling;
                if( c != null )
                {
                    c._nextSibling = child._nextSibling;
                    --_specializationCount;
                }
            }
        }

        /// <summary>
        /// Registers a new unique mapping.
        /// Must be called on a leaf (<see cref="IsSpecialized"/> must be false). The final type must be assignable to t, but must not be the type t itself.
        /// The type t must not already be registered.
        /// </summary>
        /// <param name="t">The type that must uniquely be associated to this most specialized type.</param>
        internal void AddUniqueMapping( Type t )
        {
            Debug.Assert( SpecializationsCount == 0, "We are on the leaf." );
            Debug.Assert( t != Type, $"Unique mapping {ToString()} must not be mapped to itself." );
            Debug.Assert( t.IsAssignableFrom( Type ), $"Unique mapping '{t}' must be assignable from {ToString()}!" );
            Debug.Assert( _uniqueMappings == null || !_uniqueMappings.Contains( t ), $"Unique mapping '{t}' already registered in {ToString()}." );
            Debug.Assert( _multipleMappings == null || !_multipleMappings.Contains( t ), $"Unique mapping '{t}' already registered in MULTIPLE mappings of {ToString()}." );
            if( _uniqueMappings == null ) _uniqueMappings = new List<Type>();
            _uniqueMappings.Add( t );
        }

        /// <summary>
        /// Registers a new multiple mapping.
        /// Must be called on a leaf (<see cref="IsSpecialized"/> must be false). The final type must be assignable to t, but must not be the type t itself.
        /// The type t must not already be registered (it can, of course be mapped to other final types).
        /// </summary>
        /// <param name="t">The type that must uniquely be associated to this most specialized type.</param>
        /// <param name="k">The kind from the <see cref="CKTypeKindDetector"/>.</param>
        /// <param name="collector">The type collector.</param>
        internal void AddMultipleMapping( IActivityMonitor monitor, Type t, CKTypeKind k, CKTypeCollector collector )
        {
            Debug.Assert( !IsSpecialized, "We are on the leaf." );
            Debug.Assert( t != Type, $"Multiple mapping {ToString()} must not be mapped to itself." );
            Debug.Assert( t.IsAssignableFrom( Type ), $"Multiple mapping '{t}' must be assignable from {ToString()}!" );
            Debug.Assert( _multipleMappings == null || !_multipleMappings.Contains( t ), $"Multiple mapping '{t}' already registered in {ToString()}." );
            Debug.Assert( _uniqueMappings == null || !_uniqueMappings.Contains( t ), $"Multiple mapping '{t}' already registered in UNIQUE mappings of {ToString()}." );
            Debug.Assert( (k & CKTypeKind.IsMultipleService) != 0 );
            if( _multipleMappings == null ) _multipleMappings = new List<Type>();
            _multipleMappings.Add( t );
            if( (k&(CKTypeKind.IsFrontService|CKTypeKind.IsMarshallable)) != (CKTypeKind.IsFrontService | CKTypeKind.IsMarshallable) )
            {
                collector.RegisterMultipleInterfaces( monitor, t, k, this );
            }
        }

        /// <summary>
        /// Overridden to return a readable string.
        /// </summary>
        /// <returns>Readable string.</returns>
        public override string ToString()
        {
            var s = Type.FullName;
            Debug.Assert( s != null, "Null FullName is for generic parameters." );
            if( ServiceClass != null ) s += "|IsService";
            if( this is RealObjectClassInfo ) s += "|IsObject";
            if( IsExcluded ) s += "|IsExcluded";
            if( IsSpecialized ) s += "|IsSpecialized";
            return s;
        }
    }
}
