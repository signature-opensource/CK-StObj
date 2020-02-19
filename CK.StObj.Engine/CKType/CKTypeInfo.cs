using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulate type information for a Real Object or Auto Service class.
    /// Offers persistent access to attributes that support <see cref="IAttributeContextBound"/> interface.
    /// Attributes must be retrieved thanks to <see cref="Attributes"/>.
    /// This type information are built top-down (from generalization to most specialized type).
    /// <para>
    /// An CKTypeInfo can be either a <see cref="AutoServiceClassInfo"/> or an independent one (this is a concrete class)
    /// that is associated to a <see cref="AutoServiceClassInfo"/> (via ServiceClass). 
    /// </para>
    /// </summary>
    public class CKTypeInfo
    {
        readonly TypeAttributesCache _attributes;
        CKTypeInfo _nextSibling;
        CKTypeInfo _firstChild;
        int _specializationCount;
        bool _initializeImplementableTypeInfo;

        /// <summary>
        /// Initializes a new <see cref="CKTypeInfo"/> from a base one (its <see cref="Generalization"/>) if it exists and a type.
        /// </summary>
        /// <param name="monitor">Monitor to use.</param>
        /// <param name="t">Type itself. Can not be null.</param>
        /// <param name="parent">Parent CKTypeInfo (Generalization). Null if the base type is not a IAutoService or IRealObject type.</param>
        /// <param name="services">Available services that will be used for delegated attribute constructor injection.</param>
        /// <param name="isExcluded">True to actually exclude this type from the registration.</param>
        /// <param name="serviceClass">Service class is mandatory if this is an independent Type info.</param>
        internal CKTypeInfo( IActivityMonitor monitor, CKTypeInfo parent, Type t, IServiceProvider services, bool isExcluded, AutoServiceClassInfo serviceClass )
        {
            Debug.Assert( (serviceClass == null) == (this is RealObjectClassInfo) );
            ServiceClass = serviceClass;
            if( (parent?.IsExcluded ?? false) )
            {
                monitor.Warn( $"Type {t.FullName} is excluded since its parent is excluded." );
                IsExcluded = true;
            }
            else if( IsExcluded = isExcluded )
            {
                monitor.Info( $"Type {t.FullName} is excluded." );
            }
            else _attributes = new TypeAttributesCache( monitor, t, services, parent == null );
            if( (Generalization = parent) != null && !IsExcluded )
            {
                _nextSibling = parent._firstChild;
                parent._firstChild = this;
                ++parent._specializationCount;
            }
        }


        /// <summary>
        /// Gets the service classe information for this type is there is one.
        /// If this <see cref="CKTypeInfo"/> is an independent one, then this is necessarily not null.
        /// If this is a <see cref="RealObjectClassInfo"/> this can be null or not.
        /// </summary>
        public AutoServiceClassInfo ServiceClass { get; internal set; }

        /// <summary>
        /// Gets the Type that is decorated.
        /// </summary>
        public Type Type => _attributes.Type;

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
        public CKTypeInfo Generalization { get; }

        /// <summary>
        /// Gets the <see cref="ImplementableTypeInfo"/> if this <see cref="Type"/>
        /// is abstract, null otherwise.
        /// </summary>
        public ImplementableTypeInfo ImplementableTypeInfo { get; private set; }

        /// <summary>
        /// Gets whether this Type (that is abstract) must actually be considered as an abstract type or not.
        /// An abstract class may be considered as concrete if there is a way to concretize an instance. 
        /// This must be called only for abstract types and if <paramref name="assembly"/> is not null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="assembly">The dynamic assembly to use for generated types.</param>
        /// <returns>Concrete Type builder or null.</returns>
        internal protected ImplementableTypeInfo InitializeImplementableTypeInfo( IActivityMonitor monitor, IDynamicAssembly assembly )
        {
            Debug.Assert( Type.IsAbstract && assembly != null && !IsExcluded );

            if( _initializeImplementableTypeInfo ) return ImplementableTypeInfo;
            _initializeImplementableTypeInfo = true;

            var combined = new List<ICKCustomAttributeProvider>();
            var p = this;
            do { combined.Add( p.Attributes ); p = p.Generalization; } while( p != null );

            ImplementableTypeInfo autoImpl = ImplementableTypeInfo.CreateImplementableTypeInfo( monitor, Type, new CustomAttributeProviderComposite( combined ) );
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
        public ICKCustomAttributeTypeMultiProvider Attributes => _attributes;

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
                CKTypeInfo c = _firstChild;
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
            do
            {
                if( child == this ) return true;
            }
            while( (child = child.Generalization) != null );
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
                CKTypeInfo c = _firstChild;
                while( c != null && c._nextSibling != child ) c = c._nextSibling;
                if( c != null )
                {
                    c._nextSibling = child._nextSibling;
                    --_specializationCount;
                }
            }
        }

        /// <summary>
        /// Overridden to return a readable string.
        /// </summary>
        /// <returns>Readable string.</returns>
        public override string ToString()
        {
            var s = Type.FullName;
            if( ServiceClass != null ) s += "|IsService";
            if( this is RealObjectClassInfo ) s += "|IsObject";
            if( IsExcluded ) s += "|IsExcluded";
            if( IsSpecialized ) s += "|IsSpecialized";
            return s;
        }
    }
}
