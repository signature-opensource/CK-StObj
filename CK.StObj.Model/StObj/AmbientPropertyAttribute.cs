using System;

namespace CK.Core
{
    /// <summary>
    /// Defines an ambient property: properties tagged with this attribute can be automatically set
    /// with identically named properties value from containers. The <see cref="ResolutionSource"/> is inherited from 
    /// the same property on the base class or defaults to <see cref="PropertyResolutionSource.FromGeneralizationAndThenContainer"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Property, AllowMultiple = false, Inherited = true )]
    public class AmbientPropertyAttribute : Attribute, Setup.IAmbientPropertyOrInjectObjectAttribute
    {
        bool? _isOptional;
        PropertyResolutionSource? _source;

        /// <summary>
        /// Initializes a new <see cref="AmbientPropertyAttribute"/>.
        /// </summary>
        public AmbientPropertyAttribute()
        {
            _source = PropertyResolutionSource.FromGeneralizationAndThenContainer;
        }

        /// <summary>
        /// Gets or sets whether resolving this property is required or not.
        /// Defaults to false (unless explicitly stated, an ambient property MUST be resolved) but when 
        /// is not explicitly set to true or false on a specialized property its value is given by property 
        /// definition of the base class. 
        /// </summary>
        public bool IsOptional
        {
            get { return _isOptional.HasValue ? _isOptional.Value : false; }
            set { _isOptional = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="PropertyResolutionSource"/> for this property.
        /// Defaults to <see cref="PropertyResolutionSource.FromGeneralizationAndThenContainer"/>, but when 
        /// it is not explicitly set, its value is inherited from the property definition of the base class. 
        /// </summary>
        public PropertyResolutionSource ResolutionSource
        {
            get { return _source.HasValue ? _source.Value : PropertyResolutionSource.FromGeneralizationAndThenContainer; }
            set { _source = value; }
        }

        /// <summary>
        /// Gets whether <see cref="ResolutionSource"/> has been set.
        /// </summary>
        public bool IsResolutionSourceDefined => _source.HasValue; 

        bool Setup.IAmbientPropertyOrInjectObjectAttribute.IsOptionalDefined => _isOptional.HasValue; 

        bool Setup.IAmbientPropertyOrInjectObjectAttribute.IsAmbientProperty =>  true; 
    }
}
